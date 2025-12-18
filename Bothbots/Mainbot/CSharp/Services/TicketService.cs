using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
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
    }
}
