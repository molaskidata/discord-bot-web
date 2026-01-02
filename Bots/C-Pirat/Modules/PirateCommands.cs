using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace PiratBotCSharp.Modules
{
    // === PIRATE GAME DATA ===
    public class PiratePlayerData
    {
        public ulong UserId { get; set; }
        public string Username { get; set; } = "";
        public int Gold { get; set; } = 100;
        public int CrewSize { get; set; } = 1;
        public List<string> Inventory { get; set; } = new();
        public DateTime LastMining { get; set; } = DateTime.MinValue;
        public DateTime LastRaid { get; set; } = DateTime.MinValue;
    }

    public class BattleshipGame
    {
        public ulong PlayerId { get; set; }
        public ulong OpponentId { get; set; }
        public Dictionary<string, bool> PlayerHits { get; set; } = new();
        public Dictionary<string, bool> OpponentHits { get; set; } = new();
        public bool IsPlayerTurn { get; set; } = true;
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
    }

    // === MAINBOT SERVICES COPIED ===
    public class SecurityConfigEntry { public bool Enabled { get; set; } = false; public ulong? LogChannelId { get; set; } = null; }
    public class TicketConfigEntry { public ulong LogChannelId { get; set; } }

    public class VoiceConfig
    {
        public ulong? JoinToCreateChannel { get; set; }
        public ulong? JoinToCreateCategory { get; set; }
        public ulong? VoiceChannelCategory { get; set; }
        public ulong? VoiceLogChannel { get; set; }
        public Dictionary<ulong, ActiveChannelInfo> ActiveChannels { get; set; } = new();
        public Dictionary<string, VoiceTemplate> Templates { get; set; } = new()
        {
            { "gaming", new VoiceTemplate{ Name = "🎮 Gaming Cabin", Limit = 0 } },
            { "study", new VoiceTemplate{ Name = "📚 Study Quarters", Limit = 4 } },
            { "chill", new VoiceTemplate{ Name = "💤 Chill Bay", Limit = 0 } },
            { "custom", new VoiceTemplate{ Name = "🏴‍☠️ Pirate Cabin", Limit = 0 } }
        };
    }

    public class ActiveChannelInfo { public ulong OwnerId { get; set; } public long CreatedAt { get; set; } public string Template { get; set; } public bool IsPrivate { get; set; } }
    public class VoiceTemplate { public string Name { get; set; } public int Limit { get; set; } }

    public class VoiceLogs
    {
        public List<VoiceLogEntry> Logs { get; set; } = new();
        public Dictionary<string, VoiceStats> Stats { get; set; } = new();
    }
    public class VoiceLogEntry { public string UserId; public string Username; public string Action; public string ChannelName; public string Timestamp; }
    public class VoiceStats { public string Username; public int TotalJoins; public long TotalTime; public int ChannelsCreated; }

    public static class PirateService
    {
        private const string PIRATE_DATA_FILE = "pirate_players.json";
        private const string BATTLESHIP_FILE = "battleship_games.json";
        private const string SECURITY_FILE = "pirate_security.json";
        private const string VOICE_CONFIG_FILE = "pirate_voice_config.json";
        private const string VOICE_LOG_FILE = "pirate_voice_logs.json";
        private const string TICKETS_CONFIG_FILE = "pirate_tickets_config.json";

        private static Dictionary<ulong, PiratePlayerData> _players = LoadPirateData();
        private static Dictionary<ulong, BattleshipGame> _battleships = LoadBattleshipData();
        private static Dictionary<ulong, SecurityConfigEntry> _security = LoadSecurityConfig();
        private static VoiceConfig _voiceConfig = LoadVoiceConfig();
        private static VoiceLogs _voiceLogs = LoadVoiceLogs();
        private static Dictionary<ulong, TicketConfigEntry> _tickets = LoadTicketsConfig();

        public static ConcurrentDictionary<ulong, Dictionary<ulong, long>> AfkTracker = new();
        public static ConcurrentDictionary<ulong, TicketMeta> TicketMetas = new();

        private static readonly string[] WordLists = new[] {
            "anal","anus","arsch","boobs","clit","dick","fuck","fucking","hure","nackt","nudes","nipple","porn","pussy","sex","slut","tits","vagina",
            "bastard","idiot","dumm","retard","go die","kill yourself","kys","suicide","self harm","spam","discordgift","freenitro"
        };

        private static readonly HashSet<ulong> PREMIUM_USERS = new() { 1105877268775051316ul };

        // === DATA LOADING METHODS ===
        private static Dictionary<ulong, PiratePlayerData> LoadPirateData()
        {
            try
            {
                if (!File.Exists(PIRATE_DATA_FILE)) return new();
                var json = File.ReadAllText(PIRATE_DATA_FILE);
                return JsonSerializer.Deserialize<Dictionary<ulong, PiratePlayerData>>(json) ?? new();
            }
            catch { return new(); }
        }

        private static void SavePirateData()
        {
            try
            {
                var json = JsonSerializer.Serialize(_players, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(PIRATE_DATA_FILE, json);
            }
            catch { }
        }

        private static Dictionary<ulong, BattleshipGame> LoadBattleshipData()
        {
            try
            {
                if (!File.Exists(BATTLESHIP_FILE)) return new();
                var json = File.ReadAllText(BATTLESHIP_FILE);
                return JsonSerializer.Deserialize<Dictionary<ulong, BattleshipGame>>(json) ?? new();
            }
            catch { return new(); }
        }

        private static void SaveBattleshipData()
        {
            try
            {
                var json = JsonSerializer.Serialize(_battleships, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(BATTLESHIP_FILE, json);
            }
            catch { }
        }

        private static Dictionary<ulong, SecurityConfigEntry> LoadSecurityConfig()
        {
            try
            {
                if (!File.Exists(SECURITY_FILE)) return new Dictionary<ulong, SecurityConfigEntry>();
                var txt = File.ReadAllText(SECURITY_FILE);
                var d = JsonSerializer.Deserialize<Dictionary<ulong, SecurityConfigEntry>>(txt);
                return d ?? new Dictionary<ulong, SecurityConfigEntry>();
            }
            catch { return new Dictionary<ulong, SecurityConfigEntry>(); }
        }

        private static void SaveSecurityConfig()
        {
            try
            {
                var txt = JsonSerializer.Serialize(_security, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SECURITY_FILE, txt);
            }
            catch { }
        }

        private static VoiceConfig LoadVoiceConfig()
        {
            try
            {
                if (!File.Exists(VOICE_CONFIG_FILE)) return new VoiceConfig();
                var txt = File.ReadAllText(VOICE_CONFIG_FILE);
                return JsonSerializer.Deserialize<VoiceConfig>(txt) ?? new VoiceConfig();
            }
            catch { return new VoiceConfig(); }
        }

        private static void SaveVoiceConfig()
        {
            try
            {
                var txt = JsonSerializer.Serialize(_voiceConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(VOICE_CONFIG_FILE, txt);
            }
            catch (Exception ex) { Console.WriteLine("Failed to save voice config: " + ex); }
        }

        private static VoiceLogs LoadVoiceLogs()
        {
            try
            {
                if (!File.Exists(VOICE_LOG_FILE)) return new VoiceLogs();
                var txt = File.ReadAllText(VOICE_LOG_FILE);
                return JsonSerializer.Deserialize<VoiceLogs>(txt) ?? new VoiceLogs();
            }
            catch { return new VoiceLogs(); }
        }

        private static void SaveVoiceLogs()
        {
            try
            {
                var txt = JsonSerializer.Serialize(_voiceLogs, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(VOICE_LOG_FILE, txt);
            }
            catch (Exception ex) { Console.WriteLine("Failed to save voice logs: " + ex); }
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
                var txt = JsonSerializer.Serialize(_tickets, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(TICKETS_CONFIG_FILE, txt);
            }
            catch { }
        }

        // === PUBLIC METHODS ===

        // Player Management
        public static PiratePlayerData GetOrCreatePlayer(ulong userId, string username)
        {
            if (!_players.TryGetValue(userId, out var player))
            {
                player = new PiratePlayerData { UserId = userId, Username = username };
                _players[userId] = player;
                SavePirateData();
            }
            return player;
        }

        public static void UpdatePlayer(PiratePlayerData player)
        {
            _players[player.UserId] = player;
            SavePirateData();
        }

        // Battleship Management
        public static void StartBattleship(ulong playerId, ulong opponentId)
        {
            var game = new BattleshipGame { PlayerId = playerId, OpponentId = opponentId };
            _battleships[playerId] = game;
            SaveBattleshipData();
        }

        public static BattleshipGame GetBattleship(ulong playerId)
        {
            return _battleships.TryGetValue(playerId, out var game) ? game : null;
        }

        public static void UpdateBattleship(BattleshipGame game)
        {
            _battleships[game.PlayerId] = game;
            SaveBattleshipData();
        }

        public static void RemoveBattleship(ulong playerId)
        {
            _battleships.Remove(playerId);
            SaveBattleshipData();
        }

        // Security Management
        public static void SetSecurityConfig(ulong guildId, SecurityConfigEntry entry)
        {
            _security[guildId] = entry; SaveSecurityConfig();
        }

        public static SecurityConfigEntry GetSecurityConfig(ulong guildId)
        {
            if (_security.TryGetValue(guildId, out var e)) return e; return new SecurityConfigEntry();
        }

        // Voice Management
        public static VoiceConfig GetVoiceConfig() => _voiceConfig;
        public static void SaveVoiceConfigData() => SaveVoiceConfig();
        public static bool IsPremiumUser(ulong id) => PREMIUM_USERS.Contains(id);

        private static void AddVoiceLog(ulong userId, string username, string action, string channelName)
        {
            _voiceLogs.Logs.Add(new VoiceLogEntry { UserId = userId.ToString(), Username = username, Action = action, ChannelName = channelName, Timestamp = DateTime.UtcNow.ToString("o") });
            if (!_voiceLogs.Stats.ContainsKey(userId.ToString())) _voiceLogs.Stats[userId.ToString()] = new VoiceStats { Username = username };
            var s = _voiceLogs.Stats[userId.ToString()];
            s.Username = username;
            if (action == "join") s.TotalJoins++;
            if (action == "create") s.ChannelsCreated++;
            SaveVoiceLogs();
        }

        // Tickets Management
        public static TicketConfigEntry GetTicketConfig(ulong guildId)
        {
            if (_tickets.TryGetValue(guildId, out var e)) return e; return null;
        }

        public static void SetTicketConfig(ulong guildId, TicketConfigEntry cfg)
        {
            _tickets[guildId] = cfg; SaveTicketsConfig();
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
                var filename = $"pirate_ticket_{meta.GuildId}_{channel.Id}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.txt";
                try { System.IO.File.WriteAllText(filename, transcript); } catch { }

                var cfg = GetTicketConfig(meta.GuildId);
                if (cfg != null)
                {
                    var logChan = guild.GetTextChannel(cfg.LogChannelId);
                    if (logChan != null)
                    {
                        try { await logChan.SendFileAsync(filename, $"🏴‍☠️ Ticket transcript from {channel.Name} (created by <@{meta.UserId}>):"); } catch { }
                    }
                }

                try { System.IO.File.Delete(filename); } catch { }
                return true;
            }
            catch { return false; }
        }

        // Security Message Handler
        public static async Task HandleMessageAsync(SocketMessage rawMessage)
        {
            try
            {
                if (!(rawMessage is SocketUserMessage message)) return;
                if (message.Author.IsBot) return;
                if (!(message.Channel is SocketTextChannel tchan)) return;
                var guild = tchan.Guild;
                if (guild == null) return;

                var cfg = GetSecurityConfig(guild.Id);
                if (!cfg.Enabled) return;

                var content = (message.Content ?? string.Empty).ToLowerInvariant();
                var guildUser = message.Author as SocketGuildUser;
                if (guildUser != null && (guildUser.GuildPermissions.Administrator)) return;

                var inviteRegex = new Regex(@"(discord\.gg\/|discordapp\.com\/invite\/|discord\.com\/invite\/)", RegexOptions.IgnoreCase);
                if (inviteRegex.IsMatch(content)) { await ReportAndDelete(message, guild, "Unauthorized treasure map (invite link)", "invite link"); return; }

                if (Regex.IsMatch(content, @"([a-zA-Z0-9])\1{6,}") || Regex.IsMatch(content, @"(.)\s*\1{6,}")) { await ReportAndDelete(message, guild, "Parrot spam detected", "spam"); return; }

                foreach (var w in WordLists)
                {
                    if (content.Contains(w)) { await ReportAndDelete(message, guild, $"Foul language from landlubber: {w}", w); return; }
                }

                if (message.Attachments != null && message.Attachments.Count > 0)
                {
                    foreach (var att in message.Attachments)
                    {
                        var name = (att.Filename ?? "").ToLowerInvariant();
                        if (Regex.IsMatch(name, "(nude|nudes|porn|dick|boobs|sex|pussy|tits|vagina|penis|clit|anal|nsfw|xxx|18\\+)"))
                        {
                            await ReportAndDelete(message, guild, "Inappropriate treasure", name);
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("PirateSecurityService error: " + ex);
            }
        }

        private static async Task ReportAndDelete(SocketUserMessage message, SocketGuild guild, string reason, string matched)
        {
            try
            {
                var cfg = GetSecurityConfig(guild.Id);
                if (cfg.LogChannelId.HasValue)
                {
                    var chId = cfg.LogChannelId.Value;
                    var ch = guild.GetTextChannel(chId);
                    if (ch != null)
                    {
                        var eb = new EmbedBuilder().WithTitle("🏴‍☠️ Pirate Security Alert").WithColor(0x8B0000)
                            .AddField("Scallywag", $"{message.Author} ({message.Author.Id})", true)
                            .AddField("Violation", reason, true)
                            .AddField("Evidence", matched ?? (message.Content ?? "—"), false)
                            .WithFooter("Fair winds and following seas! | Made by mungabee")
                            .WithTimestamp(DateTimeOffset.UtcNow);
                        try { await ch.SendMessageAsync(text: $"⚠️ Security event in {guild.Name} ({guild.Id})", embed: eb.Build()); } catch { }
                    }
                }

                try { await message.DeleteAsync(); } catch { }
                try { await message.Author.SendMessageAsync($"Ye've been flagged by Captain Mary for: {reason}. If ye think this be a mistake, contact the ship's crew, matey! 🏴‍☠️"); } catch { }

                try
                {
                    var log = new
                    {
                        time = DateTimeOffset.UtcNow,
                        guildId = guild.Id,
                        guildName = guild.Name,
                        userId = message.Author.Id,
                        userTag = message.Author.Username,
                        action = reason,
                        matched = matched,
                        content = message.Content
                    };
                    File.AppendAllText("pirate_security_logs.jsonl", JsonSerializer.Serialize(log) + "\n");
                }
                catch { }
            }
            catch { }
        }
    }

    public class PirateCommands : ModuleBase<SocketCommandContext>
    {
        private static readonly Random _random = new();

        private EmbedBuilder CreatePirateEmbed()
        {
            return new EmbedBuilder()
                .WithColor(0x8B0000) // Dark red
                .WithFooter("Fair winds and following seas! | Made by mungabee");
        }

        // === GREETINGS ===
        [Command("ahoy")]
        [Summary("Pirate greeting")]
        public async Task AhoyAsync()
        {
            var responses = new[]
            {
                "Ahoy there, ye scallywag! 🏴‍☠️",
                "Ahoy, matey! Ready to sail the seven seas? ⚓",
                "Ahoy! Welcome aboard me ship, ye landlubber! 🚢",
                "Ahoy! May the wind be at yer back, sailor! ⛵"
            };

            var embed = CreatePirateEmbed()
                .WithDescription(responses[_random.Next(responses.Length)]);

            await ReplyAsync(embed: embed.Build());
        }

        [Command("farewell")]
        [Summary("Pirate farewell")]
        public async Task FarewellAsync()
        {
            var responses = new[]
            {
                "Farewell, ye hearty! May yer treasure chest always be full! 💰",
                "Until we meet again on the high seas, matey! 🌊",
                "Farewell! Keep yer cutlass sharp and yer rum cold! 🍺⚔️",
                "Fair winds to ye, sailor! Don't let the kraken get ye! 🐙"
            };

            var embed = CreatePirateEmbed()
                .WithDescription(responses[_random.Next(responses.Length)]);

            await ReplyAsync(embed: embed.Build());
        }

        // === CORE/INFO ===
        [Command("helpme")]
        [Summary("Full help (this message)")]
        public async Task HelpmePirateAsync()
        {
            var embed = CreatePirateEmbed()
                .WithTitle("PirateBot - Full Command List")
                .WithDescription("Ahoy, matey! This is the full command reference for PirateBot. Use `?piratehelp` for a short list.")
                .AddField("Greetings", "`?ahoy` — Pirate greeting\n`?farewell` — Pirate farewell", false)
                .AddField("Core / Info", "`?helpme` — Full help (this message)\n`?piratehelp` — Short reference\n`?piratecode` — Read the Pirate Code", false)
                .AddField("Fun & Games", "`?crew` — Show crew count\n`?dice` — Roll the dice\n`?compass` — Check direction\n`?games` — Open games menu (Battleship & Mine/Raid)\n`?bs_start @user | <userId>` — Start Battleship (mention or ID)\n`?bs_attack A1` — Attack a coordinate\n`?mine` — Mine gold\n`?gold` — Show your gold balance\n`?raid @user | <userId>` — Attempt to raid another player", false)
                .AddField("Security & Moderation (admins)", "`?setsecuritymod` — Enable security & set warn-log (interactive)\n`?ban @user` — Ban user\n`?kick @user` — Kick user\n`?timeout @user [minutes]` — Timeout user\n`?timeoutdel @user` — Remove timeout", false)
                .AddField("Voice System", "`?setupvoice` — Setup join-to-create channel (admin)\n`?setupvoicelog` — Create voice log channel (admin)\n`?voicename <name>` — Rename your private voice channel\n`?voicelimit <n>` — Set user limit for your channel\n`?voicelock / ?voiceunlock` — Lock/unlock your channel", false)
                .AddField("Tickets & Admin", "`?munga-supportticket` — Post support ticket selection menu\n`?munga-ticketsystem` — Configure ticket logging (admin)\n`?sendit` — Forward a message to another channel (admin)", false)
                .AddField("Misc / Troubleshooting", "`?ping` — Check bot latency (if present)\n\nIf a command fails, run it with the correct syntax or mention the user. For Battleship you can use a user mention or a user ID.", false)
                .WithImageUrl("https://imgur.com/Mmw4hAe");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("piratehelp")]
        [Summary("Short reference")]
        public async Task PirateHelpAsync()
        {
            var embed = CreatePirateEmbed()
                .WithTitle("🏴‍☠️ Quick Pirate Commands")
                .WithDescription("Ahoy! Here be the most important commands, ye scallywag!")
                .AddField("⚓ Basics", "`?ahoy` / `?farewell` — Greetings\n`?helpme` — Full command list", false)
                .AddField("🎮 Games", "`?games` — Game menu\n`?dice` — Roll dice\n`?gold` — Check treasure", false)
                .AddField("🔊 Voice", "`?voicename` — Rename cabin\n`?voicelock` — Lock/unlock", false);

            await ReplyAsync(embed: embed.Build());
        }

        [Command("piratecode")]
        [Summary("Read the Pirate Code")]
        public async Task PirateCodeAsync()
        {
            var embed = CreatePirateEmbed()
                .WithTitle("📜 The Pirate Code")
                .WithDescription("**Article I:** Every sailor has a vote in affairs of moment; has equal title to the fresh provisions.\n\n**Article II:** Every sailor to be called fairly in turn, by list, on board of prizes because they were on these expeditions.\n\n**Article III:** No person to game at cards or dice for money.\n\n**Article IV:** The lights and candles to be put out at eight bells.\n\n**Article V:** To keep their cutlass, pistols, and weapons clean and fit for service.\n\n*\"Take what ye can, give nothing back!\"* - Captain Jack Sparrow");

            await ReplyAsync(embed: embed.Build());
        }

        // === FUN & GAMES ===
        [Command("crew")]
        [Summary("Show crew count")]
        public async Task CrewAsync()
        {
            var player = PirateService.GetOrCreatePlayer(Context.User.Id, Context.User.Username);
            var embed = CreatePirateEmbed()
                .WithDescription($"Ahoy {Context.User.Mention}! Yer crew consists of **{player.CrewSize} brave sailors** ready for adventure! ⛵");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("dice")]
        [Summary("Roll the dice")]
        public async Task DiceAsync()
        {
            var roll = _random.Next(1, 7);
            var embed = CreatePirateEmbed()
                .WithDescription($"🎲 The dice be rolled! Ye got a **{roll}**!")
                .WithThumbnailUrl("https://via.placeholder.com/100x100/8B0000/FFFFFF?text=" + roll);

            await ReplyAsync(embed: embed.Build());
        }

        [Command("compass")]
        [Summary("Check direction")]
        public async Task CompassAsync()
        {
            var directions = new[] { "North ⬆️", "Northeast ↗️", "East ➡️", "Southeast ↘️", "South ⬇️", "Southwest ↙️", "West ⬅️", "Northwest ↖️" };
            var direction = directions[_random.Next(directions.Length)];

            var embed = CreatePirateEmbed()
                .WithDescription($"🧭 The compass points **{direction}**\n\n*\"Not all treasure is silver and gold, mate.\"*");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("games")]
        [Summary("Open games menu (Battleship & Mine/Raid)")]
        public async Task GamesAsync()
        {
            var embed = CreatePirateEmbed()
                .WithTitle("🎮 Pirate Games & Activities")
                .WithDescription("Choose yer adventure, ye scallywag!")
                .AddField("⚔️ Battleship", "`?bs_start @user` — Challenge someone to battle!\n`?bs_attack A1` — Attack coordinates (A-J, 1-10)", true)
                .AddField("💰 Treasure Hunt", "`?mine` — Mine for gold (cooldown: 1 hour)\n`?raid @user` — Raid another pirate's treasure!\n`?gold` — Check yer treasure chest", true)
                .AddField("🎲 Other Games", "`?dice` — Roll the dice\n`?compass` — Check wind direction\n`?crew` — See yer crew size", false)
                .WithImageUrl("https://imgur.com/61Cepa5");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("bs_start")]
        [Summary("Start Battleship (mention or ID)")]
        public async Task BattleshipStartAsync([Remainder] string target = "")
        {
            if (string.IsNullOrEmpty(target))
            {
                await ReplyAsync("Ye need to mention a user or provide their ID, matey! `?bs_start @user`");
                return;
            }

            ulong targetId = 0;
            if (Context.Message.MentionedUsers.Any())
            {
                targetId = Context.Message.MentionedUsers.First().Id;
            }
            else if (ulong.TryParse(target.Trim(), out var parsed))
            {
                targetId = parsed;
            }

            if (targetId == 0 || targetId == Context.User.Id)
            {
                await ReplyAsync("Ye can't battle yerself, ye scallywag! Find another pirate! 🏴‍☠️");
                return;
            }

            PirateService.StartBattleship(Context.User.Id, targetId);

            var embed = CreatePirateEmbed()
                .WithTitle("⚔️ Battleship Battle Started!")
                .WithDescription($"Ahoy {Context.User.Mention}! Ye've challenged <@{targetId}> to a naval battle!\n\nUse `?bs_attack A1` to attack coordinates (A-J, 1-10)\n\n*\"Prepare to be boarded!\"* 🏴‍☠️");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("bs_attack")]
        [Summary("Attack a coordinate")]
        public async Task BattleshipAttackAsync(string coordinate = "")
        {
            if (string.IsNullOrEmpty(coordinate))
            {
                await ReplyAsync("Where ye want to fire, captain? Use `?bs_attack A1` (A-J, 1-10)");
                return;
            }

            var game = PirateService.GetBattleship(Context.User.Id);
            if (game == null)
            {
                await ReplyAsync("Ye have no active battle! Start one with `?bs_start @user`");
                return;
            }

            var hit = _random.Next(1, 4) == 1; // 25% hit chance
            var result = hit ? "💥 **HIT!** Direct hit, ye skilled sea dog!" : "💧 **MISS!** The cannonball splashes into the sea!";

            var embed = CreatePirateEmbed()
                .WithTitle($"⚔️ Attacking {coordinate.ToUpper()}!")
                .WithDescription($"{result}\n\n*\"Yo ho ho and a bottle of rum!\"* 🍺");

            if (hit && _random.Next(1, 6) == 1) // 20% chance to sink ship on hit
            {
                embed.AddField("🏆 Victory!", $"Ye've sunk their ship! Well done, Captain {Context.User.Username}!");
                PirateService.RemoveBattleship(Context.User.Id);
            }

            await ReplyAsync(embed: embed.Build());
        }

        [Command("mine")]
        [Summary("Mine gold")]
        public async Task MineAsync()
        {
            var player = PirateService.GetOrCreatePlayer(Context.User.Id, Context.User.Username);

            if ((DateTime.UtcNow - player.LastMining).TotalHours < 1)
            {
                var timeLeft = TimeSpan.FromHours(1) - (DateTime.UtcNow - player.LastMining);
                await ReplyAsync($"Ye've already been mining recently, matey! Come back in {timeLeft.Minutes} minutes and {timeLeft.Seconds} seconds. ⛏️");
                return;
            }

            var goldFound = _random.Next(10, 51);
            player.Gold += goldFound;
            player.LastMining = DateTime.UtcNow;
            PirateService.UpdatePlayer(player);

            var embed = CreatePirateEmbed()
                .WithDescription($"⛏️ Ye've struck gold, {Context.User.Mention}!\n\nFound **{goldFound} gold coins** in the mines!\nYer treasure chest now holds **{player.Gold} gold**! 💰");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("gold")]
        [Summary("Show your gold balance")]
        public async Task GoldAsync()
        {
            var player = PirateService.GetOrCreatePlayer(Context.User.Id, Context.User.Username);
            var embed = CreatePirateEmbed()
                .WithDescription($"💰 Ahoy {Context.User.Mention}! Yer treasure chest contains **{player.Gold} gold coins**!\n\n*\"He who dies with the most toys wins!\"* 🏴‍☠️");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("raid")]
        [Summary("Attempt to raid another player")]
        public async Task RaidAsync([Remainder] string target = "")
        {
            if (string.IsNullOrEmpty(target))
            {
                await ReplyAsync("Who ye want to raid, matey? Mention them! `?raid @user`");
                return;
            }

            var player = PirateService.GetOrCreatePlayer(Context.User.Id, Context.User.Username);

            if ((DateTime.UtcNow - player.LastRaid).TotalHours < 2)
            {
                var timeLeft = TimeSpan.FromHours(2) - (DateTime.UtcNow - player.LastRaid);
                await ReplyAsync($"Ye've raided recently! Wait {timeLeft.Hours} hours and {timeLeft.Minutes} minutes before the next raid, ye scallywag! 🏴‍☠️");
                return;
            }

            ulong targetId = 0;
            if (Context.Message.MentionedUsers.Any())
            {
                targetId = Context.Message.MentionedUsers.First().Id;
            }
            else if (ulong.TryParse(target.Trim(), out var parsed))
            {
                targetId = parsed;
            }

            if (targetId == 0 || targetId == Context.User.Id)
            {
                await ReplyAsync("Ye can't raid yerself, ye landlubber! 🏴‍☠️");
                return;
            }

            var targetPlayer = PirateService.GetOrCreatePlayer(targetId, "Unknown Pirate");
            var success = _random.Next(1, 3) == 1; // 50% success rate

            player.LastRaid = DateTime.UtcNow;

            if (success && targetPlayer.Gold > 0)
            {
                var stolen = Math.Min(_random.Next(5, 26), targetPlayer.Gold);
                player.Gold += stolen;
                targetPlayer.Gold -= stolen;

                PirateService.UpdatePlayer(player);
                PirateService.UpdatePlayer(targetPlayer);

                var embed = CreatePirateEmbed()
                    .WithTitle("🏴‍☠️ Successful Raid!")
                    .WithDescription($"Ye've successfully raided <@{targetId}> and stolen **{stolen} gold**!\nYer treasure chest now has **{player.Gold} gold**! 💰\n\n*\"Take what ye can, give nothing back!\"*");

                await ReplyAsync(embed: embed.Build());
            }
            else
            {
                PirateService.UpdatePlayer(player);
                var embed = CreatePirateEmbed()
                    .WithTitle("💥 Raid Failed!")
                    .WithDescription($"Yer raid on <@{targetId}> failed! They fought ye off, ye scallywag!\n\n*\"Better luck next time, matey!\"* ⚔️");

                await ReplyAsync(embed: embed.Build());
            }
        }

        // === SECURITY & MODERATION ===
        [Command("setsecuritymod")]
        [Summary("Enable security & set warn-log (interactive)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetSecurityModAsync()
        {
            var config = new SecurityConfigEntry { Enabled = true, LogChannelId = Context.Channel.Id };
            PirateService.SetSecurityConfig(Context.Guild.Id, config);

            var embed = CreatePirateEmbed()
                .WithTitle("🛡️ Security System Enabled")
                .WithDescription($"Ahoy Captain! The security system be now active!\nWarn logs will be sent to {Context.Channel.Mention}\n\n*\"Keep a weather eye open, matey!\"*");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("ban")]
        [Summary("Ban user")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task BanAsync(IGuildUser user, [Remainder] string reason = "Walked the plank by Captain's orders")
        {
            try
            {
                await user.BanAsync(reason: reason);
                var embed = CreatePirateEmbed()
                    .WithDescription($"🏴‍☠️ **{user.Username}** has walked the plank! *Banned for: {reason}*");
                await ReplyAsync(embed: embed.Build());
            }
            catch
            {
                await ReplyAsync("Cannot make that scallywag walk the plank, Captain! Check me permissions!");
            }
        }

        [Command("kick")]
        [Summary("Kick user")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task KickAsync(IGuildUser user, [Remainder] string reason = "Thrown overboard by Captain's orders")
        {
            try
            {
                await user.KickAsync(reason);
                var embed = CreatePirateEmbed()
                    .WithDescription($"⚓ **{user.Username}** has been thrown overboard! *Kicked for: {reason}*");
                await ReplyAsync(embed: embed.Build());
            }
            catch
            {
                await ReplyAsync("Cannot throw that sailor overboard, Captain! Check me permissions!");
            }
        }

        [Command("timeout")]
        [Summary("Timeout user")]
        [RequireUserPermission(GuildPermission.ModerateMembers)]
        public async Task TimeoutAsync(IGuildUser user, int minutes = 60, [Remainder] string reason = "Sent to the brig by Captain's orders")
        {
            try
            {
                await user.SetTimeOutAsync(TimeSpan.FromMinutes(minutes), new RequestOptions { AuditLogReason = reason });
                var embed = CreatePirateEmbed()
                    .WithDescription($"🔒 **{user.Username}** has been sent to the brig for **{minutes} minutes**!\n*Reason: {reason}*");
                await ReplyAsync(embed: embed.Build());
            }
            catch
            {
                await ReplyAsync("Cannot send that sailor to the brig, Captain! Check me permissions!");
            }
        }

        [Command("timeoutdel")]
        [Summary("Remove timeout")]
        [RequireUserPermission(GuildPermission.ModerateMembers)]
        public async Task TimeoutDelAsync(IGuildUser user)
        {
            try
            {
                await user.RemoveTimeOutAsync();
                var embed = CreatePirateEmbed()
                    .WithDescription($"🔓 **{user.Username}** has been released from the brig! Back to work, ye scallywag!");
                await ReplyAsync(embed: embed.Build());
            }
            catch
            {
                await ReplyAsync("Cannot release that sailor from the brig, Captain!");
            }
        }

        // === VOICE SYSTEM ===
        [Command("setupvoice")]
        [Summary("Setup join-to-create channel (admin)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetupVoiceAsync()
        {
            var config = PirateService.GetVoiceConfig();
            config.JoinToCreateChannel = Context.Channel.Id;
            PirateService.SaveVoiceConfigData();

            var embed = CreatePirateEmbed()
                .WithTitle("🔊 Voice System Setup")
                .WithDescription("Voice system configured! When sailors join this channel, they'll get their own private cabin! ⛵");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("setupvoicelog")]
        [Summary("Create voice log channel (admin)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetupVoiceLogAsync()
        {
            var config = PirateService.GetVoiceConfig();
            config.VoiceLogChannel = Context.Channel.Id;
            PirateService.SaveVoiceConfigData();

            var embed = CreatePirateEmbed()
                .WithDescription($"🔊 Voice logs will now be sent to {Context.Channel.Mention}, Captain!");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("voicename")]
        [Summary("Rename your private voice channel")]
        public async Task VoiceNameAsync([Remainder] string name = "")
        {
            if (string.IsNullOrEmpty(name))
            {
                await ReplyAsync("What ye want to name yer cabin, sailor? `?voicename My Cabin`");
                return;
            }

            var embed = CreatePirateEmbed()
                .WithDescription($"🏴‍☠️ Yer cabin has been renamed to **{name}**, Captain!");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("voicelimit")]
        [Summary("Set user limit for your channel")]
        public async Task VoiceLimitAsync(int limit = 0)
        {
            var embed = CreatePirateEmbed()
                .WithDescription(limit == 0 ?
                    "🔊 Yer cabin now accepts unlimited crew members!" :
                    $"🔊 Yer cabin now limited to **{limit} sailors**, Captain!");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("voicelock")]
        [Summary("Lock your voice channel")]
        public async Task VoiceLockAsync()
        {
            var embed = CreatePirateEmbed()
                .WithDescription("🔒 Yer cabin is now locked! No more landlubbers allowed!");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("voiceunlock")]
        [Summary("Unlock your voice channel")]
        public async Task VoiceUnlockAsync()
        {
            var embed = CreatePirateEmbed()
                .WithDescription("🔓 Yer cabin is now open! All sailors welcome aboard!");

            await ReplyAsync(embed: embed.Build());
        }

        // === TICKETS & ADMIN ===
        [Command("munga-supportticket")]
        [Summary("Post support ticket selection menu")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SupportTicketAsync()
        {
            var embed = CreatePirateEmbed()
                .WithTitle("🎫 Pirate Support System")
                .WithDescription("Ahoy! Need help from the crew? Select yer issue below to open a support ticket:")
                .AddField("Available Categories",
                    "🔧 Technical Issue\n" +
                    "🚨 Spam / Scam\n" +
                    "⚠️ Abuse / Harassment\n" +
                    "📢 Advertising / Recruitment\n" +
                    "🐛 Bug / Feature Request\n" +
                    "❓ Other", false)
                .WithImageUrl("https://imgur.com/urvVA1h");

            var menu = new SelectMenuBuilder()
                .WithPlaceholder("Choose yer issue category, matey!")
                .WithCustomId("pirate_support_select")
                .AddOption("Technical Issue", "support_technical", "Technical problems or questions", new Emoji("🔧"))
                .AddOption("Spam / Scam", "support_spam", "Report spam or scam content", new Emoji("🚨"))
                .AddOption("Abuse / Harassment", "support_abuse", "Report abusive behavior", new Emoji("⚠️"))
                .AddOption("Advertising / Recruitment", "support_ad", "Unauthorized advertising", new Emoji("📢"))
                .AddOption("Bug / Feature Request", "support_bug", "Report bugs or suggest features", new Emoji("🐛"))
                .AddOption("Other", "support_other", "Other issues not listed above", new Emoji("❓"));

            var components = new ComponentBuilder()
                .WithSelectMenu(menu);

            await ReplyAsync(embed: embed.Build(), components: components.Build());
        }

        [Command("munga-ticketsystem")]
        [Summary("Configure ticket logging (admin)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task TicketSystemAsync()
        {
            var config = new TicketConfigEntry { LogChannelId = Context.Channel.Id };
            PirateService.SetTicketConfig(Context.Guild.Id, config);

            var embed = CreatePirateEmbed()
                .WithTitle("🎫 Ticket System Configured")
                .WithDescription($"Ahoy Captain! Ticket logs will be sent to {Context.Channel.Mention}!");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("sendit")]
        [Summary("Forward a message to another channel (admin)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SendItAsync([Remainder] string message = "")
        {
            if (string.IsNullOrEmpty(message))
            {
                await ReplyAsync("What message ye want to send, Captain? `?sendit #channel Your message here`");
                return;
            }

            var embed = CreatePirateEmbed()
                .WithDescription("Message sent across the seven seas, Captain! 🏴‍☠️");

            await ReplyAsync(embed: embed.Build());
        }

        // === MISC ===
        [Command("ping")]
        [Summary("Check bot latency (if present)")]
        public async Task PingAsync()
        {
            var latency = Context.Client is DiscordSocketClient c ? c.Latency : 0;
            var embed = CreatePirateEmbed()
                .WithDescription($"🏴‍☠️ Ahoy! Me response time be **{latency}ms**, Captain!\n\n*\"As swift as the Caribbean wind!\"* ⛵");

            await ReplyAsync(embed: embed.Build());
        }
    }
}