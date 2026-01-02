using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace MainbotCSharp.Modules
{
    // Security Service Classes
    public class SecurityConfigEntry { public bool Enabled { get; set; } = false; public ulong? LogChannelId { get; set; } = null; }

    public class CleanupIntervalEntry { public ulong ChannelId { get; set; } public bool Enabled { get; set; } = true; }

    public static class SecurityService
    {
        private const string SECURITY_FILE = "security_config.json";
        private const string CLEANUP_INTERVALS_FILE = "cleanup_intervals.json";
        private static Dictionary<ulong, SecurityConfigEntry> _config = LoadSecurityConfig();
        private static Dictionary<ulong, CleanupIntervalEntry> _cleanupIntervals = LoadCleanupIntervals();
        private static Dictionary<ulong, System.Timers.Timer> _timers = new Dictionary<ulong, System.Timers.Timer>();

        private static readonly string[] WordLists = new[] {
            "anal","anus","arsch","boobs","clit","dick","fuck","fucking","hure","nackt","nudes","nipple","porn","pussy","sex","slut","tits","vagina",
            "bastard","idiot","dumm","retard","go die","kill yourself","kys","suicide","self harm","spam","discordgift","freenitro"
        };

        private static Dictionary<ulong, SecurityConfigEntry> LoadSecurityConfig()
        {
            try
            {
                if (!File.Exists(SECURITY_FILE)) return new Dictionary<ulong, SecurityConfigEntry>();
                var txt = File.ReadAllText(SECURITY_FILE);
                var d = JsonSerializer.Deserialize<Dictionary<ulong, SecurityConfigEntry>>(txt);
                return d ?? new Dictionary<ulong, SecurityConfigEntry>();
            }
            catch
            {
                return new Dictionary<ulong, SecurityConfigEntry>();
            }
        }

        private static Dictionary<ulong, CleanupIntervalEntry> LoadCleanupIntervals()
        {
            try
            {
                if (!File.Exists(CLEANUP_INTERVALS_FILE)) return new Dictionary<ulong, CleanupIntervalEntry>();
                var txt = File.ReadAllText(CLEANUP_INTERVALS_FILE);
                var d = JsonSerializer.Deserialize<Dictionary<ulong, CleanupIntervalEntry>>(txt);
                return d ?? new Dictionary<ulong, CleanupIntervalEntry>();
            }
            catch
            {
                return new Dictionary<ulong, CleanupIntervalEntry>();
            }
        }

        private static void SaveSecurityConfig()
        {
            try
            {
                var txt = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SECURITY_FILE, txt);
            }
            catch { }
        }

        private static void SaveCleanupIntervals()
        {
            try
            {
                var txt = JsonSerializer.Serialize(_cleanupIntervals, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(CLEANUP_INTERVALS_FILE, txt);
            }
            catch { }
        }

        public static void SetCleanupInterval(ulong guildId, ulong channelId, DiscordSocketClient client)
        {
            _cleanupIntervals[guildId] = new CleanupIntervalEntry { ChannelId = channelId };
            SaveCleanupIntervals();

            // Remove existing timer if any
            if (_timers.TryGetValue(guildId, out var existingTimer))
            {
                existingTimer.Stop();
                existingTimer.Dispose();
                _timers.Remove(guildId);
            }

            // Create new timer for 1 hour intervals
            var timer = new System.Timers.Timer(TimeSpan.FromHours(1).TotalMilliseconds);
            timer.Elapsed += async (sender, e) =>
            {
                try
                {
                    var guild = client.GetGuild(guildId);
                    var channel = guild?.GetTextChannel(channelId);
                    if (channel != null)
                    {
                        await PerformScheduledCleanup(channel);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Cleanup timer error: {ex.Message}");
                }
            };
            timer.Start();
            _timers[guildId] = timer;
        }

        public static void RemoveCleanupInterval(ulong guildId)
        {
            _cleanupIntervals.Remove(guildId);
            SaveCleanupIntervals();

            if (_timers.TryGetValue(guildId, out var timer))
            {
                timer.Stop();
                timer.Dispose();
                _timers.Remove(guildId);
            }
        }

        private static async Task PerformScheduledCleanup(SocketTextChannel channel)
        {
            try
            {
                bool hasMore = true;
                int totalDeleted = 0;

                while (hasMore)
                {
                    var messages = await channel.GetMessagesAsync(100).FlattenAsync();
                    // Exclude pinned messages
                    var deleteableMessages = messages.Where(x =>
                        DateTimeOffset.UtcNow - x.Timestamp < TimeSpan.FromDays(14) &&
                        !x.IsPinned).ToList();

                    if (!deleteableMessages.Any())
                    {
                        hasMore = false;
                        break;
                    }

                    if (deleteableMessages.Count == 1)
                    {
                        await deleteableMessages.First().DeleteAsync();
                        totalDeleted += 1;
                        hasMore = false;
                    }
                    else
                    {
                        await channel.DeleteMessagesAsync(deleteableMessages);
                        totalDeleted += deleteableMessages.Count;
                    }

                    await Task.Delay(1000); // Rate limit protection
                }

                // Send cleanup notification
                if (totalDeleted > 0)
                {
                    var notification = await channel.SendMessageAsync($"🕐 Scheduled cleanup completed! {totalDeleted} messages were deleted :)) Thank you for using me as your cleaning lady!");

                    // Delete notification after 45 seconds
                    _ = Task.Delay(45000).ContinueWith(async _ =>
                    {
                        try { await notification.DeleteAsync(); } catch { }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Scheduled cleanup error: {ex.Message}");
            }
        }

        public static CleanupIntervalEntry GetCleanupInterval(ulong guildId)
        {
            return _cleanupIntervals.TryGetValue(guildId, out var entry) ? entry : null;
        }

        public static void SetConfig(ulong guildId, SecurityConfigEntry entry)
        {
            _config[guildId] = entry; SaveSecurityConfig();
        }

        public static SecurityConfigEntry GetConfig(ulong guildId)
        {
            if (_config.TryGetValue(guildId, out var e)) return e; return new SecurityConfigEntry();
        }

        public static async Task HandleMessageAsync(SocketMessage rawMessage)
        {
            try
            {
                if (!(rawMessage is SocketUserMessage message)) return;
                if (message.Author.IsBot) return;
                if (!(message.Channel is SocketTextChannel tchan)) return;
                var guild = tchan.Guild;
                if (guild == null) return;

                var cfg = GetConfig(guild.Id);
                if (!cfg.Enabled) return;

                var content = (message.Content ?? string.Empty).ToLowerInvariant();
                var guildUser = message.Author as SocketGuildUser;
                if (guildUser != null && (guildUser.GuildPermissions.Administrator)) return;

                var inviteRegex = new Regex(@"(discord\.gg\/|discordapp\.com\/invite\/|discord\.com\/invite\/)", RegexOptions.IgnoreCase);
                if (inviteRegex.IsMatch(content)) { await ReportAndDelete(message, guild, "Invite link", "invite link"); return; }

                if (Regex.IsMatch(content, @"([a-zA-Z0-9])\1{6,}") || Regex.IsMatch(content, @"(.)\s*\1{6,}")) { await ReportAndDelete(message, guild, "Spam detected", "spam"); return; }

                foreach (var w in WordLists)
                {
                    if (content.Contains(w)) { await ReportAndDelete(message, guild, $"Inappropriate language: {w}", w); return; }
                }

                if (message.Attachments != null && message.Attachments.Count > 0)
                {
                    foreach (var att in message.Attachments)
                    {
                        var name = (att.Filename ?? "").ToLowerInvariant();
                        if (Regex.IsMatch(name, "(nude|nudes|porn|dick|boobs|sex|pussy|tits|vagina|penis|clit|anal|nsfw|xxx|18\\+)"))
                        {
                            await ReportAndDelete(message, guild, "NSFW attachment", name);
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("SecurityService error: " + ex);
            }
        }

        private static async Task ReportAndDelete(SocketUserMessage message, SocketGuild guild, string reason, string matched)
        {
            try
            {
                var cfg = GetConfig(guild.Id);
                if (cfg.LogChannelId.HasValue)
                {
                    var chId = cfg.LogChannelId.Value;
                    var ch = guild.GetTextChannel(chId);
                    if (ch != null)
                    {
                        var eb = new EmbedBuilder().WithTitle("Security Alert").WithColor(Color.Orange)
                            .AddField("User", $"{message.Author} ({message.Author.Id})", true)
                            .AddField("Action", reason, true)
                            .AddField("Matched", matched ?? (message.Content ?? "—"), false)
                            .WithTimestamp(DateTimeOffset.UtcNow);
                        try { await ch.SendMessageAsync(text: $"Security event in {guild.Name} ({guild.Id})", embed: eb.Build()); } catch { }
                    }
                }

                try { await message.DeleteAsync(); } catch { }
                try { await message.Author.SendMessageAsync($"You have been flagged for: {reason}. If you think this is a mistake, contact the server staff."); } catch { }

                try
                {
                    var log = new
                    {
                        time = DateTimeOffset.UtcNow,
                        guildId = guild.Id,
                        guildName = guild.Name,
                        userId = message.Author.Id,
                        userTag = message.Author.Username,
                        action = reason,
                        matched = matched,
                        content = message.Content
                    };
                    File.AppendAllText("security_logs_main.jsonl", JsonSerializer.Serialize(log) + "\n");
                }
                catch { }
            }
            catch { }
        }
    }

    [Group("security")]
    public class SecurityCommands : ModuleBase<SocketCommandContext>
    {
        [Command("setup")]
        [Summary("Setup security system (Admin only)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SecuritySetupAsync()
        {
            try
            {
                var config = new SecurityConfigEntry
                {
                    Enabled = true,
                    LogChannelId = Context.Channel.Id
                };

                SecurityService.SetConfig(Context.Guild.Id, config);

                var embed = new EmbedBuilder()
                    .WithTitle("🛡️ Security System Enabled")
                    .WithColor(Color.Green)
                    .WithDescription("Security monitoring is now active!")
                    .AddField("Log Channel", $"<#{Context.Channel.Id}>", true)
                    .AddField("Features", "• Invite link detection\n• Spam detection\n• NSFW content filter\n• Inappropriate language filter", false);

                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Setup failed: {ex.Message}");
            }
        }

        [Command("disable")]
        [Summary("Disable security system (Admin only)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SecurityDisableAsync()
        {
            try
            {
                var config = new SecurityConfigEntry { Enabled = false };
                SecurityService.SetConfig(Context.Guild.Id, config);
                await ReplyAsync("🛡️ Security system disabled.");
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Failed to disable: {ex.Message}");
            }
        }

        [Command("status")]
        [Summary("Check security system status (Admin only)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SecurityStatusAsync()
        {
            try
            {
                var config = SecurityService.GetConfig(Context.Guild.Id);
                var embed = new EmbedBuilder()
                    .WithTitle("🛡️ Security Status")
                    .WithColor(config.Enabled ? Color.Green : Color.Red)
                    .AddField("Status", config.Enabled ? "✅ Enabled" : "❌ Disabled", true)
                    .AddField("Log Channel", config.LogChannelId.HasValue ? $"<#{config.LogChannelId}>" : "Not set", true);

                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Failed to get status: {ex.Message}");
            }
        }

        [Command("kick")]
        [Summary("Kick a user from the server")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        [RequireBotPermission(GuildPermission.KickMembers)]
        public async Task KickAsync(SocketGuildUser user, [Remainder] string reason = "No reason provided")
        {
            try
            {
                if (user.Id == Context.User.Id)
                {
                    await ReplyAsync("❌ You cannot kick yourself!");
                    return;
                }

                if (user.Hierarchy >= (Context.User as SocketGuildUser)?.Hierarchy)
                {
                    await ReplyAsync("❌ You cannot kick users with equal or higher roles!");
                    return;
                }

                await user.KickAsync(reason);

                var embed = new EmbedBuilder()
                    .WithTitle("👢 User Kicked")
                    .WithColor(0x40E0D0)
                    .AddField("User", $"{user.Username}#{user.Discriminator}", true)
                    .AddField("Moderator", Context.User.Username, true)
                    .AddField("Reason", reason, false)
                    .WithTimestamp(DateTimeOffset.UtcNow);

                await ReplyAsync(embed: embed.Build());

                try
                {
                    await user.SendMessageAsync($"You have been kicked from {Context.Guild.Name}. Reason: {reason}");
                }
                catch { /* User has DMs disabled */ }
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Failed to kick user: {ex.Message}");
            }
        }

        [Command("ban")]
        [Summary("Ban a user from the server")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        [RequireBotPermission(GuildPermission.BanMembers)]
        public async Task BanAsync(SocketGuildUser user, int days = 0, [Remainder] string reason = "No reason provided")
        {
            try
            {
                if (user.Id == Context.User.Id)
                {
                    await ReplyAsync("❌ You cannot ban yourself!");
                    return;
                }

                if (user.Hierarchy >= (Context.User as SocketGuildUser)?.Hierarchy)
                {
                    await ReplyAsync("❌ You cannot ban users with equal or higher roles!");
                    return;
                }

                await user.BanAsync(days, reason);

                var embed = new EmbedBuilder()
                    .WithTitle("🔨 User Banned")
                    .WithColor(Color.Red)
                    .AddField("User", $"{user.Username}#{user.Discriminator}", true)
                    .AddField("Moderator", Context.User.Username, true)
                    .AddField("Reason", reason, false)
                    .AddField("Days Deleted", days.ToString(), true)
                    .WithTimestamp(DateTimeOffset.UtcNow);

                await ReplyAsync(embed: embed.Build());

                try
                {
                    await user.SendMessageAsync($"You have been banned from {Context.Guild.Name}. Reason: {reason}");
                }
                catch { /* User has DMs disabled */ }
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Failed to ban user: {ex.Message}");
            }
        }

        [Command("timeout")]
        [Summary("Timeout a user for specified minutes")]
        [RequireUserPermission(GuildPermission.ModerateMembers)]
        [RequireBotPermission(GuildPermission.ModerateMembers)]
        public async Task TimeoutAsync(SocketGuildUser user, int minutes, [Remainder] string reason = "No reason provided")
        {
            try
            {
                if (user.Id == Context.User.Id)
                {
                    await ReplyAsync("❌ You cannot timeout yourself!");
                    return;
                }

                if (user.Hierarchy >= (Context.User as SocketGuildUser)?.Hierarchy)
                {
                    await ReplyAsync("❌ You cannot timeout users with equal or higher roles!");
                    return;
                }

                if (minutes <= 0 || minutes > 40320) // Max 28 days
                {
                    await ReplyAsync("❌ Timeout must be between 1 and 40320 minutes (28 days)!");
                    return;
                }

                var until = DateTimeOffset.UtcNow.AddMinutes(minutes);
                await user.SetTimeOutAsync(until, new RequestOptions { AuditLogReason = reason });

                var embed = new EmbedBuilder()
                    .WithTitle("⏰ User Timed Out")
                    .WithColor(Color.Orange)
                    .AddField("User", $"{user.Username}#{user.Discriminator}", true)
                    .AddField("Moderator", Context.User.Username, true)
                    .AddField("Duration", $"{minutes} minutes", true)
                    .AddField("Until", $"<t:{until.ToUnixTimeSeconds()}:F>", true)
                    .AddField("Reason", reason, false)
                    .WithTimestamp(DateTimeOffset.UtcNow);

                await ReplyAsync(embed: embed.Build());

                try
                {
                    await user.SendMessageAsync($"You have been timed out in {Context.Guild.Name} for {minutes} minutes. Reason: {reason}");
                }
                catch { /* User has DMs disabled */ }
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Failed to timeout user: {ex.Message}");
            }
        }

        [Command("cleanup")]
        [Summary("Cleanup/delete messages in current channel")]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        public async Task CleanupAsync(int? amount = null)
        {
            try
            {
                int totalDeleted = 0;

                if (amount == null)
                {
                    // No amount specified = clear ALL messages (except pinned)
                    var textChannel = Context.Channel as SocketTextChannel;
                    if (textChannel == null) return;

                    // Delete command message first
                    try { await Context.Message.DeleteAsync(); } catch { }

                    bool hasMore = true;
                    while (hasMore)
                    {
                        var messages = await textChannel.GetMessagesAsync(100).FlattenAsync();
                        // Exclude pinned messages
                        var deleteableMessages = messages.Where(x =>
                            DateTimeOffset.UtcNow - x.Timestamp < TimeSpan.FromDays(14) &&
                            !x.IsPinned).ToList();

                        if (!deleteableMessages.Any())
                        {
                            hasMore = false;
                            break;
                        }

                        if (deleteableMessages.Count == 1)
                        {
                            await deleteableMessages.First().DeleteAsync();
                            totalDeleted += 1;
                            hasMore = false;
                        }
                        else
                        {
                            await textChannel.DeleteMessagesAsync(deleteableMessages);
                            totalDeleted += deleteableMessages.Count;
                        }

                        // Small delay to avoid rate limits
                        await Task.Delay(1000);
                    }
                }
                else
                {
                    // Specific amount specified
                    if (amount <= 0)
                    {
                        await ReplyAsync("❌ Amount must be greater than 0!");
                        return;
                    }

                    var messages = await Context.Channel.GetMessagesAsync(amount.Value + 1).FlattenAsync(); // +1 to include command
                    // Exclude pinned messages
                    var deleteableMessages = messages.Where(x =>
                        DateTimeOffset.UtcNow - x.Timestamp < TimeSpan.FromDays(14) &&
                        !x.IsPinned);

                    if (Context.Channel is SocketTextChannel textChannel)
                    {
                        await textChannel.DeleteMessagesAsync(deleteableMessages);
                        totalDeleted = deleteableMessages.Count() - 1; // -1 because command message is included
                    }
                }

                // Send funny cleanup message
                var reply = await ReplyAsync($"{totalDeleted} messages were deleted :)) Thank you for using me as your cleaning lady!");

                // Delete confirmation message after 45 seconds
                _ = Task.Delay(45000).ContinueWith(async _ =>
                {
                    try { await reply.DeleteAsync(); } catch { }
                });
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Failed to cleanup messages: {ex.Message}");
            }
        }

        [Command("setcleanupinterval")]
        [Summary("Set automatic cleanup interval for a channel (1 hour)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetCleanupIntervalAsync()
        {
            try
            {
                await ReplyAsync("In which channel should the interval be set? (provide the channel ID)");

                var response = await NextMessageAsync(timeout: TimeSpan.FromMinutes(1));

                if (response == null)
                {
                    await ReplyAsync("❌ Timeout! Please try again.");
                    return;
                }

                if (!ulong.TryParse(response.Content.Trim(), out ulong channelId))
                {
                    await ReplyAsync("❌ Invalid channel ID! Please provide a valid numeric ID.");
                    return;
                }

                var channel = Context.Guild.GetTextChannel(channelId);
                if (channel == null)
                {
                    await ReplyAsync("❌ Channel not found! Make sure the channel ID is correct and the bot has access to it.");
                    return;
                }

                // Set the cleanup interval
                SecurityService.SetCleanupInterval(Context.Guild.Id, channelId, Context.Client as DiscordSocketClient);

                var embed = new EmbedBuilder()
                    .WithTitle("⏰ Cleanup Interval Set!")
                    .WithColor(0x40E0D0)
                    .AddField("Channel", $"<#{channelId}>", true)
                    .AddField("Interval", "1 hour", true)
                    .AddField("Status", "✅ Active", true)
                    .WithDescription("The channel will be automatically cleaned every hour (excluding pinned messages)!")
                    .WithTimestamp(DateTimeOffset.UtcNow);

                await ReplyAsync(embed: embed.Build());

                // Delete user's channel ID message for privacy
                try { await response.DeleteAsync(); } catch { }
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Failed to set cleanup interval: {ex.Message}");
            }
        }

        [Command("removecleanupinterval")]
        [Summary("Remove automatic cleanup interval")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task RemoveCleanupIntervalAsync()
        {
            try
            {
                var currentInterval = SecurityService.GetCleanupInterval(Context.Guild.Id);
                if (currentInterval == null)
                {
                    await ReplyAsync("❌ No cleanup interval is currently set for this server.");
                    return;
                }

                SecurityService.RemoveCleanupInterval(Context.Guild.Id);

                var embed = new EmbedBuilder()
                    .WithTitle("⏰ Cleanup Interval Removed!")
                    .WithColor(Color.Orange)
                    .AddField("Status", "❌ Disabled", true)
                    .WithDescription("Automatic cleanup has been disabled for this server.")
                    .WithTimestamp(DateTimeOffset.UtcNow);

                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Failed to remove cleanup interval: {ex.Message}");
            }
        }

        [Command("warn")]
        [Summary("Warn a user")]
        [RequireUserPermission(GuildPermission.ModerateMembers)]
        public async Task WarnAsync(SocketGuildUser user, [Remainder] string reason = "No reason provided")
        {
            try
            {
                if (user.Id == Context.User.Id)
                {
                    await ReplyAsync("❌ You cannot warn yourself!");
                    return;
                }

                var embed = new EmbedBuilder()
                    .WithTitle("⚠️ User Warned")
                    .WithColor(Color.Gold)
                    .AddField("User", $"{user.Username}#{user.Discriminator}", true)
                    .AddField("Moderator", Context.User.Username, true)
                    .AddField("Reason", reason, false)
                    .WithTimestamp(DateTimeOffset.UtcNow);

                await ReplyAsync(embed: embed.Build());

                try
                {
                    await user.SendMessageAsync($"You have been warned in {Context.Guild.Name}. Reason: {reason}");
                }
                catch { /* User has DMs disabled */ }

                // Log warning
                try
                {
                    var log = new
                    {
                        time = DateTimeOffset.UtcNow,
                        type = "warning",
                        guildId = Context.Guild.Id,
                        guildName = Context.Guild.Name,
                        userId = user.Id,
                        userTag = $"{user.Username}#{user.Discriminator}",
                        moderatorId = Context.User.Id,
                        moderatorTag = Context.User.Username,
                        reason = reason
                    };
                    File.AppendAllText("warnings.jsonl", JsonSerializer.Serialize(log) + "\n");
                }
                catch { }
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Failed to warn user: {ex.Message}");
            }
        }

        [Command("unban")]
        [Summary("Unban a user by ID")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        [RequireBotPermission(GuildPermission.BanMembers)]
        public async Task UnbanAsync(ulong userId, [Remainder] string reason = "No reason provided")
        {
            try
            {
                var ban = await Context.Guild.GetBanAsync(userId);
                if (ban == null)
                {
                    await ReplyAsync("❌ User is not banned!");
                    return;
                }

                await Context.Guild.RemoveBanAsync(userId, new RequestOptions { AuditLogReason = reason });

                var embed = new EmbedBuilder()
                    .WithTitle("✅ User Unbanned")
                    .WithColor(Color.Green)
                    .AddField("User", $"{ban.User.Username}#{ban.User.Discriminator}", true)
                    .AddField("Moderator", Context.User.Username, true)
                    .AddField("Reason", reason, false)
                    .WithTimestamp(DateTimeOffset.UtcNow);

                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Failed to unban user: {ex.Message}");
            }
        }
    }
}
