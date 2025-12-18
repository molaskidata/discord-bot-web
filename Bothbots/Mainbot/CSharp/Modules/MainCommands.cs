using System.Threading.Tasks;
using Discord.Commands;
using Discord;

namespace MainbotCSharp.Modules
{
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
            var eb = new EmbedBuilder()
                .WithTitle("Mainbot Help")
                .WithDescription("Available commands: !ping, !help. More commands will be ported.")
                .WithColor(Color.DarkBlue);
            await ReplyAsync(embed: eb.Build());
        }

        // Placeholder for security moderation trigger - this mimics the JS handler invocation
        [Command("debugsecurity")]
        [Summary("Run a quick security check on the message content (admin only)")]
        public async Task DebugSecurityAsync([Remainder] string text = null)
        {
            if (!Context.User.IsBot && Context.Guild != null && Context.Message.Member != null && !Context.Message.Member.Roles.Any())
            {
                // implement security checks when porting
            }
            await ReplyAsync("Security check placeholder (not implemented yet)");
        }
    }
}
