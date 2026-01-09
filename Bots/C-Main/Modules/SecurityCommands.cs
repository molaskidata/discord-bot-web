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

        public static async Task HandleUserJoinedAsync(SocketGuildUser user)
        {
            try
            {
                var cfg = GetConfig(user.Guild.Id);
                if (!cfg.Enabled) return;

                var suspiciousReasons = new List<string>();

                // Check account age (less than 20 days)
                var accountAge = DateTimeOffset.UtcNow - user.CreatedAt;
                if (accountAge.TotalDays < 20)
                {
                    suspiciousReasons.Add($"Account age: {accountAge.TotalDays:F0} days (< 20 days)");
                }

                // Check suspicious username patterns
                var username = user.Username.ToLowerInvariant();
                var suspiciousUsernamePatterns = new[]
                {
                    @"discord\.gg", @"bit\.ly", @"tinyurl", @"shorturl", // Links
                    @"admin\d+", @"moderator\d+", @"staff\d+", // Fake staff
                    @"[0-9]{8,}", // Long numbers only
                    @"^[a-z]{1,3}[0-9]{4,}$", // Short letters + many numbers
                    @"free.*nitro", @"nitro.*free", @"giveaway", // Common scams
                    @"crypto", @"bitcoin", @"trade", @"invest", // Crypto scams
                    @"xxx", @"porn", @"sex", @"nude", // NSFW usernames
                };

                foreach (var pattern in suspiciousUsernamePatterns)
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(username, pattern))
                    {
                        suspiciousReasons.Add($"Suspicious username pattern: {pattern}");
                        break;
                    }
                }

                // Check suspicious profile picture
                var avatarUrl = user.GetAvatarUrl(size: 256);
                if (avatarUrl == null)
                {
                    suspiciousReasons.Add("No profile picture (default Discord avatar)");
                }
                else
                {
                    // Check for suspicious avatar patterns
                    var suspiciousAvatarPatterns = new[]
                    {
                        @"discord\.com", // Discord CDN (might be stolen)
                        @"anime", @"waifu", // Common bot avatars
                        @"generic", @"placeholder" // Generic images
                    };

                    foreach (var pattern in suspiciousAvatarPatterns)
                    {
                        if (System.Text.RegularExpressions.Regex.IsMatch(avatarUrl, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        {
                            suspiciousReasons.Add($"Suspicious avatar: {pattern} detected");
                            break;
                        }
                    }
                }

                // Check if user has suspicious discriminator patterns
                if (user.Discriminator != null)
                {
                    var discriminator = user.Discriminator;
                    if (discriminator.All(c => c == discriminator[0])) // All same digits like #0000, #1111
                    {
                        suspiciousReasons.Add($"Suspicious discriminator: #{discriminator}");
                    }
                }

                // If suspicious, log the user
                if (suspiciousReasons.Any())
                {
                    await LogSuspiciousUser(user, suspiciousReasons);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HandleUserJoined error: {ex.Message}");
            }
        }

        private static async Task LogSuspiciousUser(SocketGuildUser user, List<string> reasons)
        {
            try
            {
                var cfg = GetConfig(user.Guild.Id);
                if (!cfg.LogChannelId.HasValue) return;

                var logChannel = user.Guild.GetTextChannel(cfg.LogChannelId.Value);
                if (logChannel == null) return;

                var embed = new EmbedBuilder()
                    .WithTitle("🚨 Suspicious User Joined")
                    .WithColor(Color.Orange)
                    .WithThumbnailUrl(user.GetAvatarUrl(size: 256) ?? user.GetDefaultAvatarUrl())
                    .AddField("User", $"{user.Mention}\n`{user.Username}#{user.Discriminator}`\n`{user.Id}`", true)
                    .AddField("Account Created", $"<t:{user.CreatedAt.ToUnixTimeSeconds()}:F>\n<t:{user.CreatedAt.ToUnixTimeSeconds()}:R>", true)
                    .AddField("Joined Server", $"<t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:F>\n<t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:R>", true)
                    .AddField("Suspicious Indicators", string.Join("\n• ", reasons.Select(r => $"• {r}")), false)
                    .WithFooter($"User ID: {user.Id} • Keep an eye on this user")
                    .WithTimestamp(DateTimeOffset.UtcNow);

                // Add action buttons for staff
                var components = new ComponentBuilder()
                    .WithButton("🔨 Ban", $"security_ban_{user.Id}", ButtonStyle.Danger)
                    .WithButton("👢 Kick", $"security_kick_{user.Id}", ButtonStyle.Secondary)
                    .WithButton("👁️ Watch", $"security_watch_{user.Id}", ButtonStyle.Primary)
                    .WithButton("✅ Dismiss", $"security_dismiss_{user.Id}", ButtonStyle.Success);

                await logChannel.SendMessageAsync(embed: embed.Build(), components: components.Build());

                // Log to file for permanent record
                try
                {
                    var logEntry = new
                    {
                        timestamp = DateTimeOffset.UtcNow,
                        type = "suspicious_join",
                        guildId = user.Guild.Id,
                        guildName = user.Guild.Name,
                        userId = user.Id,
                        username = $"{user.Username}#{user.Discriminator}",
                        accountAge = (DateTimeOffset.UtcNow - user.CreatedAt).TotalDays,
                        reasons = reasons,
                        avatarUrl = user.GetAvatarUrl() ?? "none"
                    };
                    File.AppendAllText("suspicious_users.jsonl", JsonSerializer.Serialize(logEntry) + "\n");
                }
                catch { }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LogSuspiciousUser error: {ex.Message}");
            }
        }

        public static async Task HandleSuspiciousUserAction(SocketMessageComponent component)
        {
            try
            {
                if (!component.Data.CustomId.StartsWith("security_")) return;

                var parts = component.Data.CustomId.Split('_');
                if (parts.Length != 3) return;

                var action = parts[1];
                if (!ulong.TryParse(parts[2], out var userId)) return;

                var guild = (component.User as SocketGuildUser)?.Guild;
                if (guild == null) return;

                var user = guild.GetUser(userId);
                if (user == null)
                {
                    await component.RespondAsync("❌ User not found or already left the server.", ephemeral: true);
                    return;
                }

                var moderator = component.User as SocketGuildUser;
                if (moderator == null) return;

                switch (action)
                {
                    case "ban":
                        if (moderator.GuildPermissions.BanMembers)
                        {
                            await user.BanAsync(0, "Suspicious user - banned by security system");
                            await component.RespondAsync($"🔨 **{user.Username}#{user.Discriminator}** has been banned.", ephemeral: true);
                        }
                        else
                        {
                            await component.RespondAsync("❌ You don't have permission to ban users.", ephemeral: true);
                        }
                        break;

                    case "kick":
                        if (moderator.GuildPermissions.KickMembers)
                        {
                            await user.KickAsync("Suspicious user - kicked by security system");
                            await component.RespondAsync($"👢 **{user.Username}#{user.Discriminator}** has been kicked.", ephemeral: true);
                        }
                        else
                        {
                            await component.RespondAsync("❌ You don't have permission to kick users.", ephemeral: true);
                        }
                        break;

                    case "watch":
                        await component.RespondAsync($"👁️ **{user.Username}#{user.Discriminator}** is now being watched. Monitor their activity.", ephemeral: true);
                        break;

                    case "dismiss":
                        await component.RespondAsync($"✅ Alert for **{user.Username}#{user.Discriminator}** has been dismissed.", ephemeral: true);
                        break;
                }

                // Update the original message to show action taken
                var embed = component.Message.Embeds.FirstOrDefault();
                if (embed != null)
                {
                    var newEmbed = new EmbedBuilder()
                        .WithTitle(embed.Title)
                        .WithDescription(embed.Description)
                        .WithColor(action == "dismiss" ? Color.Green : Color.Red)
                        .WithThumbnailUrl(embed.Thumbnail?.Url)
                        .WithFooter($"Action taken: {action.ToUpper()} by {moderator.Username} • {embed.Footer?.Text ?? ""}")
                        .WithTimestamp(embed.Timestamp ?? DateTimeOffset.UtcNow);

                    foreach (var field in embed.Fields)
                    {
                        newEmbed.AddField(field.Name, field.Value, field.Inline);
                    }

                    await component.Message.ModifyAsync(x =>
                    {
                        x.Embed = newEmbed.Build();
                        x.Components = new ComponentBuilder().Build(); // Remove buttons
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HandleSuspiciousUserAction error: {ex.Message}");
            }
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

    public class SecurityCommands : ModuleBase<SocketCommandContext>
    {
        [Command("setsecuritymod")]
        [Summary("Setup security system (Admin only)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SecuritySetupAsync()
        {
            try
            {
                // Step 1: Ask for security log channel
                var askEmbed = new EmbedBuilder()
                    .WithTitle("🛡️ Security System Setup")
                    .WithDescription("What channel should be the security log channel?\n\nWrite the **Channel ID** only in the chat or type `!new-securechan` and I will create a security channel for you.")
                    .WithColor(0x40E0D0)
                    .Build();
                await ReplyAsync(embed: askEmbed);

                var channelResponse = await NextMessageAsync(TimeSpan.FromMinutes(1));
                if (channelResponse == null)
                {
                    var timeoutEmbed = new EmbedBuilder()
                        .WithTitle("⏰ Timeout")
                        .WithDescription("❌ Timeout! Please try again.")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: timeoutEmbed);
                    return;
                }

                ulong logChannelId;
                ITextChannel logChannel;

                if (channelResponse.Content.Trim() == "!new-securechan")
                {
                    // Ask for category
                    var categoryAskEmbed = new EmbedBuilder()
                        .WithTitle("📁 Category Selection")
                        .WithDescription("In which category should the channel be created?\n\nWrite the **Category ID** only.")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: categoryAskEmbed);

                    var categoryResponse = await NextMessageAsync(TimeSpan.FromMinutes(1));
                    if (categoryResponse == null)
                    {
                        var timeoutEmbed = new EmbedBuilder()
                            .WithTitle("⏰ Timeout")
                            .WithDescription("❌ Timeout! Please try again.")
                            .WithColor(0x40E0D0)
                            .Build();
                        await ReplyAsync(embed: timeoutEmbed);
                        return;
                    }

                    if (!ulong.TryParse(categoryResponse.Content.Trim(), out var categoryId))
                    {
                        var invalidEmbed = new EmbedBuilder()
                            .WithTitle("❌ Invalid Input")
                            .WithDescription("Invalid Category ID! Please provide a valid Category ID.")
                            .WithColor(0x40E0D0)
                            .Build();
                        await ReplyAsync(embed: invalidEmbed);
                        return;
                    }

                    var category = Context.Guild.GetCategoryChannel(categoryId);
                    if (category == null)
                    {
                        var notFoundEmbed = new EmbedBuilder()
                            .WithTitle("❌ Not Found")
                            .WithDescription("Category not found! Please provide a valid Category ID from this server.")
                            .WithColor(0x40E0D0)
                            .Build();
                        await ReplyAsync(embed: notFoundEmbed);
                        return;
                    }

                    // Create new security channel
                    logChannel = await Context.Guild.CreateTextChannelAsync("★-security-log", properties =>
                    {
                        properties.CategoryId = categoryId;
                    });
                    logChannelId = logChannel.Id;
                    var createdEmbed = new EmbedBuilder()
                        .WithTitle("✅ Channel Created")
                        .WithDescription($"Security channel created: {logChannel.Mention}\nThe channel was set in category: **{category.Name}**")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: createdEmbed);
                }
                else if (ulong.TryParse(channelResponse.Content.Trim(), out logChannelId))
                {
                    // Use existing channel
                    logChannel = Context.Guild.GetTextChannel(logChannelId);
                    if (logChannel == null)
                    {
                        var notFoundEmbed = new EmbedBuilder()
                            .WithTitle("❌ Not Found")
                            .WithDescription("Channel not found! Please provide a valid Channel ID from this server.")
                            .WithColor(0x40E0D0)
                            .Build();
                        await ReplyAsync(embed: notFoundEmbed);
                        return;
                    }
                    var setEmbed = new EmbedBuilder()
                        .WithTitle("✅ Channel Set")
                        .WithDescription($"Security log channel set: {logChannel.Mention}")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: setEmbed);
                }
                else
                {
                    var invalidEmbed = new EmbedBuilder()
                        .WithTitle("❌ Invalid Input")
                        .WithDescription("Invalid input! Please provide a Channel ID or type `!new-securechan`.")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: invalidEmbed);
                    return;
                }

                // Enable security
                var config = new SecurityConfigEntry
                {
                    Enabled = true,
                    LogChannelId = logChannelId
                };

                SecurityService.SetConfig(Context.Guild.Id, config);

                // Send confirmation embed
                var embed = new EmbedBuilder()
                    .WithTitle("🛡️ Security System Enabled!")
                    .WithColor(Color.Green)
                    .WithDescription("Security monitoring is now active for your server!")
                    .AddField("Log Channel", $"<#{logChannelId}>", true)
                    .AddField("Status", "✅ Active", true)
                    .AddField("Features",
                        "• Invite link detection\n" +
                        "• Spam detection\n" +
                        "• NSFW content filter\n" +
                        "• Inappropriate language filter\n" +
                        "• Suspicious user detection\n" +
                        "• Account age verification (< 20 days)\n" +
                        "• Username pattern analysis\n" +
                        "• Profile picture checks", false)
                    .WithFooter("Security system is now protecting your server!")
                    .WithTimestamp(DateTimeOffset.UtcNow);

                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("❌ Setup Failed")
                    .WithDescription($"Setup failed: {ex.Message}")
                    .WithColor(0x40E0D0)
                    .Build();
                await ReplyAsync(embed: errorEmbed);
            }
        }

        private async Task<SocketMessage> NextMessageAsync(TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<SocketMessage>();

            Task Handler(SocketMessage message)
            {
                if (message.Channel.Id == Context.Channel.Id && message.Author.Id == Context.User.Id && !message.Author.IsBot)
                {
                    tcs.SetResult(message);
                }
                return Task.CompletedTask;
            }

            Context.Client.MessageReceived += Handler;

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
            Context.Client.MessageReceived -= Handler;

            if (completedTask == tcs.Task)
            {
                return await tcs.Task;
            }

            return null;
        }

        [Command("suspicious-check")]
        [Summary("Check a specific user for suspicious indicators (Staff only)")]
        [RequireUserPermission(GuildPermission.ModerateMembers)]
        public async Task SuspiciousCheckAsync(SocketGuildUser user)
        {
            try
            {
                var suspiciousReasons = new List<string>();

                // Check account age
                var accountAge = DateTimeOffset.UtcNow - user.CreatedAt;
                if (accountAge.TotalDays < 20)
                {
                    suspiciousReasons.Add($"Account age: {accountAge.TotalDays:F0} days (< 20 days)");
                }

                // Check username patterns  
                var username = user.Username.ToLowerInvariant();
                var suspiciousPatterns = new[]
                {
                    @"discord\.gg", @"bit\.ly", @"tinyurl", @"shorturl",
                    @"admin\d+", @"moderator\d+", @"staff\d+",
                    @"[0-9]{8,}", @"^[a-z]{1,3}[0-9]{4,}$",
                    @"free.*nitro", @"nitro.*free", @"giveaway",
                    @"crypto", @"bitcoin", @"trade", @"invest"
                };

                foreach (var pattern in suspiciousPatterns)
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(username, pattern))
                    {
                        suspiciousReasons.Add($"Username matches pattern: {pattern}");
                    }
                }

                // Check profile picture
                var avatarUrl = user.GetAvatarUrl(size: 256);
                if (avatarUrl == null)
                {
                    suspiciousReasons.Add("Using default Discord avatar");
                }

                var color = suspiciousReasons.Any() ? Color.Orange : Color.Green;
                var status = suspiciousReasons.Any() ? "🚨 SUSPICIOUS" : "✅ Clean";

                var embed = new EmbedBuilder()
                    .WithTitle($"Security Check: {user.Username}")
                    .WithColor(color)
                    .WithThumbnailUrl(user.GetAvatarUrl(size: 128) ?? user.GetDefaultAvatarUrl())
                    .AddField("Status", status, true)
                    .AddField("Account Age", $"{accountAge.TotalDays:F0} days", true)
                    .AddField("Created", $"<t:{user.CreatedAt.ToUnixTimeSeconds()}:F>", true)
                    .AddField("Joined Server", user.JoinedAt.HasValue ? $"<t:{user.JoinedAt.Value.ToUnixTimeSeconds()}:F>" : "Unknown", true)
                    .AddField("User ID", user.Id.ToString(), true)
                    .AddField("Avatar", avatarUrl != null ? "Custom" : "Default", true);

                if (suspiciousReasons.Any())
                {
                    embed.AddField("🚨 Suspicious Indicators", string.Join("\n", suspiciousReasons.Select(r => $"• {r}")), false);
                }

                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Failed to check user: {ex.Message}");
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
                    var errorEmbed = new EmbedBuilder()
                        .WithTitle("❌ Cannot Kick")
                        .WithDescription("You cannot kick yourself!")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: errorEmbed);
                    return;
                }

                if (user.Hierarchy >= (Context.User as SocketGuildUser)?.Hierarchy)
                {
                    var hierarchyEmbed = new EmbedBuilder()
                        .WithTitle("❌ Cannot Kick")
                        .WithDescription("You cannot kick users with equal or higher roles!")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: hierarchyEmbed);
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
                    var errorEmbed = new EmbedBuilder()
                        .WithTitle("❌ Cannot Ban")
                        .WithDescription("You cannot ban yourself!")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: errorEmbed);
                    return;
                }

                if (user.Hierarchy >= (Context.User as SocketGuildUser)?.Hierarchy)
                {
                    var hierarchyEmbed = new EmbedBuilder()
                        .WithTitle("❌ Cannot Ban")
                        .WithDescription("You cannot ban users with equal or higher roles!")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: hierarchyEmbed);
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
                await user.SetTimeOutAsync(TimeSpan.FromMinutes(minutes));

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
        [Summary("Delete a specific number of messages in current channel")]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        public async Task CleanupAsync(int amount)
        {
            try
            {
                if (amount <= 0 || amount > 1000)
                {
                    var errorEmbed = new EmbedBuilder()
                        .WithTitle("❌ Invalid Amount")
                        .WithDescription("Please provide a number between 1 and 1000.")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: errorEmbed);
                    return;
                }

                var deletingEmbed = new EmbedBuilder()
                    .WithTitle("🧹 Cleaning Messages")
                    .WithDescription($"Deleting {amount} messages... Please wait.")
                    .WithColor(0x40E0D0)
                    .Build();
                await ReplyAsync(embed: deletingEmbed);

                var messages = await Context.Channel.GetMessagesAsync(amount + 1).FlattenAsync(); // +1 for the command message
                var deleteableMessages = messages.Where(x => 
                    DateTimeOffset.UtcNow - x.Timestamp < TimeSpan.FromDays(14) &&
                    !x.IsPinned).ToList();

                int totalDeleted = 0;

                if (deleteableMessages.Count > 1)
                {
                    await (Context.Channel as ITextChannel).DeleteMessagesAsync(deleteableMessages);
                    totalDeleted = deleteableMessages.Count;
                }
                else if (deleteableMessages.Count == 1)
                {
                    await deleteableMessages.First().DeleteAsync();
                    totalDeleted = 1;
                }

                var reply = await ReplyAsync($"✅ {totalDeleted} messages were deleted! Thank you for using me as your cleaning lady! :))");

                // Delete confirmation message after 10 seconds
                _ = Task.Delay(10000).ContinueWith(async _ =>
                {
                    try { await reply.DeleteAsync(); } catch { }
                });
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Failed to delete messages: {ex.Message}");
            }
        }

        [Command("cleanup")]
        [Alias("cleanup-channel")]
        [Summary("Cleanup/delete all messages in a specific channel")]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        public async Task CleanupChannelAsync(ulong channelId)
        {
            try
            {
                var textChannel = Context.Guild.GetTextChannel(channelId);
                if (textChannel == null)
                {
                    var errorEmbed = new EmbedBuilder()
                        .WithTitle("❌ Channel Not Found")
                        .WithDescription("Channel not found! Please provide a valid channel ID.")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: errorEmbed);
                    return;
                }

                var startEmbed = new EmbedBuilder()
                    .WithTitle("🧹 Starting Cleanup")
                    .WithDescription($"Starting cleanup of {textChannel.Mention}... This may take a while.")
                    .WithColor(0x40E0D0)
                    .Build();
                await ReplyAsync(embed: startEmbed);

                int totalDeleted = 0;
                bool hasMore = true;

                while (hasMore)
                {
                    var messages = await textChannel.GetMessagesAsync(100).FlattenAsync();
                    // Exclude pinned messages
                    var deleteableMessages = messages.Where(x => !x.IsPinned).ToList();

                    if (!deleteableMessages.Any())
                    {
                        hasMore = false;
                        break;
                    }

                    // Discord bulk delete only works for messages less than 14 days old
                    var recentMessages = deleteableMessages.Where(x => DateTimeOffset.UtcNow - x.Timestamp < TimeSpan.FromDays(14)).ToList();
                    var oldMessages = deleteableMessages.Where(x => DateTimeOffset.UtcNow - x.Timestamp >= TimeSpan.FromDays(14)).ToList();

                    // Bulk delete recent messages
                    if (recentMessages.Count > 1)
                    {
                        await textChannel.DeleteMessagesAsync(recentMessages);
                        totalDeleted += recentMessages.Count;
                    }
                    else if (recentMessages.Count == 1)
                    {
                        await recentMessages.First().DeleteAsync();
                        totalDeleted += 1;
                    }

                    // Delete old messages one by one
                    foreach (var msg in oldMessages)
                    {
                        try
                        {
                            await msg.DeleteAsync();
                            totalDeleted++;
                            await Task.Delay(500); // Rate limit protection
                        }
                        catch { }
                    }

                    if (deleteableMessages.Count < 100) hasMore = false;

                    // Small delay to avoid rate limits
                    await Task.Delay(1000);
                }

                var reply = await ReplyAsync($"✅ {totalDeleted} messages were deleted from {textChannel.Mention}! Thank you for using me as your cleaning lady! :))");

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

        [Command("cleanup-intervall")]
        [Alias("setcleanupinterval")]
        [Summary("Set automatic cleanup interval for a channel (1 hour)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetCleanupIntervalAsync(ulong channelId)
        {
            try
            {
                var channel = Context.Guild.GetTextChannel(channelId);
                if (channel == null)
                {
                    var errorEmbed = new EmbedBuilder()
                        .WithTitle("❌ Channel Not Found")
                        .WithDescription("Channel not found! Make sure the channel ID is correct and the bot has access to it.")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: errorEmbed);
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
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Failed to set cleanup interval: {ex.Message}");
            }
        }

        [Command("delcleanup-intervall")]
        [Alias("cleanupdel", "removecleanupinterval")]
        [Summary("Remove automatic cleanup interval for a channel")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task RemoveCleanupIntervalAsync(ulong channelId)
        {
            try
            {
                var currentInterval = SecurityService.GetCleanupInterval(Context.Guild.Id);
                if (currentInterval == null)
                {
                    var errorEmbed = new EmbedBuilder()
                        .WithTitle("❌ Not Configured")
                        .WithDescription("No cleanup interval is currently set for this server.")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: errorEmbed);
                    return;
                }

                if (currentInterval.ChannelId != channelId)
                {
                    await ReplyAsync($"❌ The cleanup interval is set for <#{currentInterval.ChannelId}>, not <#{channelId}>.");
                    return;
                }

                SecurityService.RemoveCleanupInterval(Context.Guild.Id);

                var embed = new EmbedBuilder()
                    .WithTitle("⏰ Cleanup Interval Removed!")
                    .WithColor(Color.Orange)
                    .AddField("Channel", $"<#{channelId}>", true)
                    .AddField("Status", "❌ Disabled", true)
                    .WithDescription("Automatic cleanup has been disabled for this channel.")
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
                    var errorEmbed = new EmbedBuilder()
                        .WithTitle("❌ Cannot Warn")
                        .WithDescription("You cannot warn yourself!")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: errorEmbed);
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

        [Command("timeoutdel")]
        [Summary("Remove timeout from a user")]
        [RequireUserPermission(GuildPermission.ModerateMembers)]
        [RequireBotPermission(GuildPermission.ModerateMembers)]
        public async Task TimeoutDeleteAsync(SocketGuildUser user)
        {
            try
            {
                if (user.Id == Context.User.Id)
                {
                    var errorEmbed = new EmbedBuilder()
                        .WithTitle("❌ Cannot Remove Timeout")
                        .WithDescription("You cannot remove timeout from yourself!")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: errorEmbed);
                    return;
                }

                if (user.TimedOutUntil == null || user.TimedOutUntil <= DateTimeOffset.UtcNow)
                {
                    var notTimedOutEmbed = new EmbedBuilder()
                        .WithTitle("❌ Not Timed Out")
                        .WithDescription("This user is not timed out!")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: notTimedOutEmbed);
                    return;
                }

                await user.RemoveTimeOutAsync();

                var embed = new EmbedBuilder()
                    .WithTitle("✅ Timeout Removed")
                    .WithColor(Color.Green)
                    .AddField("User", $"{user.Username}#{user.Discriminator}", true)
                    .AddField("Moderator", Context.User.Username, true)
                    .WithTimestamp(DateTimeOffset.UtcNow);

                await ReplyAsync(embed: embed.Build());

                try
                {
                    await user.SendMessageAsync($"Your timeout has been removed in {Context.Guild.Name}.");
                }
                catch { /* User has DMs disabled */ }
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Failed to remove timeout: {ex.Message}");
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

        private async Task<SocketMessage> NextMessageAsync(TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<SocketMessage>();

            Task Handler(SocketMessage message)
            {
                if (message.Channel.Id == Context.Channel.Id && message.Author.Id == Context.User.Id && !message.Author.IsBot)
                {
                    tcs.SetResult(message);
                }
                return Task.CompletedTask;
            }

            Context.Client.MessageReceived += Handler;

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
            Context.Client.MessageReceived -= Handler;

            if (completedTask == tcs.Task)
            {
                return await tcs.Task;
            }

            return null;
        }
    }
}
