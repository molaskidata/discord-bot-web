using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace MainbotCSharp.Services
{
    public class VerifyConfigEntry
    {
        public ulong ChannelId { get; set; }
        public ulong RoleId { get; set; }
        public ulong? MessageId { get; set; }
        public Dictionary<ulong, bool?> Snapshot { get; set; } = new Dictionary<ulong, bool?>();
    }

    public static class VerifyService
    {
        private const string VERIFY_FILE = "main_verify_config.json";
        private static Dictionary<ulong, VerifyConfigEntry> _cfg = LoadVerifyConfig();

        private static Dictionary<ulong, VerifyConfigEntry> LoadVerifyConfig()
        {
            try
            {
                if (!File.Exists(VERIFY_FILE)) return new Dictionary<ulong, VerifyConfigEntry>();
                var txt = File.ReadAllText(VERIFY_FILE);
                var d = JsonSerializer.Deserialize<Dictionary<ulong, VerifyConfigEntry>>(txt);
                return d ?? new Dictionary<ulong, VerifyConfigEntry>();
            }
            catch
            {
                return new Dictionary<ulong, VerifyConfigEntry>();
            }
        }

        private static void SaveVerifyConfig()
        {
            try
            {
                var txt = JsonSerializer.Serialize(_cfg, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(VERIFY_FILE, txt);
            }
            catch { }
        }

        public static VerifyConfigEntry GetConfig(ulong guildId)
        {
            if (_cfg.TryGetValue(guildId, out var e)) return e; return null;
        }

        public static void SetConfig(ulong guildId, VerifyConfigEntry config)
        {
            _cfg[guildId] = config; SaveVerifyConfig();
        }

        public static void RemoveConfig(ulong guildId)
        {
            if (_cfg.ContainsKey(guildId)) { _cfg.Remove(guildId); SaveVerifyConfig(); }
        }
    }
}
