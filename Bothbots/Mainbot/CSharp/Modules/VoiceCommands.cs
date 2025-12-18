using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Discord;

namespace MainbotCSharp.Modules
{
    public class VoiceCommands : ModuleBase<SocketCommandContext>
    {
        [Command("voicename")]
        public async Task VoiceNameAsync([Remainder] string name)
        {
            var svc = MainbotCSharp.Services.VoiceService.GetConfig();
            var ownerInfo = MainbotCSharp.Services.VoiceService.GetActiveChannelByOwner(Context.User.Id);
            if (ownerInfo == null) { await ReplyAsync("You don't own an active voice channel."); return; }
            var ch = Context.Guild.GetVoiceChannel(Context.Guild.VoiceChannels.FirstOrDefault(c => c.Id == ownerInfo.GetType().GetProperty("OwnerId")?.GetValue(ownerInfo) as ulong? ?? 0) != null ? ownerInfo.GetType().GetProperty("OwnerId")?.GetValue(ownerInfo) as ulong? ?? 0 : 0);
            // fallback: try find by owner mapping
            ulong channelId = 0;
            foreach (var kv in svc.ActiveChannels) if (kv.Value.OwnerId == Context.User.Id) { channelId = kv.Key; break; }
            if (channelId == 0) { await ReplyAsync("Cannot find your channel."); return; }
            var channel = Context.Guild.GetVoiceChannel(channelId);
            if (channel == null) { await ReplyAsync("Channel not found."); return; }
            try { await channel.ModifyAsync(p => p.Name = name); await ReplyAsync($"✅ Channel renamed to {name}"); }
            catch { await ReplyAsync("Failed to rename channel."); }
        }

        [Command("voicelock")]
        public async Task VoiceLockAsync()
        {
            var svc = MainbotCSharp.Services.VoiceService.GetConfig();
            ulong channelId = 0;
            foreach (var kv in svc.ActiveChannels) if (kv.Value.OwnerId == Context.User.Id) { channelId = kv.Key; break; }
            if (channelId == 0) { await ReplyAsync("You don't own an active voice channel."); return; }
            var channel = Context.Guild.GetVoiceChannel(channelId);
            if (channel == null) { await ReplyAsync("Channel not found."); return; }
            try
            {
                var everyone = Context.Guild.EveryoneRole;
                // toggle: if currently denied connect -> remove deny, else set deny
                var perms = channel.GetPermissionOverwrite(everyone);
                if (perms.HasValue && perms.Value.Connect == PermValue.Deny)
                {
                    await channel.RemovePermissionOverwriteAsync(everyone);
                    await ReplyAsync("🔓 Channel unlocked.");
                }
                else
                {
                    await channel.AddPermissionOverwriteAsync(everyone, new OverwritePermissions(connect: PermValue.Deny));
                    await ReplyAsync("🔒 Channel locked.");
                }
            }
            catch { await ReplyAsync("Failed to toggle lock."); }
        }

        [Command("voiceunlock")]
        public async Task VoiceUnlockAsync()
        {
            var svc = MainbotCSharp.Services.VoiceService.GetConfig();
            ulong channelId = 0;
            foreach (var kv in svc.ActiveChannels) if (kv.Value.OwnerId == Context.User.Id) { channelId = kv.Key; break; }
            if (channelId == 0) { await ReplyAsync("You don't own an active voice channel."); return; }
            var channel = Context.Guild.GetVoiceChannel(channelId);
            if (channel == null) { await ReplyAsync("Channel not found."); return; }
            try { await channel.RemovePermissionOverwriteAsync(Context.Guild.EveryoneRole); await ReplyAsync("🔓 Channel unlocked."); } catch { await ReplyAsync("Failed to unlock channel."); }
        }

        [Command("voicelimit")]
        public async Task VoiceLimitAsync(int limit)
        {
            if (limit < 0 || limit > 99) { await ReplyAsync("Usage: `!voicelimit [0-99]` (0 = unlimited)"); return; }
            var svc = MainbotCSharp.Services.VoiceService.GetConfig();
            ulong channelId = 0;
            foreach (var kv in svc.ActiveChannels) if (kv.Value.OwnerId == Context.User.Id) { channelId = kv.Key; break; }
            if (channelId == 0) { await ReplyAsync("You don't own an active voice channel."); return; }
            var channel = Context.Guild.GetVoiceChannel(channelId);
            if (channel == null) { await ReplyAsync("Channel not found."); return; }
            try { await channel.ModifyAsync(p => p.UserLimit = limit); await ReplyAsync($"✅ Voice limit set to {limit}"); } catch { await ReplyAsync("Failed to set limit."); }
        }

        [Command("voicetemplate")]
        public async Task VoiceTemplateAsync([Remainder] string templateKey)
        {
            var key = templateKey.Trim().ToLower();
            var cfg = MainbotCSharp.Services.VoiceService.GetConfig();
            if (!cfg.Templates.ContainsKey(key)) { await ReplyAsync($"Unknown template '{key}'. Available: {string.Join(", ", cfg.Templates.Keys)}"); return; }
            ulong channelId = 0;
            foreach (var kv in cfg.ActiveChannels) if (kv.Value.OwnerId == Context.User.Id) { channelId = kv.Key; break; }
            if (channelId == 0) { await ReplyAsync("You don't own an active voice channel."); return; }
            cfg.ActiveChannels[channelId].Template = key;
            MainbotCSharp.Services.VoiceService.SetConfig(cfg);
            await ReplyAsync($"✅ Template '{key}' applied to your channel.");
        }

        [Command("voicekick")]
        public async Task VoiceKickAsync(SocketGuildUser target)
        {
            if (target == null) { await ReplyAsync("Usage: !voicekick @user"); return; }
            var cfg = MainbotCSharp.Services.VoiceService.GetConfig();
            if (Context.User is SocketGuildUser guser)
            {
                if (guser.VoiceChannel == null) { await ReplyAsync("You are not in a voice channel."); return; }
                if (!cfg.ActiveChannels.ContainsKey(guser.VoiceChannel.Id) || cfg.ActiveChannels[guser.VoiceChannel.Id].OwnerId != Context.User.Id) { await ReplyAsync("You must be the owner of the voice channel to use this."); return; }
                if (target.VoiceChannel == null || target.VoiceChannel.Id != guser.VoiceChannel.Id) { await ReplyAsync("That user is not in your voice channel."); return; }
                try { await target.ModifyAsync(x => x.ChannelId = null); await ReplyAsync($"✅ Kicked {target.Username} from the channel."); } catch { await ReplyAsync("Failed to kick user."); }
            }
        }

        [Command("voicepermit")]
        public async Task VoicePermitAsync(SocketUser user)
        {
            var target = user as SocketGuildUser;
            if (target == null) { await ReplyAsync("Usage: !voicepermit @user"); return; }
            var cfg = MainbotCSharp.Services.VoiceService.GetConfig();
            if (Context.User is SocketGuildUser guser)
            {
                if (guser.VoiceChannel == null) { await ReplyAsync("You are not in a voice channel."); return; }
                if (!cfg.ActiveChannels.ContainsKey(guser.VoiceChannel.Id) || cfg.ActiveChannels[guser.VoiceChannel.Id].OwnerId != Context.User.Id) { await ReplyAsync("You must be the owner of the voice channel to use this."); return; }
                var channel = Context.Guild.GetVoiceChannel(guser.VoiceChannel.Id);
                try { await channel.AddPermissionOverwriteAsync(target, new OverwritePermissions(connect: PermValue.Allow)); await ReplyAsync($"✅ Permitted {target.Username}."); } catch { await ReplyAsync("Failed to update permissions."); }
            }
        }

        [Command("voicedeny")]
        public async Task VoiceDenyAsync(SocketUser user)
        {
            var target = user as SocketGuildUser;
            if (target == null) { await ReplyAsync("Usage: !voicedeny @user"); return; }
            var cfg = MainbotCSharp.Services.VoiceService.GetConfig();
            if (Context.User is SocketGuildUser guser)
            {
                if (guser.VoiceChannel == null) { await ReplyAsync("You are not in a voice channel."); return; }
                if (!cfg.ActiveChannels.ContainsKey(guser.VoiceChannel.Id) || cfg.ActiveChannels[guser.VoiceChannel.Id].OwnerId != Context.User.Id) { await ReplyAsync("You must be the owner of the voice channel to use this."); return; }
                var channel = Context.Guild.GetVoiceChannel(guser.VoiceChannel.Id);
                try { await channel.AddPermissionOverwriteAsync(target, new OverwritePermissions(connect: PermValue.Deny)); if (target.VoiceChannel != null && target.VoiceChannel.Id == channel.Id) await target.ModifyAsync(x => x.ChannelId = null); await ReplyAsync($"✅ Denied {target.Username}."); } catch { await ReplyAsync("Failed to update permissions."); }
            }
        }

        [Command("voiceprivate")]
        public async Task VoicePrivateAsync()
        {
            var cfg = MainbotCSharp.Services.VoiceService.GetConfig();
            if (Context.User is SocketGuildUser guser)
            {
                if (guser.VoiceChannel == null) { await ReplyAsync("You are not in a voice channel."); return; }
                if (!cfg.ActiveChannels.ContainsKey(guser.VoiceChannel.Id) || cfg.ActiveChannels[guser.VoiceChannel.Id].OwnerId != Context.User.Id) { await ReplyAsync("You must be the owner of the voice channel to use this."); return; }
                cfg.ActiveChannels[guser.VoiceChannel.Id].IsPrivate = true;
                MainbotCSharp.Services.VoiceService.SetConfig(cfg);
                await ReplyAsync("🔒 Channel is now private! Use `!voicepermit @user` to allow specific users.");
            }
        }

        [Command("voicestats")]
        public async Task VoiceStatsAsync()
        {
            var logs = MainbotCSharp.Services.VoiceService.GetLogs();
            if (logs == null || logs.Stats == null || logs.Stats.Count == 0) { await ReplyAsync("❌ No voice activity recorded yet."); return; }
            var sorted = logs.Stats.OrderByDescending(kv => kv.Value.TotalJoins).Take(10).Select(kv => $"{kv.Value.Username}: {kv.Value.TotalJoins} joins");
            await ReplyAsync("🎙️ Voice Activity Stats:\n" + string.Join('\n', sorted));
        }

        [Command("cleanupvoice")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task CleanupVoiceAsync()
        {
            var cfg = MainbotCSharp.Services.VoiceService.GetConfig();
            if (cfg == null || !cfg.VoiceLogChannel.HasValue) { await ReplyAsync("❌ No voice log channel configured. Use `!setupvoicelog` first."); return; }
            var logChan = Context.Guild.GetTextChannel(cfg.VoiceLogChannel.Value);
            if (logChan == null) { await ReplyAsync("❌ Voice log channel not found."); return; }
            int deleted = 0;
            try
            {
                var messages = await logChan.GetMessagesAsync(100).FlattenAsync();
                foreach (var msg in messages) { try { if (!msg.Pinned) { await msg.DeleteAsync(); deleted++; } } catch { } }
                await ReplyAsync($"✅ Voice log channel cleaned! Deleted {deleted} messages.");
            }
            catch { await ReplyAsync("❌ Error cleaning voice log channel."); }
        }

        [Command("deletevoice")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task DeleteVoiceAsync([Remainder] string confirm = null)
        {
            if (confirm != "CONFIRM") { await ReplyAsync("⚠️ This will delete all voice system channels and reset settings. Run `!deletevoice CONFIRM` to proceed."); return; }
            var cfg = MainbotCSharp.Services.VoiceService.GetConfig();
            int deletedCount = 0; var errors = new System.Collections.Generic.List<string>();
            try
            {
                if (cfg.JoinToCreateChannel.HasValue) { try { var ch = Context.Guild.GetVoiceChannel(cfg.JoinToCreateChannel.Value); if (ch != null) { await ch.DeleteAsync(); deletedCount++; } } catch { errors.Add("Join-to-Create channel"); } }
                if (cfg.VoiceLogChannel.HasValue) { try { var ch = Context.Guild.GetTextChannel(cfg.VoiceLogChannel.Value); if (ch != null) { await ch.DeleteAsync(); deletedCount++; } } catch { errors.Add("Voice log channel"); } }
                if (cfg.ActiveChannels != null) { foreach (var channelId in cfg.ActiveChannels.Keys.ToList()) { try { var ch = Context.Guild.GetVoiceChannel(channelId); if (ch != null) { await ch.DeleteAsync(); deletedCount++; } } catch { errors.Add($"Voice channel {channelId}"); } } }
                cfg.JoinToCreateChannel = null; cfg.JoinToCreateCategory = null; cfg.VoiceChannelCategory = null; cfg.VoiceLogChannel = null; cfg.ActiveChannels = new System.Collections.Generic.Dictionary<ulong, MainbotCSharp.Services.ActiveChannelInfo>();
                MainbotCSharp.Services.VoiceService.SetConfig(cfg);
                await ReplyAsync($"✅ Voice System Deleted! Deleted {deletedCount} channels. Errors: {string.Join(',', errors)}");
            }
            catch (Exception ex) { await ReplyAsync("❌ Error deleting voice system: " + ex.Message); }
        }
    }
}
