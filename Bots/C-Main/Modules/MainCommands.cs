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
                .WithColor(0x2F3136)
                .WithDescription("```\n★ Bot-command help ★\n```\n**Here are all available commands:**")
                .WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl())

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
                    "`!voicelock / !voiceunlock` - Lock/unlock your channel\n" +
                    "`!voicekick @user` - Kick user from your channel\n" +
                    "`!voicestats` - View voice activity stats\n" +
                    "`!voiceprivate` - Make channel private\n" +
                    "`!voicepermit @user` - Allow user to join\n" +
                    "`!voicedeny @user` - Block user from joining", false)

                .AddField("★ **Utilities** *Admin only, Premium*",
                    "`!sendit MESSAGE_ID to CHANNEL_ID` - Forward a message\n" +
                    "`!cleanup` - Enable hourly auto-cleanup: deletes all messages in this channel every hour! Run the command in the channel you want to clean up.\n" +
                    "`!cleanupdel` - Stop the hourly auto-cleanup for this channel. Run this command in the channel where cleanup is active.\n" +
                    "`!setupflirtlang [language]` - Set AI flirt language for this server\n" +
                    "`!removeflirtlang` - Remove AI flirt language setting for this server\n" +
                    "`!flirt [text]` - Flirt with AI-generated responses", false)

                .AddField("★ **Twitch** *Admin only*",
                    "`!settwitch` - Link Twitch account and configure clip notifications\n" +
                    "`!setchannel` - Create a new thread-only channel for clips *(use during !settwitch setup)*\n" +
                    "`!testtwitch` - Test clip posting by fetching latest clip\n" +
                    "`!deletetwitch` - Delete your Twitch account data", false)

                .AddField("★ **Bump Reminders** *Admin only*",
                    "`!bumpreminder on/off` - Enable/disable bump reminders\n" +
                    "`!bumpstatus` - Show bump reminder status\n" +
                    "→ Automatically reminds you every 2 hours when server can be bumped\n" +
                    "→ Works with Disboard bot for server promotion", false)

                .AddField("★ **Birthday**",
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

                .WithImageUrl("https://imgur.com/aYh8OAq")
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
                "Dream something beautiful! 🌙💫"
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
                "Good morning! 🌻 Let's rock this day!"
            };

            var random = new Random();
            var message = messages[random.Next(messages.Length)];

            await ReplyAsync(message);
        }

        [Command("bumpreminder")]
        [Summary("Toggle bump reminders")]
        public async Task BumpReminderAsync(string action = null)
        {
            if (action == null)
            {
                await ReplyAsync("📝 Usage: `!bumpreminder on` or `!bumpreminder off`");
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
    }
}
