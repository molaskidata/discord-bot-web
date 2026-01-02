using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Discord;
using MainbotCSharp.Services;
using System;

namespace MainbotCSharp.Modules
{
    public class VoiceCommands : ModuleBase<SocketCommandContext>
    {
        [Command("voicename")]
        [Summary("Change your voice channel name")]
        public async Task VoiceNameAsync([Remainder] string name)
        {
            var svc = VoiceService.GetConfig();
            var ownerInfo = VoiceService.GetActiveChannelByOwner(Context.User.Id);
            if (ownerInfo == null)
            {
                await ReplyAsync("❌ You don't own an active voice channel.");
                return;
            }

            // Find channel by owner mapping
            ulong channelId = 0;
            foreach (var kv in svc.ActiveChannels)
            {
                if (kv.Value.OwnerId == Context.User.Id)
                {
                    channelId = kv.Key;
                    break;
                }
            }

            if (channelId == 0)
            {
                await ReplyAsync("❌ Cannot find your channel.");
                return;
            }

            var channel = Context.Guild.GetVoiceChannel(channelId);
            if (channel == null)
            {
                await ReplyAsync("❌ Channel not found.");
                return;
            }

            try
            {
                await channel.ModifyAsync(p => p.Name = name);
                await ReplyAsync($"✅ Channel renamed to **{name}**");
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Failed to rename channel: {ex.Message}");
            }
        }

        [Command("voicelimit")]
        [Summary("Set user limit for your voice channel")]
        public async Task VoiceLimitAsync(int limit)
        {
            if (limit < 0 || limit > 99)
            {
                await ReplyAsync("❌ Limit must be between 0-99 (0 = unlimited)");
                return;
            }

            var svc = VoiceService.GetConfig();
            var ownerInfo = VoiceService.GetActiveChannelByOwner(Context.User.Id);
            if (ownerInfo == null)
            {
                await ReplyAsync("❌ You don't own an active voice channel.");
                return;
            }

            ulong channelId = 0;
            foreach (var kv in svc.ActiveChannels)
            {
                if (kv.Value.OwnerId == Context.User.Id)
                {
                    channelId = kv.Key;
                    break;
                }
            }

            var channel = Context.Guild.GetVoiceChannel(channelId);
            if (channel == null)
            {
                await ReplyAsync("❌ Channel not found.");
                return;
            }

            try
            {
                await channel.ModifyAsync(p => p.UserLimit = limit);
                await ReplyAsync(limit == 0 ? "✅ Channel limit removed (unlimited)" : $"✅ Channel limit set to {limit} users");
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Failed to set limit: {ex.Message}");
            }
        }

        [Command("voiceprivate")]
        [Summary("Make your voice channel private")]
        public async Task VoicePrivateAsync()
        {
            var svc = VoiceService.GetConfig();
            var ownerInfo = VoiceService.GetActiveChannelByOwner(Context.User.Id);
            if (ownerInfo == null)
            {
                await ReplyAsync("❌ You don't own an active voice channel.");
                return;
            }

            ulong channelId = 0;
            foreach (var kv in svc.ActiveChannels)
            {
                if (kv.Value.OwnerId == Context.User.Id)
                {
                    channelId = kv.Key;
                    break;
                }
            }

            var channel = Context.Guild.GetVoiceChannel(channelId);
            if (channel == null)
            {
                await ReplyAsync("❌ Channel not found.");
                return;
            }

            try
            {
                var overwrites = channel.PermissionOverwrites.ToList();
                var everyoneOverwrite = new Overwrite(Context.Guild.EveryoneRole.Id, PermissionTarget.Role,
                    new OverwritePermissions(viewChannel: PermValue.Deny, connect: PermValue.Deny));
                var ownerOverwrite = new Overwrite(Context.User.Id, PermissionTarget.User,
                    new OverwritePermissions(viewChannel: PermValue.Allow, connect: PermValue.Allow, speak: PermValue.Allow));

                overwrites.RemoveAll(o => o.TargetId == Context.Guild.EveryoneRole.Id);
                overwrites.Add(everyoneOverwrite);
                overwrites.Add(ownerOverwrite);

                await channel.ModifyAsync(p => p.PermissionOverwrites = overwrites);
                await ReplyAsync("🔒 **Channel is now private**");

                svc.ActiveChannels[channelId].IsPrivate = true;
                VoiceService.SetConfig(svc);
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Failed to make channel private: {ex.Message}");
            }
        }

        [Command("voicepublic")]
        [Summary("Make your voice channel public")]
        public async Task VoicePublicAsync()
        {
            var svc = VoiceService.GetConfig();
            var ownerInfo = VoiceService.GetActiveChannelByOwner(Context.User.Id);
            if (ownerInfo == null)
            {
                await ReplyAsync("❌ You don't own an active voice channel.");
                return;
            }

            ulong channelId = 0;
            foreach (var kv in svc.ActiveChannels)
            {
                if (kv.Value.OwnerId == Context.User.Id)
                {
                    channelId = kv.Key;
                    break;
                }
            }

            var channel = Context.Guild.GetVoiceChannel(channelId);
            if (channel == null)
            {
                await ReplyAsync("❌ Channel not found.");
                return;
            }

            try
            {
                var overwrites = channel.PermissionOverwrites.ToList();
                overwrites.RemoveAll(o => o.TargetId == Context.Guild.EveryoneRole.Id);

                await channel.ModifyAsync(p => p.PermissionOverwrites = overwrites);
                await ReplyAsync("🔓 **Channel is now public**");

                svc.ActiveChannels[channelId].IsPrivate = false;
                VoiceService.SetConfig(svc);
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Failed to make channel public: {ex.Message}");
            }
        }

        [Command("voicesetup")]
        [Summary("Setup voice channel system (Admin only)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task VoiceSetupAsync()
        {
            await ReplyAsync("🎤 **Voice System Setup**\n\nPlease mention or provide the ID for:\n" +
                           "1. Join-to-create channel\n2. Category for new channels\n3. Log channel\n\n" +
                           "Example: `!voicesetup #join-here #voice-category #voice-logs`");
        }

        [Command("voicesetup")]
        [Summary("Setup voice channel system with channels")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task VoiceSetupAsync(IVoiceChannel joinChannel, ICategoryChannel category, ITextChannel logChannel)
        {
            try
            {
                var config = VoiceService.GetConfig();
                config.JoinToCreateChannel = joinChannel.Id;
                config.JoinToCreateCategory = category.Id;
                config.VoiceChannelCategory = category.Id;
                config.VoiceLogChannel = logChannel.Id;

                VoiceService.SetConfig(config);

                var embed = new EmbedBuilder()
                    .WithTitle("✅ Voice System Configured")
                    .WithColor(Color.Green)
                    .AddField("Join-to-Create", joinChannel.Name, true)
                    .AddField("Category", category.Name, true)
                    .AddField("Log Channel", logChannel.Name, true)
                    .WithDescription("Users can now join the designated channel to create their own voice room!");

                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Setup failed: {ex.Message}");
            }
        }

        [Command("voicestats")]
        [Summary("Show voice statistics")]
        public async Task VoiceStatsAsync()
        {
            try
            {
                var logs = VoiceService.GetLogs();
                var config = VoiceService.GetConfig();

                var embed = new EmbedBuilder()
                    .WithTitle("📊 Voice Statistics")
                    .WithColor(Color.Blue)
                    .AddField("Active Channels", config.ActiveChannels.Count.ToString(), true)
                    .AddField("Total Log Entries", logs.Logs.Count.ToString(), true)
                    .AddField("Tracked Users", logs.Stats.Count.ToString(), true);

                if (logs.Stats.Any())
                {
                    var topUsers = logs.Stats.Values
                        .OrderByDescending(s => s.TotalJoins)
                        .Take(5)
                        .Select(s => $"{s.Username}: {s.TotalJoins} joins, {s.ChannelsCreated} created")
                        .ToList();

                    if (topUsers.Any())
                        embed.AddField("Top Users", string.Join("\n", topUsers), false);
                }

                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Failed to get stats: {ex.Message}");
            }
        }

        [Command("voicetemplate")]
        [Summary("Create/modify voice templates (Premium users only)")]
        public async Task VoiceTemplateAsync(string templateName, int userLimit, [Remainder] string channelName)
        {
            if (!VoiceService.IsPremiumUser(Context.User.Id))
            {
                await ReplyAsync("❌ This feature is only available for premium users.");
                return;
            }

            if (userLimit < 0 || userLimit > 99)
            {
                await ReplyAsync("❌ User limit must be between 0-99 (0 = unlimited)");
                return;
            }

            try
            {
                VoiceService.AddOrUpdateTemplate(templateName.ToLowerInvariant(), channelName, userLimit);
                await ReplyAsync($"✅ Template **{templateName}** created/updated:\n" +
                               $"Name: {channelName}\n" +
                               $"Limit: {(userLimit == 0 ? "Unlimited" : userLimit.ToString())}");
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Failed to create template: {ex.Message}");
            }
        }

        [Command("voicehelp")]
        [Summary("Show voice commands help")]
        public async Task VoiceHelpAsync()
        {
            var embed = new EmbedBuilder()
                .WithTitle("🎤 Voice Commands Help")
                .WithColor(Color.Purple)
                .AddField("Channel Management",
                    "`!voicename <name>` - Rename your channel\n" +
                    "`!voicelimit <0-99>` - Set user limit (0 = unlimited)\n" +
                    "`!voiceprivate` - Make channel private\n" +
                    "`!voicepublic` - Make channel public", false)
                .AddField("System & Stats",
                    "`!voicestats` - Show voice statistics\n" +
                    "`!voicesetup` - Setup system (Admin)\n" +
                    "`!voicetemplate` - Manage templates (Premium)", false)
                .WithFooter("Join the designated channel to create your own voice room!");

            await ReplyAsync(embed: embed.Build());
        }
    }
}
