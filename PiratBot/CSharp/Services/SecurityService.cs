using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord;

namespace PiratBotCSharp.Services
{
    public class SecurityConfigEntry { public bool Enabled { get; set; } = false; public ulong? LogChannelId { get; set; } = null; }

    public static class SecurityService
    {
        private const string SECURITY_FILE = "pirate_security_config.json";
        private static Dictionary<ulong, SecurityConfigEntry> _config = LoadSecurityConfig();

        private static readonly string[] WordLists = new[] {
            "anal","anus","arsch","boobs","clit","dick","fuck","fucking","hure","nackt","nudes","nipple","porn","pussy","sex","slut","tits","vagina",
            "bastard","idiot","dumm","retard","go die","kill yourself","kys","suicide","self harm","spam","discordgift","freenitro"
        };

        private static Dictionary<ulong, SecurityConfigEntry> LoadSecurityConfig()
        {
            try
            {
                if (!File.Exists(SECURITY_FILE)) return new Dictionary<ulong, SecurityConfigEntry>();
                var txt = File.ReadAllText(SECURITY_FILE);
                var d = JsonSerializer.Deserialize<Dictionary<ulong, SecurityConfigEntry>>(txt);
                return d ?? new Dictionary<ulong, SecurityConfigEntry>();
            }
            catch
            {
                return new Dictionary<ulong, SecurityConfigEntry>();
            }
        }

        private static void SaveSecurityConfig()
        {
            try
            {
                var txt = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SECURITY_FILE, txt);
            }
            catch { }
        }

        public static void SetConfig(ulong guildId, SecurityConfigEntry entry)
        {
            _config[guildId] = entry; SaveSecurityConfig();
        }

        public static SecurityConfigEntry GetConfig(ulong guildId)
        {
            if (_config.TryGetValue(guildId, out var e)) return e; return new SecurityConfigEntry();
        }

        public static async Task HandleMessageAsync(SocketMessage rawMessage)
        {
            try
            {
                if (!(rawMessage is SocketUserMessage message)) return;
                if (message.Author.IsBot) return;
                if (!(message.Channel is SocketTextChannel tchan)) return;
                var guild = tchan.Guild;
                if (guild == null) return;

                var cfg = GetConfig(guild.Id);
                if (!cfg.Enabled) return;

                var content = (message.Content ?? string.Empty).ToLowerInvariant();
                var guildUser = message.Author as SocketGuildUser;
                if (guildUser != null && guildUser.GuildPermissions.Administrator) return;

                var inviteRegex = new Regex("(discord\\.gg/|discordapp\\.com/\\invite/|discord\\.com/\\invite/)", RegexOptions.IgnoreCase);
                if (inviteRegex.IsMatch(content)) { await ReportAndDelete(message, guild, "Invite link", "invite link"); return; }

                if (Regex.IsMatch(content, "([a-zA-Z0-9])\\1{6,}") || Regex.IsMatch(content, "(.)\\s*\\1{6,}")) { await ReportAndDelete(message, guild, "Spam detected", "spam"); return; }

                foreach (var w in WordLists)
                {
                    if (content.Contains(w)) { await ReportAndDelete(message, guild, $"Inappropriate language: {w}", w); return; }
                }

                if (message.Attachments != null && message.Attachments.Count > 0)
                {
                    foreach (var att in message.Attachments)
                    {
                        var name = (att.Filename ?? string.Empty).ToLowerInvariant();
                        if (Regex.IsMatch(name, "(nude|nudes|porn|dick|boobs|sex|pussy|tits|vagina|penis|clit|anal|nsfw|xxx|18\\+)"))
                        {
                            await ReportAndDelete(message, guild, "NSFW attachment", name); return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("SecurityService error: " + ex);
            }
        }

        private static async Task ReportAndDelete(SocketUserMessage message, SocketGuild guild, string reason, string matched)
        {
            try
            {
                var cfg = GetConfig(guild.Id);
                if (cfg.LogChannelId.HasValue)
                {
                    var ch = guild.GetTextChannel(cfg.LogChannelId.Value);
                    if (ch != null)
                    {
                        var eb = new EmbedBuilder().WithTitle("Security Alert").WithColor(Color.Orange)
                            .AddField("User", $"{message.Author} ({message.Author.Id})", true)
                            .AddField("Action", reason, true)
                            .AddField("Matched", matched ?? (message.Content ?? "—"), false)
                            .WithTimestamp(DateTimeOffset.UtcNow);
                        try { await ch.SendMessageAsync(text: $"Security event in {guild.Name} ({guild.Id})", embed: eb.Build()); } catch { }
                    }
                }

                try { await message.DeleteAsync(); } catch { }
                try { await message.Author.SendMessageAsync($"You have been flagged for: {reason}. If you think this is a mistake, contact the server staff."); } catch { }

                try
                {
                    var logEntry = new { time = DateTimeOffset.UtcNow, guildId = guild.Id, guildName = guild.Name, userId = message.Author.Id, userTag = message.Author.Username, action = reason, matched = matched, content = message.Content };
                    File.AppendAllText("security_logs_pirate.jsonl", JsonSerializer.Serialize(logEntry) + "\n");
                }
                catch { }
            }
            catch { }
        }
    }
}
