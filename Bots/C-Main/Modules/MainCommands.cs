using System.Threading.Tasks;
using Discord.Commands;
using Discord;
using Discord.WebSocket;
using MainbotCSharp.Services;
using System.Linq;
using System.Text.RegularExpressions;
using System;
using System.Collections.Generic;
using System.IO;

namespace MainbotCSharp.Modules
{
    public class MainCommands : ModuleBase<SocketCommandContext>
    {
        [Command("ping")]
        [Summary("Check bot latency")]
        public async Task PingAsync()
        {
            var latency = Context.Client.Latency;
            await ReplyAsync($"🏓 Pong! Latency: {latency}ms");
        }

        [Command("help")]
        [Summary("Shows all available commands")]
        public async Task HelpAsync()
        {
            var embed = new EmbedBuilder()
                .WithTitle("★ !Code.Master() ★")
                .WithColor(0x40E0D0)
                .WithDescription("\n★ Bot-command help ★\n\n*Here are all available commands:*")
                .AddField("★ **General**",
                    "`!info` - Bot info\n" +
                    "`!ping` - Testing the pingspeed of the bot\n" +
                    "`!gm` - Get motivated for the day\n" +
                    "`!gn` - Good night messages for you and your mates\n" +
                    "`!hi` - Say hello and get a hello from me\n" +
                    "`!coffee` - Tell your friends it's coffee time!", false)

                .AddField("★ **Security Features** 🔒 *Premium*",
                    "**All security features require Premium subscription**\n\n" +
                    "`!setsecuritymod` - Enable the AI Security System for this server.\n" +
                    "→ The security system will automatically monitor all messages for spam, NSFW, invite links, and offensive language in multiple languages.\n" +
                    "→ If a violation is detected, the user will be timed out for 2 hours and warned via DM.\n" +
                    "→ You can customize the word list and settings soon.\n" +
                    "`!ban @user` - Manually ban a user\n" +
                    "`!kick @user` - Manually kick a user\n" +
                    "`!timeout @user [minutes]` - Manually timeout a user\n" +
                    "`!timeoutdel @user` - Remove timeout from a user", false)

                .AddField("★ **Ticket System** 🔒 *Premium*",
                    "**All ticket features require Premium subscription**\n\n" +
                    "`!ticket-setup` - Configure ticket system with log channel\n" +
                    "`!ticket-close` - Close the current ticket\n" +
                    "`!ticket-add @user` - Add user to ticket\n" +
                    "`!ticket-remove @user` - Remove user from ticket\n" +
                    "`!ticket-status` - Show ticket system status\n" +
                    "`!ticket-transcript` - Generate transcript for ticket\n" +
                    "`!del-ticket-system` - Remove log channel configuration\n" +
                    "`!del-munga-supportticket` - Completely deactivate ticket system", false)

                .AddField("★ **Voice Features** 🔒 *Premium*",
                    "**All voice features require Premium subscription**\n\n" +
                    "`!setupvoice` - Create Join-to-Create channel\n" +
                    "`!setupvoicelog` - Create voice log channel\n" +
                    "`!cleanupvoice` - Clean voice log channel\n" +
                    "`!deletevoice` - Delete entire voice system\n" +
                    "`!voicename [name]` - Rename your voice channel\n" +
                    "`!voicelimit [0-99]` - Set user limit (0=unlimited)\n" +
                    "`!voicetemplate [gaming/study/chill]` - Apply template\n" +
                    "`!voicelock / !voiceunlock` - Lock/unlock your channel\n" +
                    "`!voicekick @user` - Kick user from your channel\n" +
                    "`!voicestats` - View voice activity stats\n" +
                    "`!voiceprivate` - Make channel private\n" +
                    "`!voicepermit @user` - Allow user to join\n" +
                    "`!voicedeny @user` - Block user from joining", false)

                .AddField("★ **Utilities** ✅ *FREE*",
                    "`!sendit MESSAGE_ID to CHANNEL_ID` - Forward a message\n" +
                    "`!cleanup` - Enable hourly auto-cleanup: deletes all messages in this channel every hour! Run the command in the channel you want to clean up.\n" +
                    "`!cleanupdel` - Stop the hourly auto-cleanup for this channel. Run this command in the channel where cleanup is active.", false)

                .AddField("★ **Twitch** ✅ *FREE*",
                    "`!settwitch` - Link Twitch account and configure clip notifications\n" +
                    "`!setchannel` - Create a new thread-only channel for clips *(use during !settwitch setup)*\n" +
                    "`!testtwitch` - Test clip posting by fetching latest clip\n" +
                    "`!deletetwitch` - Delete your Twitch account data", false)

                .AddField("★ **Bump Reminders** ✅ *FREE*",
                    "`!bumpreminder on/off` - Enable/disable bump reminders\n" +
                    "`!bumpstatus` - Show bump reminder status\n" +
                    "→ Automatically reminds you every 2 hours when server can be bumped\n" +
                    "→ Works with Disboard bot for server promotion", false)

                .AddField("★ **Birthday** ✅ *FREE*",
                    "`!birthdaychannel` - Set the birthday channel *Admin only*\n" +
                    "`!birthdayset` - Save your birthday", false)

                .AddField("★ **Help Categories**",
                    "`!mungehelpdesk` - Shows all big help categories\n" +
                    "`!helpvoice` - Voice help\n" +
                    "`!helpsecure` - Security help\n" +
                    "`!helptwitch` - Twitch help\n" +
                    "`!helpgithub` - GitHub help\n" +
                    "`!helpbump` - Bump/Disboard help\n" +
                    "`!helpbirth` - Birthday help", false)

                .AddField("🔓 **Premium**",
                    "`!premium` - View Premium benefits and upgrade options\n" +
                    "`!premiumstatus` - Check your server's Premium status\n" +
                    "→ Unlock Voice, Security & Ticket features\n" +
                    "→ €5.99/month or €60/year", false)

                .WithImageUrl("https://imgur.com/aYh8OAq.png")
                .WithFooter("Powered by mungabee /aka ozzygirl", "https://i.imgur.com/7mkVUuO.png")
                .WithCurrentTimestamp();

            await ReplyAsync(embed: embed.Build());
        }

        [Command("info")]
        [Summary("Shows bot information")]
        public async Task InfoAsync()
        {
            var embed = new EmbedBuilder()
                .WithTitle("Bot Information")
                .WithColor(Color.Green)
                .AddField("Bot Name", Context.Client.CurrentUser.Username, true)
                .AddField("Server", "[Click me <3](https://discord.gg/hQmvHTs9vz)", true)
                .AddField("Online since", Context.Client.CurrentUser.CreatedAt.ToString("dd.MM.yyyy HH:mm"), true)
                .AddField("Latency", $"{Context.Client.Latency}ms", true)
                .AddField("Framework", "Discord.NET", true)
                .AddField("Language", "C#", true)
                .WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl())
                .WithFooter("Made with ❤️ from mgb")
                .WithCurrentTimestamp();

            await ReplyAsync(embed: embed.Build());
        }

        [Command("gn")]
        [Summary("Good night message")]
        public async Task GoodNightAsync()
        {
            var messages = new[]
            {
                "Good night! 🌙 Sleep well!",
                "Sweet dreams! 😴💤",
                "Sleep tight! 🌙✨",
                "Good night and restful sleep! 😌",
                "Dream something beautiful! 🌙💫",
                "Nighty night! Don't let the bedbugs bite! 🛏️",
                "Rest well, you earned it! 💪🌙",
                "Time to recharge! See you tomorrow! 🔋✨",
                "Off to dreamland! Safe travels! 🌠",
                "May your pillow be soft and your dreams be sweet! 😊",
                "Catch you on the flip side! Sleep tight! 🌙",
                "Lights out! Time for some quality Zzz's! 💡😴",
                "Pleasant dreams and peaceful sleep! 🌌",
                "Good night! Hope you wake up refreshed! ☀️",
                "Sleep well, friend! Tomorrow's a new adventure! 🎒",
                "Night night! Don't stay up too late! ⏰",
                "Time to hit the hay! Good night! 🌾",
                "Sweet slumber awaits! Rest easy! 😌💤",
                "Good night! May your dreams be as awesome as you! 🌟",
                "Off to bed! See you in the morning sunshine! 🌅"
            };

            var random = new Random();
            var message = messages[random.Next(messages.Length)];

            await ReplyAsync(message);
        }

        [Command("hi")]
        [Summary("Say hello")]
        public async Task HelloAsync()
        {
            var messages = new[]
            {
                "Hey there! 👋 How's it going?",
                "Hello! 😊 Great to see you!",
                "Hi! 🎉 Welcome back!",
                "Hey! 👋 What's up?",
                "Howdy! 🤠 Nice to have you here!",
                "Hiya! 😄 How are you doing?",
                "Hello there! 🌟 Good to see you!",
                "Hey hey! 👋 What brings you here today?",
                "Hi there! 😊 Hope you're having a great day!",
                "Greetings! 🎊 How can I help?",
                "Yo! 🤘 What's happening?",
                "Hello friend! 👋 How's your day going?",
                "Hey! 😁 Long time no see!",
                "Hi! ✨ Ready for some fun?",
                "Hello! 🌈 Lovely to see you!",
                "Hey there! 🎮 What's new?",
                "Hi! 🚀 Hope you're doing awesome!",
                "Hello! 💬 Feel free to chat!",
                "Hey! 🎵 How's everything with you?",
                "Hi there! 🌻 Have a wonderful day!"
            };

            var random = new Random();
            var message = messages[random.Next(messages.Length)];

            await ReplyAsync(message);
        }

        [Command("gm")]
        [Summary("Good morning message")]
        public async Task GoodMorningAsync()
        {
            var messages = new[]
            {
                "Good morning! ☀️ Have a beautiful day!",
                "Morning! 🌅 Did you sleep well?",
                "Good morning! ☕ Ready for a new day?",
                "Morning! 🌞 Hope you're feeling good!",
                "Good morning! 🌻 Let's rock this day!",
                "Rise and shine! ✨ Time to conquer the day!",
                "Good morning! 🌈 Make today amazing!",
                "Morning sunshine! ☀️ Ready to crush it?",
                "Top of the morning to you! 🎩 Let's go!",
                "Good morning! 🚀 Today's full of possibilities!",
                "Wake up and be awesome! 💪 Good morning!",
                "Morning! 🌄 Hope you slept like a baby!",
                "Good morning! 🎊 Time to make magic happen!",
                "Rise and grind! ⚡ Good morning!",
                "Morning! 🦅 Soar high today!",
                "Good morning! 🌺 Wishing you a fantastic day!",
                "Wakey wakey! 🥞 Time for some breakfast!",
                "Good morning! 🎯 Let's hit those goals!",
                "Morning! 🌟 Shine bright today!",
                "Good morning! 🎮 Ready to level up your day?"
            };

            var random = new Random();
            var message = messages[random.Next(messages.Length)];

            await ReplyAsync(message);
        }

        [Command("bumpreminder")]
        [Summary("Toggle bump reminders")]
        public async Task BumpReminderAsync(string? action = null)
        {
            if (action == null)
            {
                var usageEmbed = new EmbedBuilder()
                    .WithColor(0xFFD700) // Gold/Yellow
                    .WithTitle("📝 !bumpreminder Command Usage")
                    .WithDescription("Enable or disable automatic bump reminders for your server.")
                    .AddField("Usage", "`!bumpreminder <on|off>`", false)
                    .AddField("Example", "`!bumpreminder on` - Enable reminders\n`!bumpreminder off` - Disable reminders", false)
                    .WithFooter("You need Administrator permissions to use this command")
                    .Build();

                await ReplyAsync(embed: usageEmbed);
                return;
            }

            if (action.ToLower() == "on")
            {
                BumpReminderService.SetBumpReminder(Context.Guild.Id, Context.Channel.Id);
                await ReplyAsync("✅ Bump reminders have been activated! You will be notified when the server can be bumped again.");
            }
            else if (action.ToLower() == "off")
            {
                BumpReminderService.RemoveBumpReminder(Context.Guild.Id);
                await ReplyAsync("❌ Bump reminders have been deactivated.");
            }
            else
            {
                await ReplyAsync("❌ Invalid option. Use `on` or `off`.");
            }
        }

        [Command("bumpstatus")]
        [Summary("Check bump reminder status")]
        public async Task BumpStatusAsync()
        {
            var status = BumpReminderService.GetBumpReminderStatus(Context.Guild.Id);

            var embed = new EmbedBuilder()
                .WithTitle("Bump Reminder Status")
                .WithColor(status?.IsActive == true ? Color.Green : Color.Red)
                .AddField("Status", status?.IsActive == true ? "✅ Enabled" : "❌ Disabled", true)
                .AddField("Channel", Context.Channel.Name, true);

            if (status?.IsActive == true && status.NextBumpTime > DateTime.Now)
            {
                var timeRemaining = status.NextBumpTime - DateTime.Now;
                if (timeRemaining.TotalSeconds > 0)
                {
                    embed.AddField("Next bump possible in",
                        $"{timeRemaining.Hours}h {timeRemaining.Minutes}m {timeRemaining.Seconds}s", false);
                }
                else
                {
                    embed.AddField("Next Bump", "Now possible! 🎉", false);
                }
            }

            embed.WithCurrentTimestamp();
            await ReplyAsync(embed: embed.Build());
        }

        [Command("sendit")]
        [Summary("Forward a message to another channel")]
        public async Task SendItAsync([Remainder] string? args = null)
        {
            // Check admin permissions
            var guildUser = Context.Guild.GetUser(Context.User.Id);
            if (guildUser == null || !guildUser.GuildPermissions.Has(GuildPermission.Administrator))
            {
                await ReplyAsync("❌ This command requires Administrator permissions.");
                return;
            }

            if (string.IsNullOrWhiteSpace(args))
            {
                var usageEmbed = new EmbedBuilder()
                    .WithColor(0xFFD700) // Gold/Yellow
                    .WithTitle("📝 !sendit Command Usage")
                    .WithDescription("Forward a message from this channel to another channel.")
                    .AddField("Usage", "`!sendit MESSAGE_ID to CHANNEL_ID`", false)
                    .AddField("Example", "`!sendit 1234567890123456789 to 9876543210987654321`", false)
                    .WithFooter("You need Administrator permissions to use this command")
                    .Build();

                await ReplyAsync(embed: usageEmbed);
                return;
            }

            // Parse: MESSAGE_ID to CHANNEL_ID
            var parts = args.Split(new[] { " to " }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                await ReplyAsync("❌ Invalid format! Use: `!sendit MESSAGE_ID to CHANNEL_ID`");
                return;
            }

            var messageIdStr = parts[0].Trim();
            var channelIdStr = parts[1].Trim();

            if (!ulong.TryParse(messageIdStr, out var messageId))
            {
                await ReplyAsync("❌ Invalid MESSAGE_ID! Must be a valid snowflake ID.");
                return;
            }

            if (!ulong.TryParse(channelIdStr, out var channelId))
            {
                await ReplyAsync("❌ Invalid CHANNEL_ID! Must be a valid snowflake ID.");
                return;
            }

            try
            {
                // Fetch the message from current channel
                var originalMessage = await Context.Channel.GetMessageAsync(messageId);
                if (originalMessage == null)
                {
                    await ReplyAsync("❌ Message not found in this channel!");
                    return;
                }

                // Get target channel
                var targetChannel = Context.Guild.GetTextChannel(channelId);
                if (targetChannel == null)
                {
                    await ReplyAsync("❌ Target channel not found or not accessible!");
                    return;
                }

                // Check bot permissions in target channel
                var botPerms = targetChannel.GetPermissionOverwrite(Context.Guild.CurrentUser);
                if (botPerms?.SendMessages == PermValue.Deny)
                {
                    await ReplyAsync($"❌ I don't have permission to send messages in {targetChannel.Mention}!");
                    return;
                }

                // Forward the message
                if (originalMessage.Embeds.Count > 0)
                {
                    // Forward embeds
                    foreach (var embed in originalMessage.Embeds)
                    {
                        if (embed is Embed embedObj)
                        {
                            await targetChannel.SendMessageAsync(embed: embedObj);
                        }
                    }
                }
                else
                {
                    // Forward text content
                    var content = originalMessage.Content;
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        content = "*[Empty message or attachment only]*";
                    }

                    var forwardEmbed = new EmbedBuilder()
                        .WithAuthor(originalMessage.Author.Username, originalMessage.Author.GetAvatarUrl())
                        .WithDescription(content)
                        .WithColor(Color.Blue)
                        .WithFooter($"Forwarded from #{Context.Channel.Name}")
                        .WithTimestamp(originalMessage.Timestamp)
                        .Build();

                    await targetChannel.SendMessageAsync(embed: forwardEmbed);
                }

                // Forward attachments if any
                if (originalMessage.Attachments.Count > 0)
                {
                    foreach (var attachment in originalMessage.Attachments)
                    {
                        await targetChannel.SendMessageAsync($"📎 Attachment: {attachment.Url}");
                    }
                }

                await ReplyAsync($"✅ Message forwarded to {targetChannel.Mention}!");
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Error forwarding message: {ex.Message}");
            }
        }
    }
}
