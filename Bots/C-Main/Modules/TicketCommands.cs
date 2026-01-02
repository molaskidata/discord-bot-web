using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace MainbotCSharp.Modules
{
    // Ticket Service Classes
    public class TicketConfigEntry { public ulong LogChannelId { get; set; } }

    public static class TicketService
    {
        private const string TICKETS_CONFIG_FILE = "tickets_config.json";
        private static Dictionary<ulong, TicketConfigEntry> _cfg = LoadTicketsConfig();
        public static ConcurrentDictionary<ulong, TicketMeta> TicketMetas = new();

        private static Dictionary<ulong, TicketConfigEntry> LoadTicketsConfig()
        {
            try
            {
                if (!File.Exists(TICKETS_CONFIG_FILE)) return new Dictionary<ulong, TicketConfigEntry>();
                var txt = File.ReadAllText(TICKETS_CONFIG_FILE);
                var d = JsonSerializer.Deserialize<Dictionary<ulong, TicketConfigEntry>>(txt);
                return d ?? new Dictionary<ulong, TicketConfigEntry>();
            }
            catch { return new Dictionary<ulong, TicketConfigEntry>(); }
        }

        private static void SaveTicketsConfig()
        {
            try
            {
                var txt = JsonSerializer.Serialize(_cfg, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(TICKETS_CONFIG_FILE, txt);
            }
            catch { }
        }

        public static TicketConfigEntry GetConfig(ulong guildId)
        {
            if (_cfg.TryGetValue(guildId, out var e)) return e; return null;
        }

        public static void SetConfig(ulong guildId, TicketConfigEntry cfg)
        {
            _cfg[guildId] = cfg; SaveTicketsConfig();
        }

        public class TicketMeta
        {
            public ulong UserId { get; set; }
            public string Category { get; set; }
            public ulong GuildId { get; set; }
        }

        public static async Task<bool> SaveTranscriptAsync(SocketGuild guild, Discord.ITextChannel channel, TicketMeta meta)
        {
            try
            {
                var messages = await channel.GetMessagesAsync(100).FlattenAsync();
                var transcript = string.Join('\n', messages.Reverse().Select(m => $"[{m.Timestamp}] {m.Author} ({m.Author.Id}): {m.Content}"));
                var filename = $"ticket_{meta.GuildId}_{channel.Id}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.txt";
                try { System.IO.File.WriteAllText(filename, transcript); } catch { }

                var cfg = GetConfig(meta.GuildId);
                if (cfg != null)
                {
                    var logChan = guild.GetTextChannel(cfg.LogChannelId);
                    if (logChan != null)
                    {
                        try { await logChan.SendFileAsync(filename, $"Ticket transcript from {channel.Name} (created by <@{meta.UserId}>):"); } catch { }
                    }
                }

                try { System.IO.File.Delete(filename); } catch { }
                return true;
            }
            catch { return false; }
        }
    }

    public class TicketCommands : ModuleBase<SocketCommandContext>
    {
        [Command("munga-ticketsystem")]
        [Summary("Setup ticket system (Admin only)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetupTicketSystemAsync()
        {
            try
            {
                var config = new TicketConfigEntry { LogChannelId = Context.Channel.Id };
                TicketService.SetConfig(Context.Guild.Id, config);

                var embed = new EmbedBuilder()
                    .WithTitle("🎫 Support System")
                    .WithDescription("Need help? Select a category below to open a support ticket:")
                    .WithColor(Color.Blue)
                    .AddField("Available Categories",
                        "🔧 Technical Issue\n" +
                        "🚨 Spam / Scam\n" +
                        "⚠️ Abuse / Harassment\n" +
                        "📢 Advertising / Recruitment\n" +
                        "🐛 Bug / Feature Request\n" +
                        "❓ Other", false)
                    .WithFooter("Select from the dropdown below");

                var menu = new SelectMenuBuilder()
                    .WithPlaceholder("Choose your issue category")
                    .WithCustomId("support_select")
                    .AddOption("Technical Issue", "support_technical", "Technical problems or questions", new Emoji("🔧"))
                    .AddOption("Spam / Scam", "support_spam", "Report spam or scam content", new Emoji("🚨"))
                    .AddOption("Abuse / Harassment", "support_abuse", "Report abusive behavior", new Emoji("⚠️"))
                    .AddOption("Advertising / Recruitment", "support_ad", "Unauthorized advertising", new Emoji("📢"))
                    .AddOption("Bug / Feature Request", "support_bug", "Report bugs or suggest features", new Emoji("🐛"))
                    .AddOption("Other", "support_other", "Other issues not listed above", new Emoji("❓"));

                var components = new ComponentBuilder()
                    .WithSelectMenu(menu);

                await ReplyAsync(embed: embed.Build(), components: components.Build());
                await ReplyAsync($"✅ Ticket system configured! Log channel: <#{Context.Channel.Id}>");
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Setup failed: {ex.Message}");
            }
        }

        [Command("ticket-status")]
        [Summary("Check ticket system status (Admin only)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task TicketStatusAsync()
        {
            try
            {
                var config = TicketService.GetConfig(Context.Guild.Id);
                var activeTickets = TicketService.TicketMetas.Count(t => t.Value.GuildId == Context.Guild.Id);

                var embed = new EmbedBuilder()
                    .WithTitle("🎫 Ticket System Status")
                    .WithColor(Color.Blue)
                    .AddField("Log Channel", config != null ? $"<#{config.LogChannelId}>" : "Not configured", true)
                    .AddField("Active Tickets", activeTickets.ToString(), true);

                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Failed to get status: {ex.Message}");
            }
        }
    }
}
