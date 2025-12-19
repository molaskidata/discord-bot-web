using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace PingbotCSharp.Services
{
    public class PingService
    {
        private readonly ConcurrentDictionary<string, System.Threading.Timer> _reminders = new();
        private const string BUMP_FILE = "bump_reminders_ping.json";

        public async Task StartAsync(DiscordSocketClient client)
        {
            await Task.Yield();
            RestoreBumpReminders(client);
            // send initial ping once and schedule recurring
            _ = Task.Run(async () =>
            {
                try
                {
                    await SendPingToMainBot(client);
                    var interval = TimeSpan.FromMinutes(90);
                    while (true)
                    {
                        await Task.Delay(interval);
                        await SendPingToMainBot(client);
                    }
                }
                catch (Exception ex) { Console.WriteLine("Ping loop error: " + ex); }
            });
        }

        private async Task SendPingToMainBot(DiscordSocketClient client)
        {
            try
            {
                var guild = client.GetGuild(1415044198792691858);
                if (guild == null) return;
                var channel = guild.GetTextChannel(1448640396359106672);
                if (channel == null) return;
                await channel.SendMessageAsync("!pingmeee");
            }
            catch (Exception ex) { Console.WriteLine("SendPing error: " + ex); }
        }

        public void SetBumpReminder(DiscordSocketClient client, ulong channelId, ulong guildId)
        {
            var key = channelId.ToString();
            if (_reminders.TryGetValue(key, out var existing))
            {
                existing.Change(Timeout.Infinite, Timeout.Infinite);
            }
            var trigger = TimeSpan.FromHours(2);
            var timer = new System.Threading.Timer(async _ =>
            {
                try
                {
                    var guild = client.GetGuild(guildId);
                    var chan = guild?.GetTextChannel(channelId);
                    if (chan != null) await chan.SendMessageAsync("⏰ **Bump Reminder!** ⏰\n\nThe server can be bumped again now! Use `/bump` to bump the server on Disboard! 🚀");
                }
                catch (Exception e) { Console.WriteLine("Reminder send failed: " + e); }
                finally
                {
                    _reminders.TryRemove(key, out _);
                    PersistRemove(key);
                }
            }, null, trigger, TimeSpan.FromMilliseconds(-1));

            _reminders[key] = timer;
            PersistAdd(key, guildId, DateTimeOffset.UtcNow.Add(trigger).ToUnixTimeMilliseconds());
        }

        public bool CancelBumpReminder(ulong channelId)
        {
            var key = channelId.ToString();
            if (_reminders.TryRemove(key, out var t))
            {
                t.Change(Timeout.Infinite, Timeout.Infinite);
                PersistRemove(key);
                return true;
            }
            return false;
        }

        public bool HasReminder(ulong channelId) => _reminders.ContainsKey(channelId.ToString());

        private void RestoreBumpReminders(DiscordSocketClient client)
        {
            try
            {
                if (!File.Exists(BUMP_FILE)) return;
                var json = File.ReadAllText(BUMP_FILE);
                var stored = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, StoredReminder>>(json) ?? new();
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                foreach (var kv in stored)
                {
                    var channelId = ulong.Parse(kv.Key);
                    var timeLeft = kv.Value.TriggerTime - now;
                    if (timeLeft <= 0) continue;
                    var timer = new System.Threading.Timer(async _ =>
                    {
                        try
                        {
                            var guild = client.GetGuild(kv.Value.GuildId);
                            var chan = guild?.GetTextChannel(channelId);
                            if (chan != null) await chan.SendMessageAsync("⏰ **Bump Reminder!** ⏰\n\nThe server can be bumped again now! Use `/bump` to bump the server on Disboard! 🚀");
                        }
                        catch (Exception e) { Console.WriteLine("Reminder send failed: " + e); }
                        finally { _reminders.TryRemove(kv.Key, out _); PersistRemove(kv.Key); }
                    }, null, TimeSpan.FromMilliseconds(timeLeft), TimeSpan.FromMilliseconds(-1));

                    _reminders[kv.Key] = timer;
                }
            }
            catch (Exception ex) { Console.WriteLine("Restore bump reminders failed: " + ex); }
        }

        private void PersistAdd(string channelKey, ulong guildId, long triggerTime)
        {
            try
            {
                var dict = new System.Collections.Generic.Dictionary<string, StoredReminder>();
                if (File.Exists(BUMP_FILE))
                {
                    var existing = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, StoredReminder>>(File.ReadAllText(BUMP_FILE));
                    if (existing != null) dict = existing;
                }
                dict[channelKey] = new StoredReminder { GuildId = guildId, TriggerTime = triggerTime };
                File.WriteAllText(BUMP_FILE, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void PersistRemove(string channelKey)
        {
            try
            {
                if (!File.Exists(BUMP_FILE)) return;
                var existing = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, StoredReminder>>(File.ReadAllText(BUMP_FILE));
                if (existing == null) return;
                if (existing.Remove(channelKey)) File.WriteAllText(BUMP_FILE, JsonSerializer.Serialize(existing, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private class StoredReminder { public ulong GuildId { get; set; } public long TriggerTime { get; set; } }
    }
}
