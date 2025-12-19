using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace MainbotCSharp.Services
{
    public class TicketConfigEntry { public ulong LogChannelId { get; set; } }

    public static class TicketService
    {
        private const string TICKETS_CONFIG_FILE = "tickets_config.json";
        private static Dictionary<ulong, TicketConfigEntry> _cfg = LoadTicketsConfig();

        // in-memory metadata: ticketChannelId -> meta
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

        public static async Task<bool> SaveTranscriptAsync(DiscordSocketGuild guild, Discord.ITextChannel channel, TicketMeta meta)
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
}
