using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace MainbotCSharp.Services
{
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

        public MonitorService()
        {
            LoadState();
        }

        private void LoadState()
        {
            try { if (File.Exists(STATE_FILE)) _state = JsonSerializer.Deserialize<MonitorState>(File.ReadAllText(STATE_FILE)) ?? new MonitorState(); }
            catch { _state = new MonitorState(); }
        }

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
                        .AddField("Status", (res.status == "ONLINE" ? "🟢" : res.status == "STANDBY" ? "🟠" : res.status == "🔴") + " " + res.status, true)
                        .WithCurrentTimestamp();

                    if (_state.messages.TryGetValue(t.key, out var mid))
                    {
                        var msgId = ulong.Parse(mid);
                        var msg = await channel.GetMessageAsync(msgId) as IUserMessage;
                        if (msg != null && msg.Editable) await msg.ModifyAsync(m => m.Embed = embed.Build());
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
                    var pres = found.Activity != null || found.Status != UserStatus.Offline ? "online" : "offline";
                    var now = DateTimeOffset.UtcNow;
                    if (pres != "offline") { statusLabel = pres == "online" ? "ONLINE" : "STANDBY"; lastSeen = now.ToString("o"); _state.lastSeen[hints[0]] = lastSeen; SaveState(); return (statusLabel, lastSeen); }
                    if (_state.lastSeen.TryGetValue(hints[0], out var ls) && (DateTimeOffset.UtcNow - DateTimeOffset.Parse(ls)).TotalMinutes < 5) statusLabel = "CRASHED";
                }
                return (statusLabel, lastSeen);
            }
            catch { return ("OFFLINE", null); }
        }
    }
}
