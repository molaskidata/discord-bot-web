using System;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Discord;
using PiratBotCSharp.Services;

namespace PiratBotCSharp.Modules
{
    [Group("security")]
    public class SecurityCommands : ModuleBase<SocketCommandContext>
    {
        [Command("setsecuritymod")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetSecurityModAsync()
        {
            var guildId = Context.Guild.Id;
            var cfg = SecurityService.GetConfig(guildId);
            if (cfg.Enabled) { await ReplyAsync("⚠️ Security system is already enabled for this server."); return; }
            cfg.Enabled = true; SecurityService.SetConfig(guildId, cfg);
            await ReplyAsync("🛡️ Security system has been enabled for this server! Please provide the CHANNEL ID where I should send warn logs (type `none` to disable logging).");

            var response = await NextMessageAsync(m => m.Author.Id == Context.User.Id, TimeSpan.FromSeconds(60));
            if (response == null) { await ReplyAsync("⌛ Timeout: no channel provided. Run the command again to set logging."); return; }
            var val = response.Content.Trim();
            if (string.Equals(val, "none", StringComparison.OrdinalIgnoreCase)) { cfg.LogChannelId = null; SecurityService.SetConfig(guildId, cfg); await ReplyAsync("✅ Security enabled with no logging."); return; }
            var idStr = System.Text.RegularExpressions.Regex.Replace(val, "[^0-9]", "");
            if (!ulong.TryParse(idStr, out var cid)) { await ReplyAsync("❌ Invalid input. Provide a channel ID or 'none'."); return; }
            var ch = Context.Guild.GetTextChannel(cid);
            if (ch == null) { await ReplyAsync("❌ Channel not found. Make sure I can access it."); return; }
            cfg.LogChannelId = cid; SecurityService.SetConfig(guildId, cfg);
            await ReplyAsync($"✅ Warn log channel set to {ch}.");
        }

        [Command("security")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SecurityAsync(string arg = null)
        {
            var gid = Context.Guild.Id; var cfg = SecurityService.GetConfig(gid);
            if (string.IsNullOrWhiteSpace(arg) || arg == "status")
            {
                await ReplyAsync($"Security: {(cfg.Enabled ? "ENABLED" : "disabled")}. Log channel: {(cfg.LogChannelId.HasValue ? cfg.LogChannelId.Value.ToString() : "none")}."); return;
            }
            if (arg.Equals("on", StringComparison.OrdinalIgnoreCase) || arg.Equals("enable", StringComparison.OrdinalIgnoreCase))
            {
                cfg.Enabled = true; SecurityService.SetConfig(gid, cfg); await ReplyAsync("✅ Security enabled for this server."); return;
            }
            if (arg.Equals("off", StringComparison.OrdinalIgnoreCase) || arg.Equals("disable", StringComparison.OrdinalIgnoreCase))
            {
                cfg.Enabled = false; SecurityService.SetConfig(gid, cfg); await ReplyAsync("✅ Security disabled for this server."); return;
            }
            await ReplyAsync("Usage: !security <on|off|status>");
        }

        private async Task<SocketMessage> NextMessageAsync(Func<SocketMessage, bool> filter, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<SocketMessage?>();
            Task Handler(SocketMessage msg)
            {
                try { if (filter(msg)) tcs.TrySetResult(msg); } catch { }
                return Task.CompletedTask;
            }
            void OnMsg(SocketMessage m) => Handler(m);
            Context.Client.MessageReceived += OnMsg;
            var task = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
            Context.Client.MessageReceived -= OnMsg;
            if (task == tcs.Task) return tcs.Task.Result!;
            return null;
        }
    }
}
