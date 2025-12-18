using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Discord.WebSocket;

namespace PiratBotCSharp.Services
{
    public class TicketConfigEntry { public ulong LogChannelId { get; set; } }

    public static class TicketService
    {
        private const string TICKETS_CONFIG_FILE = "pirate_tickets_config.json";
        private const string TICKET_META_FILE = "pirate_ticket_meta.json";
        private static Dictionary<ulong, TicketConfigEntry> _cfg = LoadTicketsConfig();

        public static ConcurrentDictionary<ulong, TicketMeta> TicketMeta = new();

        static TicketService()
        {
            LoadTicketMeta();
        }

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

        // Persist in-memory ticket metadata
        private static void LoadTicketMeta()
        {
            try
            {
                if (!File.Exists(TICKET_META_FILE)) return;
                var txt = File.ReadAllText(TICKET_META_FILE);
                var d = JsonSerializer.Deserialize<Dictionary<ulong, TicketMeta>>(txt);
                if (d == null) return;
                foreach (var kv in d) TicketMeta.TryAdd(kv.Key, kv.Value);
            }
            catch { }
        }

        private static readonly object _metaSaveLock = new object();
        private static void SaveTicketMeta()
        {
            try
            {
                lock (_metaSaveLock)
                {
                    var copy = new Dictionary<ulong, TicketMeta>(TicketMeta);
                    var txt = JsonSerializer.Serialize(copy, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(TICKET_META_FILE, txt);
                }
            }
            catch { }
        }

        public static void AddMeta(ulong channelId, TicketMeta meta)
        {
            TicketMeta[channelId] = meta; SaveTicketMeta();
        }

        public static void RemoveMeta(ulong channelId)
        {
            TicketMeta.TryRemove(channelId, out _); SaveTicketMeta();
        }

        public class TicketMeta
        {
            public ulong UserId { get; set; }
            public string Category { get; set; }
            public ulong GuildId { get; set; }
        }
    }
}
