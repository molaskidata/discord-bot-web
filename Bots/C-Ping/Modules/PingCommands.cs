using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace PingbotCSharp.Modules
{
    // Ping Service Classes
    public class PingService
    {
        private readonly ConcurrentDictionary<string, System.Threading.Timer> _reminders = new();
        private const string BUMP_FILE = "bump_reminders_ping.json";

        public async Task StartAsync(DiscordSocketClient client)
        {
            await Task.Yield();
            RestoreBumpReminders(client);
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

        private class StoredReminder { public ulong GuildId { get; set; } public long TriggerTime { get; set; } }
    }

    public class PingCommands : ModuleBase<SocketCommandContext>
    {
        private readonly PingService _svc;
        public PingCommands(IServiceProvider svc)
        {
            _svc = svc.GetService(typeof(PingService)) as PingService;
        }

        [Command("pingme")]
        public async Task PingMe()
        {
            await ReplyAsync("!ponggg");
        }
            [Command("phelp")]
            public async Task PHelp()
            {
                await ReplyAsync("PingBot — Help\n`!pingme` - Basic ping/pong check\nBump commands: `!setbumpreminder2`, `!delbumpreminder2`, `!bumpstatus`, `!bumphelp`");
            }
        }
    }
    

