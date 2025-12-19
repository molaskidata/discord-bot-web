using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace PingbotCSharp.Modules
{
    public class PingCommands : ModuleBase<SocketCommandContext>
    {
        private readonly PingbotCSharp.Services.PingService _svc;
        public PingCommands(IServiceProvider svc)
        {
            _svc = svc.GetService(typeof(PingbotCSharp.Services.PingService)) as PingbotCSharp.Services.PingService;
        }

        [Command("pingme")]
        public async Task PingMe()
        {
            await ReplyAsync("!ponggg");
        }

        [Command("setbumpreminder2")]
        [RequireUserPermission(Discord.GuildPermissions.Administrator)]
        public async Task SetBumpReminder()
        {
            _svc?.SetBumpReminder(Context.Client as DiscordSocketClient, Context.Channel.Id, Context.Guild.Id);
            await ReplyAsync("✅ Bump reminder set! I'll remind you in 2 hours when the next bump is available.");
        }

        [Command("delbumpreminder2")]
        [RequireUserPermission(Discord.GuildPermissions.Administrator)]
        public async Task DelBumpReminder()
        {
            var ok = _svc?.CancelBumpReminder(Context.Channel.Id) ?? false;
            if (ok) await ReplyAsync("🗑️ Bump reminder for this channel has been deleted."); else await ReplyAsync("❌ No active bump reminder for this channel.");
        }

        [Command("bumpstatus")]
        [RequireUserPermission(Discord.GuildPermissions.Administrator)]
        public async Task BumpStatus()
        {
            var has = _svc?.HasReminder(Context.Channel.Id) ?? false;
            if (has) await ReplyAsync("⏳ Bump reminder is active for this channel. You'll be notified when the next bump is available."); else await ReplyAsync("❌ No active bump reminder for this channel. Use `!setbumpreminder2` to set one manually.");
        }

        [Command("bumphelp")]
        public async Task BumpHelp()
        {
            await ReplyAsync("**🤖 Bump Reminder System Help**\n\nAutomatic Detection: I automatically detect when you use `/bump` and set a 2-hour reminder!\n\nManual Commands:\n`!setbumpreminder2` - Manually set a 2-hour bump reminder (admin only)\n`!bumpstatus` - Check if there's an active reminder for this channel (admin only)\n`!bumphelp` - Show this help message");
        }

        [Command("phelp")]
        public async Task PHelp()
        {
            await ReplyAsync("PingBot — Help\n`!pingme` - Basic ping/pong check\nBump commands: `!setbumpreminder2`, `!delbumpreminder2`, `!bumpstatus`, `!bumphelp`");
        }
    }
}
