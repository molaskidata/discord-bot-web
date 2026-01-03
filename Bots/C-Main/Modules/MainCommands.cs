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
                .WithTitle("🤖 Bot Commands")
                .WithColor(Color.Blue)
                .WithDescription("Alle verfügbaren Befehle:")
                .AddField("📋 Allgemeine Befehle",
                    "**!help** - Diese Hilfe anzeigen\n" +
                    "**!info** - Bot-Informationen\n" +
                    "**!ping** - Bot-Latenz prüfen\n" +
                    "**!gn** - Gute Nacht Nachricht\n" +
                    "**!gm** - Guten Morgen Nachricht", true)

                .AddField("🔔 Bump System",
                    "**!bumpreminder on/off** - Bump-Erinnerungen aktivieren/deaktivieren\n" +
                    "**!bumpstatus** - Status der Bump-Erinnerungen prüfen", true)
                .AddField("🎫 Tickets",
                    "**!ticket create** - Neues Ticket erstellen\n" +
                    "**!ticket close** - Ticket schließen", true)
                .AddField("🔒 Sicherheit",
                    "**!security** - Sicherheitsstatus anzeigen\n" +
                    "**!scan** - Server auf verdächtige Aktivitäten prüfen", true)
                .AddField("✅ Verifizierung",
                    "**!verify** - Verifizierungsprozess starten", true)
                .WithFooter("Bot entwickelt mit Discord.NET")
                .WithCurrentTimestamp();

            await ReplyAsync(embed: embed.Build());
        }

        [Command("info")]
        [Summary("Shows bot information")]
        public async Task InfoAsync()
        {
            var embed = new EmbedBuilder()
                .WithTitle("🤖 Bot Information")
                .WithColor(Color.Green)
                .AddField("Bot Name", Context.Client.CurrentUser.Username, true)
                .AddField("Server", Context.Guild.Name, true)
                .AddField("Online seit", Context.Client.CurrentUser.CreatedAt.ToString("dd.MM.yyyy HH:mm"), true)
                .AddField("Latenz", $"{Context.Client.Latency}ms", true)
                .AddField("Framework", "Discord.NET", true)
                .AddField("Sprache", "C#", true)
                .WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl())
                .WithFooter("Made with ❤️")
                .WithCurrentTimestamp();

            await ReplyAsync(embed: embed.Build());
        }

        [Command("gn")]
        [Summary("Good night message")]
        public async Task GoodNightAsync()
        {
            var messages = new[]
            {
                "Gute Nacht! 🌙 Schlaf gut!",
                "Süße Träume! 😴💤",
                "Schlaf schön! 🌙✨",
                "Gute Nacht und erholsame Ruhe! 😌",
                "Träum was Schönes! 🌙💫"
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
                "Guten Morgen! ☀️ Einen schönen Tag!",
                "Moin! 🌅 Gut geschlafen?",
                "Guten Morgen! ☕ Bereit für einen neuen Tag?",
                "Morgen! 🌞 Hoffe du bist fit!",
                "Guten Morgen! 🌻 Lass uns den Tag rocken!"
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
                await ReplyAsync("📝 Verwendung: `!bumpreminder on` oder `!bumpreminder off`");
                return;
            }

            if (action.ToLower() == "on")
            {
                BumpReminderService.EnableReminders(Context.Channel.Id);
                await ReplyAsync("✅ Bump-Erinnerungen wurden aktiviert! Du wirst benachrichtigt, wenn der Server wieder gebumpt werden kann.");
            }
            else if (action.ToLower() == "off")
            {
                BumpReminderService.DisableReminders(Context.Channel.Id);
                await ReplyAsync("❌ Bump-Erinnerungen wurden deaktiviert.");
            }
            else
            {
                await ReplyAsync("❌ Ungültige Option. Verwende `on` oder `off`.");
            }
        }

        [Command("bumpstatus")]
        [Summary("Check bump reminder status")]
        public async Task BumpStatusAsync()
        {
            var status = BumpReminderService.GetReminderStatus(Context.Channel.Id);

            var embed = new EmbedBuilder()
                .WithTitle("📊 Bump Reminder Status")
                .WithColor(status.enabled ? Color.Green : Color.Red)
                .AddField("Status", status.enabled ? "✅ Aktiviert" : "❌ Deaktiviert", true)
                .AddField("Kanal", Context.Channel.Name, true);

            if (status.enabled && status.nextBumpTime.HasValue)
            {
                var timeRemaining = status.nextBumpTime.Value - DateTime.UtcNow;
                if (timeRemaining.TotalSeconds > 0)
                {
                    embed.AddField("Nächster Bump möglich in",
                        $"{timeRemaining.Hours}h {timeRemaining.Minutes}m {timeRemaining.Seconds}s", false);
                }
                else
                {
                    embed.AddField("Nächster Bump", "Jetzt möglich! 🎉", false);
                }
            }

            embed.WithCurrentTimestamp();
            await ReplyAsync(embed: embed.Build());
        }
    }
}