using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace MainbotCSharp.Modules
{
    public class HelpCategoriesCommands : ModuleBase<SocketCommandContext>
    {
        [Command("mungahelpdesk")]
        [Summary("Shows all big help categories with interactive menu")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task MungaHelpdeskAsync()
        {
            var embed = new EmbedBuilder()
                .WithTitle("**Code.Master() Help Desk**")
                .WithDescription("**Select a category from the menu below to get detailed help:**")
                .WithColor(0x40E0D0) // Turquoise
                .AddField("📋 *Available Categories*",
                    "• All Commands - Complete command overview\n" +
                    "• Voice - Voice channel management\n" +
                    "• Security - Moderation & security features\n" +
                    "• Twitch - Twitch integration commands\n" +
                    "• Bump - Disboard bump reminders\n" +
                    "• Birthday - Birthday notification system", false)
                .WithFooter("Made by OZZYGIRL/mungabee", "https://github.com/mungabee.png")
                .WithTimestamp(DateTimeOffset.UtcNow);

            var selectMenu = new SelectMenuBuilder()
                .WithPlaceholder("Choose a help category...")
                .WithCustomId("helpdesk_select")
                .WithMinValues(1)
                .WithMaxValues(1)
                .AddOption("All Commands", "help_all", "Show complete command list", new Emoji("📜"))
                .AddOption("Voice Features", "help_voice", "Voice channel management", new Emoji("🎤"))
                .AddOption("Security Features", "help_secure", "Moderation & security", new Emoji("🛡️"))
                .AddOption("Twitch Integration", "help_twitch", "Twitch commands", new Emoji("🎮"))
                .AddOption("Bump Reminders", "help_bump", "Disboard bump system", new Emoji("🔔"))
                .AddOption("Birthday System", "help_birth", "Birthday notifications", new Emoji("🎂"));

            var component = new ComponentBuilder()
                .WithSelectMenu(selectMenu)
                .Build();

            await ReplyAsync(embed: embed.Build(), components: component);
        }

        [Command("munga-supportticket")]
        [Summary("Post support ticket menu for users")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SupportTicketAsync()
        {
            var embed = new EmbedBuilder()
                .WithTitle("🎫 **Support Ticket System**")
                .WithDescription("**Need help or want to report an issue?**\n\nSelect the category that best describes your issue from the menu below, and we'll create a private ticket channel for you.")
                .WithColor(0x2f3136)
                .AddField("📋 **Available Support Categories**",
                    "• **Technical Issue** - Bot not working, errors, bugs\n" +
                    "• **Spam / Scam** - Report spam or scam content\n" +
                    "• **Abuse / Harassment** - Report user misconduct\n" +
                    "• **Advertising / Recruitment** - Report unwanted promotion\n" +
                    "• **Bug / Feature Request** - Report bugs or suggest features\n" +
                    "• **Other** - General questions or other issues", false)
                .WithFooter("Support tickets are private and only visible to you and server staff")
                .WithTimestamp(DateTimeOffset.UtcNow);

            var selectMenu = new SelectMenuBuilder()
                .WithPlaceholder("Select your issue type...")
                .WithCustomId("support_select")
                .WithMinValues(1)
                .WithMaxValues(1)
                .AddOption("Technical Issue", "support_technical", "Bot errors, commands not working", new Emoji("🔧"))
                .AddOption("Spam / Scam", "support_spam", "Report spam or scam content", new Emoji("🚫"))
                .AddOption("Abuse / Harassment", "support_abuse", "Report user misconduct", new Emoji("⚠️"))
                .AddOption("Advertising / Recruitment", "support_ad", "Unwanted promotion", new Emoji("📢"))
                .AddOption("Bug / Feature Request", "support_bug", "Report bugs or suggest features", new Emoji("🐛"))
                .AddOption("Other", "support_other", "General questions", new Emoji("❓"));

            var component = new ComponentBuilder()
                .WithSelectMenu(selectMenu)
                .Build();

            await ReplyAsync(embed: embed.Build(), components: component);
        }

        // Individual help commands
        [Command("helpvoice")]
        [Alias("helpyvoice")]
        [Summary("Show detailed voice system help")]
        public async Task HelpVoiceAsync()
        {
            var embed = new EmbedBuilder()
                .WithTitle("🎤 **Voice System Help**")
                .WithColor(0x40E0D0)
                .AddField("**Admin Setup Commands**",
                    "`!setupvoice` - Create Join-to-Create channel *(3 channels free)*\n" +
                    "`!setupvoicelog` - Create voice log channel *(free)*\n" +
                    "`!cleanupvoice` - Clean voice log channel\n" +
                    "`!deletevoice` - Delete entire voice system *(free)*", false)
                .AddField("**User Voice Commands**",
                    "`!voicename [name]` - Rename your voice channel\n" +
                    "`!voicelock` - Lock your voice channel\n" +
                    "`!voiceunlock` - Unlock your voice channel\n" +
                    "`!voicekick @user` - Kick user from your channel\n" +
                    "`!voiceban @user` - Ban user from your channel\n" +
                    "`!voiceunban @user` - Unban user from your channel", false)
                .AddField("**Voice Templates**",
                    "`!voicetemplate [name]` - Save current channel as template\n" +
                    "`!voiceload [template]` - Load template settings\n" +
                    "`!voicetemplates` - List your saved templates", false)
                .AddField("**Voice Statistics**",
                    "`!voicestats` - Show your voice activity stats\n" +
                    "`!voiceleaderboard` - Server voice leaderboard\n" +
                    "`!voicetime` - Your total voice time", false)
                .WithFooter("Voice system includes AFK detection and automatic cleanup");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("helpsecure")]
        [Alias("helpysecure")]
        [Summary("Show detailed security system help")]
        public async Task HelpSecureAsync()
        {
            var embed = new EmbedBuilder()
                .WithTitle("🛡️ **Security System Help**")
                .WithColor(0xFF4444)
                .AddField("**Setup Commands** *(Admin only)*",
                    "`!setsecuritymod` - Enable security system\n" +
                    "`!securityoff` - Disable security system\n" +
                    "`!securitystatus` - Check security status\n" +
                    "`!securitylog` - Set security log channel", false)
                .AddField("**Moderation Commands** *(Admin only)*",
                    "`!sban @user [reason]` - Security ban with logging\n" +
                    "`!skick @user [reason]` - Security kick with logging\n" +
                    "`!timeout @user [minutes]` - Timeout user\n" +
                    "`!timeoutdel @user` - Remove timeout\n" +
                    "`!cleanup [amount]` - Bulk delete messages", false)
                .AddField("**Auto-Detection Features**",
                    "• **Suspicious Users** - New accounts (<20 days)\n" +
                    "• **Spam Detection** - Rapid message posting\n" +
                    "• **Link Filtering** - Suspicious URLs\n" +
                    "• **Username Analysis** - Suspicious patterns\n" +
                    "• **Profile Picture Check** - Default/suspicious avatars", false)
                .AddField("**Manual Security Actions**",
                    "`!checksuspicious @user` - Manual suspicious user check\n" +
                    "`!securityreport @user [reason]` - Report user to security log\n" +
                    "`!whitelist @user` - Add user to security whitelist", false)
                .WithFooter("Security system automatically monitors new members and suspicious activity");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("helptwitch")]
        [Alias("helpytwitch")]
        [Summary("Show detailed Twitch integration help")]
        public async Task HelpTwitchAsync()
        {
            var embed = new EmbedBuilder()
                .WithTitle("🎮 **Twitch Integration Help**")
                .WithColor(0x9146FF)
                .AddField("**Setup Commands** *(Admin only)*",
                    "`!settwitch` - Link Twitch account and configure clip notifications\n" +
                    "`!setchannel` - Create a new thread-only channel for clips *(use during setup)*\n" +
                    "`!deletetwitch` - Delete your Twitch account data", false)
                .AddField("**Testing & Management**",
                    "`!testtwitch` - Test clip posting by fetching latest clip\n" +
                    "`!twitchstatus` - Show current Twitch configuration\n" +
                    "`!twitchstats` - Show your Twitch statistics", false)
                .AddField("**How Twitch Integration Works**",
                    "1. **Setup**: Admin runs `!settwitch` and links Twitch account\n" +
                    "2. **Channel**: Bot creates/uses thread-only channel for clips\n" +
                    "3. **Auto-Post**: Bot automatically posts new clips from your Twitch\n" +
                    "4. **Notifications**: Server gets notified of new clips and highlights", false)
                .AddField("**Features**",
                    "• **Automatic Clip Detection** - New clips posted automatically\n" +
                    "• **Thread Organization** - Each clip gets its own discussion thread\n" +
                    "• **Stream Notifications** - Alerts when you go live\n" +
                    "• **Statistics Tracking** - View count, clip performance", false)
                .WithFooter("Twitch integration requires admin setup and valid Twitch account");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("helpbump")]
        [Alias("helpybump")]
        [Summary("Show detailed bump reminder help")]
        public async Task HelpBumpAsync()
        {
            var embed = new EmbedBuilder()
                .WithTitle("🔔 **Bump Reminder Help**")
                .WithColor(0x7289DA)
                .AddField("**Admin Setup Commands**",
                    "`!setbumpreminder` - Set a 2-hour bump reminder\n" +
                    "`!getbumpreminder` - Remove the active bump reminder\n" +
                    "`!bumpreminder` - Activate bump reminder (alias)\n" +
                    "`!bumpreminderdet` - Deactivate bump reminder (alias)", false)
                .AddField("**Status & Information**",
                    "`!bumpstatus` - Show bump reminder status\n" +
                    "`!bumphelp` - Show bump system help\n" +
                    "`!bumptime` - Time until next bump allowed", false)
                .AddField("**How Bump Reminders Work**",
                    "1. **Auto-Detection**: Bot detects when you use `/bump` with Disboard\n" +
                    "2. **Timer**: Automatically sets 2-hour reminder\n" +
                    "3. **Notification**: Sends reminder when bump cooldown is over\n" +
                    "4. **Multi-Language**: Supports German, English, and other languages", false)
                .AddField("**Supported Languages**",
                    "🇩🇪 German • 🇺🇸 English • 🇫🇷 French • 🇪🇸 Spanish • 🇮🇹 Italian • 🇵🇹 Portuguese", false)
                .AddField("**Features**",
                    "• **Auto-Detection** - Recognizes Disboard bump confirmations\n" +
                    "• **Smart Timing** - Exactly 2 hours after successful bump\n" +
                    "• **Multi-Channel** - Works in any channel where bump was used\n" +
                    "• **Persistence** - Survives bot restarts", false)
                .WithFooter("Bump reminders help you maintain server visibility on Disboard");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("helpbirth")]
        [Alias("helpybirth")]
        [Summary("Show detailed birthday system help")]
        public async Task HelpBirthdayAsync()
        {
            var embed = new EmbedBuilder()
                .WithTitle("🎂 **Birthday System Help**")
                .WithColor(0xFFD700)
                .AddField("**Admin Setup Commands**",
                    "`!birthdaychannel` - Set the birthday notification channel\n" +
                    "`!birthdaylist` - Show all birthdays in the server\n" +
                    "`!birthdaystats` - Show birthday system statistics", false)
                .AddField("**User Birthday Commands**",
                    "`!birthdayset [dd/mm/yyyy]` - Set your birthday\n" +
                    "`!birthdayremove` - Remove your birthday from the system\n" +
                    "`!mybirthdayinfo` - Show your saved birthday info", false)
                .AddField("**How Birthday System Works**",
                    "1. **Setup**: Admin sets notification channel with `!birthdaychannel`\n" +
                    "2. **Registration**: Users save their birthdays with `!birthdayset`\n" +
                    "3. **Auto-Notify**: Bot checks daily and sends birthday wishes\n" +
                    "4. **Privacy**: Only day/month shown in lists, year is private", false)
                .AddField("**Date Format**",
                    "**Required format**: `dd/mm/yyyy`\n" +
                    "**Examples**: `25/12/1995`, `03/07/2000`, `15/04/1988`\n" +
                    "**Note**: Year is saved but not displayed publicly", false)
                .AddField("**Features**",
                    "• **Daily Checking** - Automatic birthday detection\n" +
                    "• **Privacy Protection** - Only day/month displayed\n" +
                    "• **Persistent Data** - Survives bot restarts\n" +
                    "• **Auto-Delete** - User commands auto-delete for privacy\n" +
                    "• **List View** - See all server birthdays organized by date", false)
                .WithFooter("Birthday system respects user privacy and only shows day/month publicly");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("helpall")]
        [Alias("helpyall")]
        [Summary("Show complete command overview")]
        public async Task HelpAllAsync()
        {
            // This just redirects to the main help command
            var mainCommands = new MainCommands();
            await mainCommands.HelpAsync();
        }
    }
}