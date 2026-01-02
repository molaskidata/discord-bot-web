using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Discord;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MainbotCSharp.Modules
{
    // Voice Service Classes
    public class VoiceConfig
    {
        public ulong? JoinToCreateChannel { get; set; }
        public ulong? JoinToCreateCategory { get; set; }
        public ulong? VoiceChannelCategory { get; set; }
        public ulong? VoiceLogChannel { get; set; }
        public Dictionary<ulong, ActiveChannelInfo> ActiveChannels { get; set; } = new();
        public Dictionary<string, VoiceTemplate> Templates { get; set; } = new()
        {
            { "gaming", new VoiceTemplate{ Name = "🎮 Gaming Room", Limit = 0 } },
            { "study", new VoiceTemplate{ Name = "📚 Study Session", Limit = 4 } },
            { "chill", new VoiceTemplate{ Name = "💤 Chill Zone", Limit = 0 } },
            { "custom", new VoiceTemplate{ Name = "🔊 Voice Chat", Limit = 0 } }
        };
    }

    public class ActiveChannelInfo { public ulong OwnerId { get; set; } public long CreatedAt { get; set; } public string Template { get; set; } public bool IsPrivate { get; set; } }
    public class VoiceTemplate { public string Name { get; set; } public int Limit { get; set; } }

    public class VoiceLogs
    {
        public List<VoiceLogEntry> Logs { get; set; } = new();
        public Dictionary<string, VoiceStats> Stats { get; set; } = new();
    }
    public class VoiceLogEntry { public string UserId; public string Username; public string Action; public string ChannelName; public string Timestamp; }
    public class VoiceStats { public string Username; public int TotalJoins; public long TotalTime; public int ChannelsCreated; }

    public static class VoiceService
    {
        private const string VOICE_CONFIG_FILE = "voice_config.json";
        private const string VOICE_LOG_FILE = "voice_logs.json";

        private static VoiceConfig _config = LoadVoiceConfig();
        private static VoiceLogs _logs = LoadVoiceLogs();
        private static readonly HashSet<ulong> PREMIUM_USERS = new() { 1105877268775051316ul };

        private static Dictionary<ulong, Dictionary<ulong, long>> _afkTracker = new(); // channelId -> (userId -> lastActivity)

        private static VoiceConfig LoadVoiceConfig()
        {
            try
            {
                if (!File.Exists(VOICE_CONFIG_FILE)) return new VoiceConfig();
                var txt = File.ReadAllText(VOICE_CONFIG_FILE);
                return JsonSerializer.Deserialize<VoiceConfig>(txt) ?? new VoiceConfig();
            }
            catch { return new VoiceConfig(); }
        }

        private static void SaveVoiceConfig()
        {
            try
            {
                var txt = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(VOICE_CONFIG_FILE, txt);
            }
            catch (Exception ex) { Console.WriteLine("Failed to save voice config: " + ex); }
        }

        private static VoiceLogs LoadVoiceLogs()
        {
            try
            {
                if (!File.Exists(VOICE_LOG_FILE)) return new VoiceLogs();
                var txt = File.ReadAllText(VOICE_LOG_FILE);
                return JsonSerializer.Deserialize<VoiceLogs>(txt) ?? new VoiceLogs();
            }
            catch { return new VoiceLogs(); }
        }

        private static void SaveVoiceLogs()
        {
            try
            {
                var txt = JsonSerializer.Serialize(_logs, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(VOICE_LOG_FILE, txt);
            }
            catch (Exception ex) { Console.WriteLine("Failed to save voice logs: " + ex); }
        }

        public static bool IsPremiumUser(ulong id) => PREMIUM_USERS.Contains(id);

        private static void AddVoiceLog(ulong userId, string username, string action, string channelName)
        {
            _logs.Logs.Add(new VoiceLogEntry { UserId = userId.ToString(), Username = username, Action = action, ChannelName = channelName, Timestamp = DateTime.UtcNow.ToString("o") });
            if (!_logs.Stats.ContainsKey(userId.ToString())) _logs.Stats[userId.ToString()] = new VoiceStats { Username = username };
            var s = _logs.Stats[userId.ToString()];
            if (action == "joined") s.TotalJoins++;
            if (action == "created") s.ChannelsCreated++;
            if (_logs.Logs.Count > 1000) _logs.Logs = _logs.Logs.Skip(Math.Max(0, _logs.Logs.Count - 1000)).ToList();
            SaveVoiceLogs();
        }

        public static async Task HandleVoiceStateUpdatedAsync(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
        {
            try
            {
                var guild = (oldState.VoiceChannel ?? newState.VoiceChannel)?.Guild;
                if (guild == null) return;

                var member = guild.GetUser(user.Id);

                // joined
                if (oldState.VoiceChannel == null && newState.VoiceChannel != null)
                {
                    await HandleUserJoined(member, newState.VoiceChannel);
                    return;
                }

                // left
                if (oldState.VoiceChannel != null && newState.VoiceChannel == null)
                {
                    await HandleUserLeft(member, oldState.VoiceChannel);
                    return;
                }

                // moved
                if (oldState.VoiceChannel != null && newState.VoiceChannel != null && oldState.VoiceChannel.Id != newState.VoiceChannel.Id)
                {
                    await HandleUserLeft(member, oldState.VoiceChannel);
                    await HandleUserJoined(member, newState.VoiceChannel);
                    return;
                }
            }
            catch (Exception ex) { Console.WriteLine("Voice state handler error: " + ex); }
        }

        private static async Task HandleUserJoined(SocketGuildUser member, SocketVoiceChannel channel)
        {
            var cfg = _config;
            if (cfg == null) return;
            if (channel == null || member == null) return;

            if (cfg.JoinToCreateChannel.HasValue && channel.Id == cfg.JoinToCreateChannel.Value)
            {
                await CreateVoiceChannel(member, channel.Guild, cfg);
                return;
            }

            UpdateAfkTracker(channel.Id, member.Id);
            AddVoiceLog(member.Id, member.Username, "joined", channel.Name);
            await SendToLogChannel(channel.Guild, cfg, $"✅ **{member.Username}** joined **{channel.Name}**");
        }

        private static async Task HandleUserLeft(SocketGuildUser member, SocketVoiceChannel channel)
        {
            var cfg = _config;
            if (channel == null || cfg == null) return;

            if (cfg.ActiveChannels.ContainsKey(channel.Id))
            {
                var ch = channel;
                if (ch != null && ch.Users.Count == 0)
                {
                    try { await ch.DeleteAsync(new RequestOptions { AuditLogReason = "Voice channel empty" }); } catch { }
                    cfg.ActiveChannels.Remove(channel.Id);
                    SaveVoiceConfig();
                    await SendToLogChannel(channel.Guild, cfg, $"🗑️ **{channel.Name}** was deleted (empty)");
                }
            }

            if (_afkTracker.ContainsKey(channel.Id))
            {
                _afkTracker[channel.Id].Remove(member.Id);
            }

            AddVoiceLog(member.Id, member.Username, "left", channel.Name);
            await SendToLogChannel(channel.Guild, cfg, $"❌ **{member.Username}** left **{channel.Name}**");
        }

        private static async Task CreateVoiceChannel(SocketGuildUser member, SocketGuild guild, VoiceConfig cfg)
        {
            try
            {
                var template = cfg.Templates.ContainsKey("custom") ? cfg.Templates["custom"] : cfg.Templates.Values.First();
                var channelName = $"{template.Name} - {member.Username}";
                var categoryId = cfg.VoiceChannelCategory ?? cfg.JoinToCreateCategory;

                var newChannel = await guild.CreateVoiceChannelAsync(channelName, prop =>
                {
                    if (categoryId.HasValue) prop.CategoryId = categoryId.Value;
                    prop.UserLimit = template.Limit;
                    prop.PermissionOverwrites = new[] {
                        new Overwrite(member.Id, PermissionTarget.User, new OverwritePermissions(viewChannel: PermValue.Allow, connect: PermValue.Allow, speak: PermValue.Allow))
                    };
                });

                try { var igu = guild.GetUser(member.Id); if (igu != null) await igu.ModifyAsync(p => p.ChannelId = newChannel.Id); } catch { }

                cfg.ActiveChannels[newChannel.Id] = new ActiveChannelInfo { OwnerId = member.Id, CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Template = "custom" };
                SaveVoiceConfig();

                UpdateAfkTracker(newChannel.Id, member.Id);
                AddVoiceLog(member.Id, member.Username, "created", newChannel.Name);
                await SendToLogChannel(guild, cfg, $"🎤 **{member.Username}** created **{newChannel.Name}**");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error creating voice channel: " + ex);
            }
        }

        private static void UpdateAfkTracker(ulong channelId, ulong userId)
        {
            if (!_afkTracker.ContainsKey(channelId)) _afkTracker[channelId] = new();
            _afkTracker[channelId][userId] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        private static string StripLeadingEmojiPrefixes(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var prefixes = new[] { "✅", "❌", "🎤", "🗑️", "⏰", "🧹", "🎙️", "🔒", "🔓", "🎮", "📚", "💤", "⚠️", "🚨", "📣", "🐛", "❓" };
            var t = text.TrimStart();
            foreach (var p in prefixes)
            {
                if (t.StartsWith(p + " ") || t.StartsWith(p))
                {
                    t = t.Substring(p.Length).TrimStart();
                }
            }
            return t;
        }

        private static async Task SendToLogChannel(SocketGuild guild, VoiceConfig cfg, string message)
        {
            if (cfg == null || !cfg.VoiceLogChannel.HasValue) return;
            try
            {
                var logChan = guild.GetTextChannel(cfg.VoiceLogChannel.Value);
                if (logChan == null) return;

                var sanitized = StripLeadingEmojiPrefixes(message);
                var eb = new EmbedBuilder()
                    .WithDescription(sanitized)
                    .WithColor(new Color(0, 128, 128));

                await logChan.SendMessageAsync(embed: eb.Build());
            }
            catch (Exception ex) { Console.WriteLine("Error sending to log channel: " + ex); }
        }

        public static async Task StartBackgroundTasks(DiscordSocketClient client)
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    try { await CheckAfkUsers(client); } catch (Exception ex) { Console.WriteLine("AFK check error: " + ex); }
                    await Task.Delay(TimeSpan.FromMinutes(1));
                }
            });

            _ = Task.Run(async () =>
            {
                while (true)
                {
                    try { await AutoCleanupVoiceLogs(client); } catch (Exception ex) { Console.WriteLine("AutoCleanup error: " + ex); }
                    await Task.Delay(TimeSpan.FromHours(5));
                }
            });
        }

        private static async Task CheckAfkUsers(DiscordSocketClient client)
        {
            var cfg = _config;
            const long AFK_TIMEOUT = 10 * 60 * 1000; // 10 minutes

            foreach (var kv in _afkTracker.ToList())
            {
                var channelId = kv.Key;
                var users = kv.Value;
                try
                {
                    var guild = client.Guilds.FirstOrDefault();
                    var channel = guild?.GetVoiceChannel(channelId);
                    if (channel == null) { _afkTracker.Remove(channelId); continue; }

                    foreach (var u in users.ToList())
                    {
                        var userId = u.Key; var last = u.Value;
                        var member = channel.GetUser(userId);
                        if (member == null) { users.Remove(userId); continue; }
                        var timeSince = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - last;
                        if (timeSince >= AFK_TIMEOUT)
                        {
                            try { await member.ModifyAsync(x => x.ChannelId = null); } catch { }
                            users.Remove(userId);
                            await SendToLogChannel(channel.Guild, cfg, $"**{member.Username}** was kicked from **{channel.Name}** (AFK)");
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine("Error checking AFK: " + ex); }
            }
        }

        private static async Task AutoCleanupVoiceLogs(DiscordSocketClient client)
        {
            var cfg = _config;
            if (cfg == null || !cfg.VoiceLogChannel.HasValue) return;
            try
            {
                var guild = client.Guilds.FirstOrDefault();
                var logChan = guild?.GetTextChannel(cfg.VoiceLogChannel.Value);
                if (logChan == null) return;

                int deleted = 0;
                while (true)
                {
                    var messages = await logChan.GetMessagesAsync(100).FlattenAsync();
                    if (!messages.Any()) break;
                    foreach (var msg in messages)
                    {
                        try { await msg.DeleteAsync(); deleted++; } catch { }
                    }
                    if (messages.Count() < 100) break;
                }
                Console.WriteLine($"🧹 Auto-cleanup: Deleted {deleted} messages from voice log channel");
                try { await logChan.SendMessageAsync($"🧹 **Auto-Cleanup** - Voice logs cleared ({deleted} messages deleted)"); } catch { }
            }
            catch (Exception ex) { Console.WriteLine("Error in auto cleanup voice logs: " + ex); }
        }

        // admin helpers
        public static VoiceConfig GetConfig() => _config;
        public static void SetConfig(VoiceConfig cfg) { _config = cfg; SaveVoiceConfig(); }
        public static ActiveChannelInfo GetActiveChannelByOwner(ulong ownerId) => _config.ActiveChannels.Values.FirstOrDefault(a => a.OwnerId == ownerId);
        public static VoiceLogs GetLogs() => _logs;
        public static void RemoveActiveChannel(ulong channelId) { if (_config.ActiveChannels.ContainsKey(channelId)) { _config.ActiveChannels.Remove(channelId); SaveVoiceConfig(); } }
        public static void ResetConfig() { _config = new VoiceConfig(); SaveVoiceConfig(); }
        public static void AddOrUpdateTemplate(string key, string name, int limit) { _config.Templates[key] = new VoiceTemplate { Name = name, Limit = limit }; SaveVoiceConfig(); }
    }

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
}