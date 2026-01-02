using System.Threading.Tasks;
using Discord.Commands;
using Discord;
using Discord.WebSocket;
using MainbotCSharp.Modules;
using MainbotCSharp.Services;
using System.Linq;
using System.Text.RegularExpressions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace MainbotCSharp.Modules
{
    // Monitor Service Classes
    public class MonitorService
    {
        private const string STATE_FILE = "mainbot_monitor_state.json";
        private class MonitorState { public Dictionary<string, string> messages { get; set; } = new(); public Dictionary<string, string> lastSeen { get; set; } = new(); }
        private MonitorState _state = new();
        private readonly List<(string key, string display, int color, string[] hints)> _targets = new()
        {
            ("mainbot", "!Code.Master() Stats", 0x008B8B, new[]{"Mainbot","Main","Code.Master","Mainbnbot"}),
            ("pirate", "Mary the red Stats", 0x8B0000, new[]{"Pirat","Pirate","Mary"})
        };

        public MonitorService() { LoadState(); }
        private void LoadState() { try { if (File.Exists(STATE_FILE)) _state = JsonSerializer.Deserialize<MonitorState>(File.ReadAllText(STATE_FILE)) ?? new MonitorState(); } catch { _state = new MonitorState(); } }
        private void SaveState() { try { File.WriteAllText(STATE_FILE, JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true })); } catch { } }

        public async Task StartAsync(DiscordSocketClient client)
        {
            await EnsureMonitorMessages(client);
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    try { await UpdateAllMonitors(client); } catch (Exception ex) { Console.WriteLine("Monitor update error: " + ex); }
                    await Task.Delay(TimeSpan.FromSeconds(60));
                }
            });
        }

        private async Task EnsureMonitorMessages(DiscordSocketClient client)
        {
            var guild = client.GetGuild(1410329844272595050);
            if (guild == null) return;
            var channel = guild.GetTextChannel(1450161151869452360);
            if (channel == null) return;

            foreach (var t in _targets)
            {
                if (_state.messages.TryGetValue(t.key, out var mid))
                {
                    var msg = await channel.GetMessageAsync(ulong.Parse(mid));
                    if (msg != null) continue;
                }

                var embed = new EmbedBuilder().WithTitle(t.display).WithDescription("Initializing status monitor...").WithColor(new Color((uint)t.color)).WithCurrentTimestamp();
                var sent = await channel.SendMessageAsync(embed: embed.Build());
                if (sent != null) { _state.messages[t.key] = sent.Id.ToString(); SaveState(); }
            }
        }

        private async Task UpdateAllMonitors(DiscordSocketClient client)
        {
            var guild = client.GetGuild(1410329844272595050);
            if (guild == null) return;
            var channel = guild.GetTextChannel(1450161151869452360);
            if (channel == null) return;

            foreach (var t in _targets)
            {
                try
                {
                    var res = await CheckTargetStatus(client, guild, t.hints);
                    var embed = new EmbedBuilder()
                        .WithTitle(t.display)
                        .WithColor(new Color((uint)t.color))
                        .AddField("Last Update", res.lastSeen ?? "—", true)
                        .AddField("Status", (res.status == "ONLINE" ? "🟢" : res.status == "STANDBY" ? "🟠" : "🔴") + " " + res.status, true)
                        .WithCurrentTimestamp();

                    if (_state.messages.TryGetValue(t.key, out var mid))
                    {
                        var msgId = ulong.Parse(mid);
                        var msg = await channel.GetMessageAsync(msgId) as IUserMessage;
                        if (msg != null) await msg.ModifyAsync(m => m.Embed = embed.Build());
                        else
                        {
                            var sent = await channel.SendMessageAsync(embed: embed.Build());
                            if (sent != null) { _state.messages[t.key] = sent.Id.ToString(); SaveState(); }
                        }
                    }
                    else
                    {
                        var sent = await channel.SendMessageAsync(embed: embed.Build());
                        if (sent != null) { _state.messages[t.key] = sent.Id.ToString(); SaveState(); }
                    }
                }
                catch (Exception ex) { Console.WriteLine("per-target monitor error: " + ex); }
            }
        }

        private async Task<(string status, string lastSeen)> CheckTargetStatus(DiscordSocketClient client, SocketGuild guild, string[] hints)
        {
            try
            {
                await guild.DownloadUsersAsync();
                SocketGuildUser found = null;
                foreach (var m in guild.Users)
                {
                    if (!m.IsBot) continue;
                    var uname = (m.Username ?? "").ToLowerInvariant();
                    var dname = (m.Nickname ?? "").ToLowerInvariant();
                    foreach (var h in hints)
                    {
                        var lh = h.ToLowerInvariant();
                        if (uname.Contains(lh) || dname.Contains(lh)) { found = m; break; }
                    }
                    if (found != null) break;
                }

                var statusLabel = "OFFLINE";
                string lastSeen = null;
                if (found != null)
                {
                    var pres = found.Activities?.Any() == true || found.Status != UserStatus.Offline ? "online" : "offline";
                    var now = DateTimeOffset.UtcNow;
                    if (pres != "offline") { statusLabel = pres == "online" ? "ONLINE" : "STANDBY"; lastSeen = now.ToString("o"); _state.lastSeen[hints[0]] = lastSeen; SaveState(); return (statusLabel, lastSeen); }
                    if (_state.lastSeen.TryGetValue(hints[0], out var ls) && (DateTimeOffset.UtcNow - DateTimeOffset.Parse(ls)).TotalMinutes < 5) statusLabel = "CRASHED";
                }
                return (statusLabel, lastSeen);
            }
            catch { return ("OFFLINE", null); }
        }
    }
    public class MainCommands : ModuleBase<SocketCommandContext>
    {
        [Command("ping")]
        [Summary("Check bot latency")]
        public async Task PingAsync()
        {
            var latency = Context.Client is Discord.WebSocket.DiscordSocketClient c ? c.Latency : 0;
            await ReplyAsync($"Pong! Latency: {latency}ms");
        }

        [Command("help")]
        [Summary("Show help information")]
        public async Task HelpAsync()
        {
            var embed = new EmbedBuilder()
                .WithAuthor("!Code.Master()", "https://imgur.com/fd7GZFa.png")
                .WithDescription("★ **Bot-command !help**\n\nHere are all available commands:")
                .WithColor(0x40E0D0)
                .AddField("★ **General**",
                    "`!info` - Bot info\n" +
                    "`!ping` - Testing the pingspeed of the bot\n" +
                    "`!gm` - Get motivated for the day\n" +
                    "`!gn` - Good night messages for you and your mates\n" +
                    "`!hi` - Say hello and get a hello from me\n" +
                    "`!coffee` - Tell your friends it's coffee time!\n" +
                    "`!devmeme` - Get a programming meme", false)
                .AddField("★ **Security Features** *Admin only, Premium*",
                    "`!setsecuritymod` - Enable the AI Security System for this server.\n" +
                    "→ The security system will automatically monitor all messages for spam, NSFW, invite links, and offensive language in multiple languages.\n" +
                    "→ If a violation is detected, the user will be timed out for 2 hours and warned via DM.\n" +
                    "→ You can customize the word list and settings soon.\n" +
                    "`!ban @user` - Manually ban a user\n" +
                    "`!kick @user` - Manually kick a user\n" +
                    "`!timeout @user [minutes]` - Manually timeout a user\n" +
                    "`!timeoutdel @user` - Remove timeout from a user", false)
                .AddField("★ **Voice Features** *Admin only, Premium*",
                    "`!setupvoice` - Create Join-to-Create channel *(3 channels free)*\n" +
                    "`!setupvoicelog` - Create voice log channel *(free)*\n" +
                    "`!cleanupvoice` - Clean voice log channel\n" +
                    "`!deletevoice` - Delete entire voice system *(free)*\n" +
                    "`!voicename [name]` - Rename your voice channel\n" +
                    "`!voicelimit [0-99]` - Set user limit (0=unlimited)\n" +
                    "`!voicetemplate [gaming/study/chill]` - Apply template\n" +
                    "`!voicelock`/`!voiceunlock` - Lock/unlock your voice channel\n" +
                    "`!voicekick @user` - Kick user from your channel\n" +
                    "`!voicestats` - View voice activity stats\n" +
                    "`!voiceprivate` - Make channel private\n" +
                    "`!voicepermit @user` - Allow user to join\n" +
                    "`!voicedeny @user` - Block user from joining", false)
                .AddField("★ **Utilities** *Admin only, Premium*",
                    "`!sendit MESSAGE_ID to CHANNEL_ID` - Forward a message\n" +
                    "`!cleanup` - Enable hourly auto-cleanup: deletes all messages in this channel every hour. Run this command in the channel you want to clean up.\n" +
                    "`!cleanupdel` - Stop the hourly auto-cleanup for this channel. Run this command in the channel where cleanup is active.\n" +
                    "`!setupflirtlang [language]` - Set AI flirt language for this server\n" +
                    "`!removeflirtlang` - Remove AI flirt language setting for this server\n" +
                    "`!flirt [text]` - Flirt with AI-generated responses", false)
                .AddField("★ **Twitch** *Admin only*",
                    "`!settwitch` - Link Twitch account and configure clip notifications\n" +
                    "`!setchannel` - Create a new thread-only channel for clips *(use during `!settwitch` setup)*\n" +
                    "`!testtwitch` - Test clip posting by fetching latest clip\n" +
                    "`!deletetwitch` - Delete your Twitch account data", false)
                .AddField("★ **GitHub** ✅ *OAuth Integration*",
                    "`!github` - Bot owner's GitHub and Repos\n" +
                    "`!congithubacc` - Connect your GitHub account with the bot\n" +
                    "`!discongithubacc` - Disconnect your GitHub account\n" +
                    "`!gitrank` - Show your GitHub commit level\n" +
                    "`!gitleader` - Show the top 10 committers", false)
                .AddField("★ **Bump Reminders** *Admin only*",
                    "`!setbumpreminder` - Setze einen 2-Stunden-Bump-Reminder\n" +
                    "`!getbumpreminder` - Lösche den aktiven Bump-Reminder\n" +
                    "`!bumpreminder` - Aktiviere den Bump-Reminder (Alias)\n" +
                    "`!bumpreminderdet` - Deaktiviere den Bump-Reminder (Alias)\n" +
                    "`!bumpstatus` - Zeigt den Status des Bump-Reminders\n" +
                    "`!bumphelp` - Zeigt Hilfe zum Bump-System", false)
                .AddField("★ **Birthday**",
                    "`!birthdaychannel` - Set the birthday channel *Admin only*\n" +
                    "`!birthdayset` - Save your birthday", false)
                .AddField("★ **Help Categories**",
                    "`!mungahelpdesk` - Shows all big help categories\n" +
                    "`!helpvoice` - Voice help\n" +
                    "`!helpsecure` - Security help\n" +
                    "`!helptwitch` - Twitch help\n" +
                    "`!helpgithub` - GitHub help\n" +
                    "`!helpbump` - Bump/Disboard help\n" +
                    "`!helpbirth` - Birthday help", false)
                .WithFooter("Powered by mungabee /aka ozzygirl", "https://imgur.com/LjKtaGB.png")
                .WithImageUrl("https://imgur.com/aYh8OAq.png");

            await ReplyAsync(embed: embed.Build());
        }

        // Placeholder for security moderation trigger - this mimics the JS handler invocation
        [Command("debugsecurity")]
        [Summary("Run a quick security check on the message content (admin only)")]
        public async Task DebugSecurityAsync([Remainder] string text = null)
        {
            var guser = Context.User as SocketGuildUser;
            if (!Context.User.IsBot && Context.Guild != null && guser != null && !guser.Roles.Any(r => !r.IsEveryone))
            {
                // implement security checks when porting
            }
            await ReplyAsync("Security check placeholder (not implemented yet)");
        }

        // Birthday Commands
        [Command("birthdaychannel")]
        [Summary("Set the birthday notification channel (Admin only)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetBirthdayChannelAsync()
        {
            try
            {
                BirthdayService.SetBirthdayChannel(Context.Guild.Id, Context.Channel.Id);
                await ReplyAsync($"✅ Birthday notifications will now be sent to <#{Context.Channel.Id}>");
            }
            catch (Exception ex)
            {
                await ReplyAsync("❌ Error setting birthday channel: " + ex.Message);
            }
        }

        [Command("birthdayset")]
        [Summary("Set your birthday (format: dd/mm/yyyy)")]
        public async Task SetBirthdayAsync([Remainder] string birthday = null)
        {
            try
            {
                // Delete user message for privacy
                try { await Context.Message.DeleteAsync(); } catch { }

                if (string.IsNullOrWhiteSpace(birthday))
                {
                    var msg = await ReplyAsync("Please provide your birthday in format: dd/mm/yyyy (e.g., 25/12/1995)");

                    // Wait for user response using simple message collection
                    var response = await Context.Channel.GetMessagesAsync(1).FlattenAsync();

                    // Simple timeout approach - wait for next message from user
                    var userMessages = await Context.Channel.GetMessagesAsync(50).FlattenAsync();
                    foreach (var userMsg in userMessages)
                    {
                        if (userMsg.Author.Id == Context.User.Id && userMsg.Id != Context.Message.Id)
                        {
                            birthday = userMsg.Content?.Trim();
                            try { await userMsg.DeleteAsync(); } catch { }
                            break;
                        }
                    }

                    try { await msg.DeleteAsync(); } catch { }
                }

                if (string.IsNullOrWhiteSpace(birthday))
                {
                    var errorMsg = await ReplyAsync("❌ No birthday provided. Use: `!birthdayset dd/mm/yyyy`");
                    _ = Task.Run(async () => { await Task.Delay(5000); try { await errorMsg.DeleteAsync(); } catch { } });
                    return;
                }

                if (BirthdayService.SetUserBirthday(Context.Guild.Id, Context.User.Id, birthday))
                {
                    var successMsg = await ReplyAsync($"🎉 Birthday set for <@{Context.User.Id}>!");
                    _ = Task.Run(async () => { await Task.Delay(3000); try { await successMsg.DeleteAsync(); } catch { } });
                }
                else
                {
                    var errorMsg = await ReplyAsync("❌ Invalid date format. Please use: dd/mm/yyyy (e.g., 25/12/1995)");
                    _ = Task.Run(async () => { await Task.Delay(5000); try { await errorMsg.DeleteAsync(); } catch { } });
                }
            }
            catch (Exception ex)
            {
                await ReplyAsync("❌ Error setting birthday: " + ex.Message);
            }
        }

        [Command("birthdayremove")]
        [Summary("Remove your birthday from the system")]
        public async Task RemoveBirthdayAsync()
        {
            try
            {
                // Delete user message for privacy
                try { await Context.Message.DeleteAsync(); } catch { }

                if (BirthdayService.RemoveUserBirthday(Context.Guild.Id, Context.User.Id))
                {
                    var msg = await ReplyAsync($"🗑️ Birthday removed for <@{Context.User.Id}>");
                    _ = Task.Run(async () => { await Task.Delay(3000); try { await msg.DeleteAsync(); } catch { } });
                }
                else
                {
                    var msg = await ReplyAsync("❌ No birthday found to remove.");
                    _ = Task.Run(async () => { await Task.Delay(3000); try { await msg.DeleteAsync(); } catch { } });
                }
            }
            catch (Exception ex)
            {
                await ReplyAsync("❌ Error removing birthday: " + ex.Message);
            }
        }

        [Command("birthdaylist")]
        [Summary("Show all birthdays in this server")]
        public async Task ListBirthdaysAsync()
        {
            try
            {
                var data = BirthdayService.GetGuildBirthdayData(Context.Guild.Id);
                if (data == null || !data.Users.Any())
                {
                    await ReplyAsync("📅 No birthdays set in this server yet.");
                    return;
                }

                var eb = new EmbedBuilder()
                    .WithTitle("🎂 Server Birthdays")
                    .WithColor(Color.Gold)
                    .WithFooter($"Notifications will be sent to: {(data.ChannelId != 0 ? $"<#{data.ChannelId}>" : "Not set")}");

                var birthdayList = data.Users
                    .OrderBy(kvp =>
                    {
                        var parts = kvp.Value.Split('/');
                        return new DateTime(2000, int.Parse(parts[1]), int.Parse(parts[0]));
                    })
                    .Select(kvp => $"<@{kvp.Key}> - {kvp.Value.Substring(0, 5)}") // Only show dd/mm, not year
                    .ToList();

                // Split into chunks if too many birthdays
                const int maxPerField = 15;
                for (int i = 0; i < birthdayList.Count; i += maxPerField)
                {
                    var chunk = birthdayList.Skip(i).Take(maxPerField);
                    var fieldName = birthdayList.Count > maxPerField ? $"Birthdays ({i + 1}-{Math.Min(i + maxPerField, birthdayList.Count)})" : "Birthdays";
                    eb.AddField(fieldName, string.Join("\n", chunk), false);
                }

                await ReplyAsync(embed: eb.Build());
            }
            catch (Exception ex)
            {
                await ReplyAsync("❌ Error listing birthdays: " + ex.Message);
            }
        }

        [Command("info")]
        [Summary("Show bot information")]
        public async Task InfoAsync()
        {
            var embed = new EmbedBuilder()
                .WithAuthor("CoderMaster Bot Info", "https://imgur.com/aYh8OAq.png")
                .WithDescription("**Bot Information & Details**")
                .WithColor(0x40E0D0)
                .AddField("Bot Name", "CoderMaster", true)
                .AddField("Version", "v1.0.0", true)
                .AddField("Created", "Oktober 2024", true)
                .AddField("Developer", "mungabee / ozzygirl", true)
                .AddField("Platform", "Discord.NET 3.11.0", true)
                .AddField("Language", "C# (.NET 8.0)", true)
                .AddField("Top.gg", "[Invite me to your Server ^^](https://top.gg/bot/1435244593301159978?s=06f6dae4811fd)", false)
                .AddField("Server Count", Context.Client.Guilds.Count.ToString(), true)
                .AddField("User Count", Context.Client.Guilds.Sum(g => g.MemberCount).ToString(), true)
                .AddField("Latency", $"{Context.Client.Latency}ms", true)
                .AddField("Features",
                    "• Automated Security System\n" +
                    "• Voice Channel Management\n" +
                    "• Birthday Reminder\n" +
                    "• Cleanup channels (no limit)\n" +
                    "• Ticket System ez to use\n" +
                    "• Twitch Integration\n" +
                    "• GitHub Integration\n" +
                    "• Bump Reminders", false)
                .WithFooter("Powered by mungabee /aka ozzygirl", "https://imgur.com/LjKtaGB.png")
                .WithTimestamp(DateTimeOffset.Now);

            await ReplyAsync(embed: embed.Build());
        }

        [SlashCommand("gn", "Say good night with random messages")]
        public async Task GoodNight()
        {
            var responses = new[]
            {
                "Good night! 🌙",
                "Sweet dreams! ✨",
                "Sleep well! 😴",
                "Nighty night! 🌟",
                "Rest well! 💤",
                "Dream sweetly! 🌙✨"
            };

            var random = new Random();
            var response = responses[random.Next(responses.Length)];

            await ReplyAsync(response);
        }

        [SlashCommand("gm", "Say good morning with random messages")]
        public async Task GoodMorning()
        {
            var responses = new[]
            {
                "Good morning! ☀️",
                "Rise and shine! ✨",
                "Morning! ☕",
                "Wake up sunshine! 🌅",
                "Have a great day! 🌞",
                "Time to code! ☀️💻"
            };

            var random = new Random();
            var response = responses[random.Next(responses.Length)];

            await ReplyAsync(response);
        }

        [SlashCommand("hi", "Say hello with random messages")]
        public async Task Hello()
        {
            var responses = new[]
            {
                "Hello! 👋",
                "Hi there! 😊",
                "Hey! 🎉",
                "Howdy! 🤠",
                "Greetings! ✨",
                "What's up! 🚀"
            };

            var random = new Random();
            var response = responses[random.Next(responses.Length)];

            await ReplyAsync(response);
        }

        [SlashCommand("ping", "Check bot latency and response time")]
        public async Task Ping()
        {
            var latency = Context.Client.Latency;
            await ReplyAsync($"🏓 Pong! Latency: {latency}ms");
        }

        [SlashCommand("coffee", "Get your virtual coffee fix")]
        public async Task Coffee()
        {
            var coffeeEmojis = new[] { "☕", "🍵", "🥤", "🧋", "☕", "🍺" };
            var coffeeMessages = new[]
            {
                "Here's your coffee! Enjoy!",
                "Fresh brew coming up!",
                "Caffeine incoming!",
                "Time for a coffee break!",
                "Your daily dose of energy!",
                "Fuel for coding!"
            };

            var random = new Random();
            var emoji = coffeeEmojis[random.Next(coffeeEmojis.Length)];
            var message = coffeeMessages[random.Next(coffeeMessages.Length)];

            await ReplyAsync($"{emoji} {message}");
        }

        [SlashCommand("devmeme", "Get a random developer meme")]
        public async Task DevMeme()
        {
            var memes = new[]
            {
                "https://i.imgur.com/dVDJiez.jpg",
                "https://i.imgur.com/9nVMRqa.jpg",
                "https://i.imgur.com/5K4n8Qz.png",
                "https://i.imgur.com/XqQXjch.jpg",
                "https://i.imgur.com/yQraNOx.png",
                "https://i.imgur.com/KqGHF7e.jpg"
            };

            var random = new Random();
            var meme = memes[random.Next(memes.Length)];

            var embed = new EmbedBuilder()
                .WithTitle("😂 Developer Meme")
                .WithImageUrl(meme)
                .WithColor(0x40E0D0)
                .WithFooter("Keep coding! 💻", "https://imgur.com/LjKtaGB.png")
                .WithTimestamp(DateTimeOffset.Now);

            await ReplyAsync(embed: embed.Build());
        }

        // GitHub Commands
        [Command("github")]
        [Summary("Show bot owner's GitHub and repositories")]
        public async Task GitHubAsync()
        {
            var embed = new EmbedBuilder()
                .WithTitle("🐙 **Bot Owner GitHub**")
                .WithDescription("**OZZYGIRL/mungabee** - Discord Bot Developer")
                .WithColor(0x24292e)
                .AddField("📊 **GitHub Profile**", "[github.com/mungabee](https://github.com/mungabee)", false)
                .AddField("🤖 **Main Repository**", "[discord-bot-web](https://github.com/mungabee/discord-bot-web)", false)
                .AddField("💻 **Featured Projects**",
                    "• **CoderMaster Bot** - Multi-featured Discord bot\n" +
                    "• **Voice Management** - Advanced voice channel system\n" +
                    "• **Security System** - Auto-moderation features\n" +
                    "• **GitHub Integration** - OAuth & commit tracking", false)
                .WithThumbnail("https://github.com/mungabee.png")
                .WithFooter("Connect your own GitHub with !congithubacc")
                .WithTimestamp(DateTimeOffset.UtcNow);

            await ReplyAsync(embed: embed.Build());
        }

        [Command("congithubacc")]
        [Summary("Connect your GitHub account with the bot")]
        public async Task ConnectGitHubAsync()
        {
            try
            {
                // Check if user already has GitHub linked
                var existingData = GitHubService.GetGitHubData(Context.User.Id);
                if (existingData != null)
                {
                    await ReplyAsync($"✅ You already have GitHub connected: **{existingData.GitHubUsername}**\nUse `!discongithubacc` to disconnect first.");
                    return;
                }

                // Generate OAuth URL (credentials read from environment internally)
                var redirectUri = "http://localhost:3000/github/callback";
                var oauthUrl = GitHubService.GenerateOAuthUrl(Context.User.Id, redirectUri);

                if (string.IsNullOrEmpty(oauthUrl))
                {
                    await ReplyAsync("❌ GitHub OAuth is not configured. Please contact the bot owner.");
                    return;
                }

                var embed = new EmbedBuilder()
                    .WithTitle("🔗 **Connect GitHub Account**")
                    .WithDescription("Click the link below to connect your GitHub account with the bot:")
                    .WithColor(0x24292e)
                    .AddField("🌐 **OAuth Link**", $"[Click here to connect GitHub]({oauthUrl})", false)
                    .AddField("📋 **What happens next?**",
                        "1. You'll be redirected to GitHub to authorize\n" +
                        "2. After authorization, you'll get the 'GitHub-Coder' role\n" +
                        "3. Your commits will be tracked for leaderboards\n" +
                        "4. You can use `!gitrank` and other GitHub commands", false)
                    .WithFooter("Your data is secure and only public repository info is accessed");

                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                await ReplyAsync("❌ Error generating GitHub OAuth link: " + ex.Message);
            }
        }

        [Command("discongithubacc")]
        [Summary("Disconnect your GitHub account")]
        public async Task DisconnectGitHubAsync()
        {
            try
            {
                if (GitHubService.DisconnectGitHub(Context.User.Id))
                {
                    await ReplyAsync("✅ GitHub account disconnected and GitHub-Coder role removed.");
                }
                else
                {
                    await ReplyAsync("❌ No GitHub account found to disconnect.");
                }
            }
            catch (Exception ex)
            {
                await ReplyAsync("❌ Error disconnecting GitHub: " + ex.Message);
            }
        }

        [Command("gitrank")]
        [Summary("Show your GitHub commit level")]
        public async Task GitRankAsync()
        {
            try
            {
                var userData = GitHubService.GetGitHubData(Context.User.Id);
                if (userData == null)
                {
                    await ReplyAsync("❌ You don't have a GitHub account connected. Use `!congithubacc` to connect.");
                    return;
                }

                var commitCount = await GitHubService.GetUserCommitCountAsync(Context.User.Id);
                if (commitCount == -1)
                {
                    await ReplyAsync("❌ Error fetching your GitHub commit data.");
                    return;
                }

                string rankEmoji, rankName;
                if (commitCount >= 1000) { rankEmoji = "🏆"; rankName = "Git Master"; }
                else if (commitCount >= 500) { rankEmoji = "⭐"; rankName = "Git Expert"; }
                else if (commitCount >= 200) { rankEmoji = "🥇"; rankName = "Senior Developer"; }
                else if (commitCount >= 100) { rankEmoji = "🥈"; rankName = "Developer"; }
                else if (commitCount >= 50) { rankEmoji = "🥉"; rankName = "Junior Developer"; }
                else if (commitCount >= 10) { rankEmoji = "📝"; rankName = "Contributor"; }
                else { rankEmoji = "🌱"; rankName = "Beginner"; }

                var embed = new EmbedBuilder()
                    .WithTitle($"{rankEmoji} **Your GitHub Rank**")
                    .WithDescription($"**{Context.User.Username}** connected as **{userData.GitHubUsername}**")
                    .WithColor(0x28a745)
                    .AddField("📊 **Commit Count**", commitCount.ToString(), true)
                    .AddField("🏅 **Rank**", $"{rankEmoji} {rankName}", true)
                    .AddField("📈 **Progress**",
                        commitCount < 10 ? $"{10 - commitCount} commits to Contributor" :
                        commitCount < 50 ? $"{50 - commitCount} commits to Junior Developer" :
                        commitCount < 100 ? $"{100 - commitCount} commits to Developer" :
                        commitCount < 200 ? $"{200 - commitCount} commits to Senior Developer" :
                        commitCount < 500 ? $"{500 - commitCount} commits to Git Expert" :
                        commitCount < 1000 ? $"{1000 - commitCount} commits to Git Master" :
                        "🎯 Max rank achieved!", false)
                    .WithThumbnail($"https://github.com/{userData.GitHubUsername}.png")
                    .WithFooter("Commits are counted from all your public repositories");

                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                await ReplyAsync("❌ Error getting GitHub rank: " + ex.Message);
            }
        }

        [Command("gitleader")]
        [Summary("Show the top 10 committers")]
        public async Task GitLeaderAsync()
        {
            try
            {
                var topCommitters = await GitHubService.GetTopCommittersAsync();
                if (!topCommitters.Any())
                {
                    await ReplyAsync("📊 No GitHub accounts connected yet. Use `!congithubacc` to be the first!");
                    return;
                }

                var embed = new EmbedBuilder()
                    .WithTitle("🏆 **GitHub Leaderboard**")
                    .WithDescription("Top committers in this server:")
                    .WithColor(0xffd700);

                for (int i = 0; i < Math.Min(topCommitters.Count, 10); i++)
                {
                    var (username, commits) = topCommitters[i];
                    string rankEmoji = i switch
                    {
                        0 => "🥇",
                        1 => "🥈",
                        2 => "🥉",
                        _ => $"{i + 1}."
                    };

                    embed.AddField($"{rankEmoji} **{username}**", $"{commits} commits", true);
                }

                embed.WithFooter($"Showing top {Math.Min(topCommitters.Count, 10)} contributors • Use !gitrank to see your rank");
                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                await ReplyAsync("❌ Error getting GitHub leaderboard: " + ex.Message);
            }
        }

        // Bump Reminder Commands
        [Command("setbumpreminder")]
        [Alias("bumpreminder")]
        [Summary("Set a 2-hour bump reminder (Admin only)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetBumpReminderAsync()
        {
            try
            {
                if (BumpReminderService.SetBumpReminder(Context.Guild.Id, Context.Channel.Id))
                {
                    await ReplyAsync("✅ **2-Stunden Bump-Reminder aktiviert!**\nDer Bot wird Sie in 2 Stunden daran erinnern zu bumpen.");
                }
                else
                {
                    await ReplyAsync("❌ Fehler beim Setzen des Bump-Reminders.");
                }
            }
            catch (Exception ex)
            {
                await ReplyAsync("❌ Error setting bump reminder: " + ex.Message);
            }
        }

        [Command("getbumpreminder")]
        [Alias("bumpreminderdet")]
        [Summary("Remove the active bump reminder (Admin only)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task RemoveBumpReminderAsync()
        {
            try
            {
                if (BumpReminderService.RemoveBumpReminder(Context.Guild.Id))
                {
                    await ReplyAsync("✅ **Bump-Reminder deaktiviert!**\nKeine weiteren Bump-Erinnerungen werden gesendet.");
                }
                else
                {
                    await ReplyAsync("❌ Kein aktiver Bump-Reminder gefunden.");
                }
            }
            catch (Exception ex)
            {
                await ReplyAsync("❌ Error removing bump reminder: " + ex.Message);
            }
        }

        [Command("bumpstatus")]
        [Summary("Show bump reminder status")]
        public async Task BumpStatusAsync()
        {
            try
            {
                var status = BumpReminderService.GetBumpReminderStatus(Context.Guild.Id);
                if (status == null)
                {
                    await ReplyAsync("📊 **Bump-Reminder Status: Inaktiv**\nKein Bump-Reminder für diesen Server gesetzt.");
                    return;
                }

                var embed = new EmbedBuilder()
                    .WithTitle("🔔 **Bump-Reminder Status**")
                    .WithColor(status.IsActive ? 0x00ff00 : 0xff0000)
                    .AddField("📊 **Status**", status.IsActive ? "✅ Aktiv" : "❌ Inaktiv", true)
                    .AddField("📍 **Kanal**", $"<#{status.ChannelId}>", true)
                    .AddField("⏰ **Nächster Bump**",
                        status.IsActive ? status.NextBumpTime.ToString("dd.MM.yyyy HH:mm") + " Uhr" : "Nicht gesetzt", false)
                    .AddField("🌍 **Sprache**", status.Language, true)
                    .WithFooter("Bump-Reminders werden automatisch nach Disboard-Bumps gesetzt");

                if (status.IsActive)
                {
                    var timeLeft = status.NextBumpTime - DateTime.Now;
                    if (timeLeft.TotalMinutes > 0)
                    {
                        embed.AddField("⏳ **Zeit verbleibend**",
                            $"{timeLeft.Hours}h {timeLeft.Minutes}m", true);
                    }
                }

                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                await ReplyAsync("❌ Error getting bump status: " + ex.Message);
            }
        }

        [Command("bumphelp")]
        [Summary("Show bump system help")]
        public async Task BumpHelpAsync()
        {
            var embed = new EmbedBuilder()
                .WithTitle("🔔 **Bump-System Hilfe**")
                .WithDescription("Das automatische Disboard Bump-Erinnerungs-System:")
                .WithColor(0x7289DA)
                .AddField("📋 **Befehle** *(Admin only)*",
                    "`!setbumpreminder` - 2-Stunden Reminder aktivieren\n" +
                    "`!getbumpreminder` - Aktiven Reminder löschen\n" +
                    "`!bumpstatus` - Status des Bump-Reminders anzeigen\n" +
                    "`!bumphelp` - Diese Hilfe anzeigen", false)
                .AddField("🤖 **Automatische Erkennung**",
                    "• Bot erkennt automatisch Disboard `/bump` Befehle\n" +
                    "• Setzt automatisch 2-Stunden Timer\n" +
                    "• Sendet Erinnerung wenn Cooldown vorbei ist\n" +
                    "• Unterstützt mehrere Sprachen", false)
                .AddField("🌍 **Unterstützte Sprachen**",
                    "🇩🇪 Deutsch • 🇺🇸 English • 🇫🇷 Français • 🇪🇸 Español • 🇮🇹 Italiano • 🇵🇹 Português", false)
                .WithFooter("Bump-Reminders helfen dabei, Server-Sichtbarkeit auf Disboard zu erhalten");

            await ReplyAsync(embed: embed.Build());
        }
    }
}
