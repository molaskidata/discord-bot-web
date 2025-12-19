using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Discord;
using MainbotCSharp.Services;

namespace MainbotCSharp.Modules
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

            var filter = new Func<SocketMessage, bool>(m => m.Author.Id == Context.User.Id);
            var interactivity = Context.Client as DiscordSocketClient;
            try
            {
                var response = await NextMessageAsync(filter, TimeSpan.FromSeconds(60));
                if (response == null) { await ReplyAsync("⌛ Timeout: no channel provided. Run the command again to set logging."); return; }
                var val = response.Content.Trim();
                if (string.Equals(val, "none", StringComparison.OrdinalIgnoreCase)) { cfg.LogChannelId = null; SecurityService.SetConfig(guildId, cfg); await ReplyAsync("✅ Security enabled with no logging."); return; }
                var idStr = System.Text.RegularExpressions.Regex.Replace(val, "[^0-9]", "");
                if (ulong.TryParse(idStr, out var cid))
                {
                    var ch = Context.Guild.GetTextChannel(cid);
                    if (ch == null) { await ReplyAsync("❌ Channel not found. Make sure I can access it."); return; }
                    cfg.LogChannelId = cid; SecurityService.SetConfig(guildId, cfg);
                    await ReplyAsync($"✅ Warn log channel set to {ch}.");
                    return;
                }
                await ReplyAsync("❌ Invalid input. Provide a channel ID or 'none'.");
            }
            catch (Exception ex)
            {
                await ReplyAsync("Error while waiting for channel: " + ex.Message);
            }
        }

        [Command("status")]
        public async Task SecurityStatusAsync()
        {
            var gid = Context.Guild.Id; var cfg = SecurityService.GetConfig(gid);
            await ReplyAsync($"Security: {(cfg.Enabled ? "ENABLED" : "disabled")}. Log channel: {(cfg.LogChannelId.HasValue ? $"<# {cfg.LogChannelId.Value}>" : "none")}.");
        }

        [Command("setlogchannel")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetLogChannelAsync(ulong channelId)
        {
            var gid = Context.Guild.Id; var cfg = SecurityService.GetConfig(gid);
            cfg.LogChannelId = channelId; SecurityService.SetConfig(gid, cfg);
            await ReplyAsync($"Set security log channel to <#{channelId}>");
        }

        // Helper: get the next message from the invoking user (simple polling)
        private async Task<SocketMessage> NextMessageAsync(Func<SocketMessage, bool> filter, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<SocketMessage?>();
            Func<SocketMessage, Task> handler = (SocketMessage msg) =>
            {
                try { if (filter(msg)) tcs.TrySetResult(msg); } catch { }
                return Task.CompletedTask;
            };
            Context.Client.MessageReceived += handler;
            var task = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
            Context.Client.MessageReceived -= handler;
            if (task == tcs.Task) return tcs.Task.Result!;
            return null;
        }
    }
}
