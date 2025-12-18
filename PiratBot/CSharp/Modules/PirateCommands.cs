using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using System.IO;
using System.Text.Json;

namespace PiratBotCSharp.Modules
{
    public class PirateCommands : ModuleBase<SocketCommandContext>
    {
        private static readonly ConcurrentDictionary<string, PlayerData> Players = new();
        private static readonly ConcurrentDictionary<string, BattleGame> Battles = new();
        private static readonly ConcurrentDictionary<string, long> LastMine = new();
        private static readonly ConcurrentDictionary<string, long> LastRaid = new();

        private const string GAMES_FILE = "pirate_games.json";

        static PirateCommands()
        {
            LoadGameData();
        }

        private static void LoadGameData()
        {
            try
            {
                if (!File.Exists(GAMES_FILE)) return;
                var txt = File.ReadAllText(GAMES_FILE);
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var gd = JsonSerializer.Deserialize<GameData>(txt, opts);
                if (gd == null) return;
                Players.Clear(); Battles.Clear();
                if (gd.players != null)
                {
                    foreach (var kv in gd.players) Players.TryAdd(kv.Key, kv.Value);
                }
                if (gd.battles != null)
                {
                    foreach (var kv in gd.battles) Battles.TryAdd(kv.Key, kv.Value);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to load game data: " + ex.Message);
            }
        }

        private static void SaveGameData()
        {
            try
            {
                var gd = new GameData { players = new System.Collections.Generic.Dictionary<string, PlayerData>(), battles = new System.Collections.Generic.Dictionary<string, BattleGame>() };
                foreach (var kv in Players) gd.players[kv.Key] = kv.Value;
                foreach (var kv in Battles) gd.battles[kv.Key] = kv.Value;
                var txt = JsonSerializer.Serialize(gd, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(GAMES_FILE, txt);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to save game data: " + ex.Message);
            }
        }

        [Command("mine")]
        public async Task MineAsync()
        {
            var pid = Context.User.Id.ToString();
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (LastMine.TryGetValue(pid, out var t) && now - t < 60_000)
            {
                await ReplyAsync("You must wait 60s between mines.");
                return;
            }
            var rnd = new Random();
            var found = rnd.Next(5, 25);
            var p = Players.GetOrAdd(pid, _ => new PlayerData());
            p.Gold += found;
            Players[pid] = p;
            LastMine[pid] = now;
            SaveGameData();
            await ReplyAsync($"⛏️ You mined {found} gold! Current gold: {p.Gold}");
        }

        [Command("gold")]
        public async Task GoldAsync()
        {
            var pid = Context.User.Id.ToString();
            var p = Players.GetOrAdd(pid, _ => new PlayerData());
            await ReplyAsync($"🏴‍☠️ You have **{p.Gold}** gold stored on your ship.");
        }

        [Command("raid")]
        public async Task RaidAsync(string mentionOrId = null)
        {
            if (string.IsNullOrWhiteSpace(mentionOrId)) { await ReplyAsync("Usage: !raid @user or !raid <userId>"); return; }
            var targetId = ParseUserId(mentionOrId);
            if (targetId == null) { await ReplyAsync("User not found."); return; }
            if (targetId == Context.User.Id.ToString()) { await ReplyAsync("You cannot raid yourself."); return; }

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var attackerId = Context.User.Id.ToString();
            if (LastRaid.TryGetValue(attackerId, out var t) && now - t < 2 * 60_000) { await ReplyAsync("Raid cooldown: 2 minutes."); return; }
            var defender = Players.GetOrAdd(targetId, _ => new PlayerData());
            if (defender.Gold <= 0) { await ReplyAsync("Target has no gold to steal."); return; }
            var rnd = new Random();
            var success = rnd.NextDouble() < 0.5;
            var attacker = Players.GetOrAdd(attackerId, _ => new PlayerData());
            if (success)
            {
                var stolen = Math.Max(1, (int)Math.Floor(defender.Gold * (rnd.NextDouble() * 0.15 + 0.05)));
                defender.Gold = Math.Max(0, defender.Gold - stolen);
                attacker.Gold += stolen;
                attacker.LastRaid = now;
                Players[attackerId] = attacker;
                Players[targetId] = defender;
                LastRaid[attackerId] = now;
                SaveGameData();
                await ReplyAsync($"🏴‍☠️ Raid successful! You stole **{stolen}** gold from <@{targetId}>.");
            }
            else
            {
                attacker.LastRaid = now; Players[attackerId] = attacker; LastRaid[attackerId] = now;
                SaveGameData();
                await ReplyAsync($"💥 Raid failed! <@{targetId}> defended their ship.");
            }
        }

        [Command("bs")]
        public async Task BsAsync([Remainder] string args = null)
        {
            var parts = (args ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var sub = parts.Length > 0 ? parts[0].ToLower() : null;
            var data = Battles;
            if (sub == "start")
            {
                string targetMention = parts.Length > 1 ? parts[1] : null;
                var targetId = ParseUserId(targetMention);
                if (targetId == null) { await ReplyAsync("Usage: !bs start @user  OR  !bs start <userId>"); return; }
                if (targetId == Context.User.Id.ToString()) { await ReplyAsync("Cannot play against yourself."); return; }
                var gid = $"{Context.Guild.Id}_{Context.Channel.Id}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 10000}";
                var boardA = MakeEmptyBoard();
                var boardB = MakeEmptyBoard();
                PlaceRandomShips(boardA); PlaceRandomShips(boardB);
                var game = new BattleGame { PlayerA = Context.User.Id.ToString(), PlayerB = targetId, BoardA = boardA, BoardB = boardB, Turn = Context.User.Id.ToString(), Finished = false };
                Battles[gid] = game;
                SaveGameData();
                await ReplyAsync($"⚓ Battleship started between <@{Context.User.Id}> and <@{targetId}>! Game ID: `{gid}`. <@{Context.User.Id}> starts. Use `!bs attack A1` to fire.");
                return;
            }
            if (sub == "attack")
            {
                var coord = parts.Length > 1 ? parts[1] : null;
                if (coord == null) { await ReplyAsync("Usage: !bs attack A1"); return; }
                var gid = Battles.Keys.FirstOrDefault(k => !Battles[k].Finished && (Battles[k].PlayerA == Context.User.Id.ToString() || Battles[k].PlayerB == Context.User.Id.ToString()));
                if (gid == null) { await ReplyAsync("No active battles found for you. Start one with `!bs start @user`."); return; }
                var game = Battles[gid];
                if (game.Turn != Context.User.Id.ToString()) { await ReplyAsync("Not your turn."); return; }
                var idx = CoordToIndex(coord);
                if (idx == null) { await ReplyAsync("Invalid coordinate. Use A1..E5."); return; }
                var opponentBoard = game.PlayerA == Context.User.Id.ToString() ? game.BoardB : game.BoardA;
                var cell = opponentBoard[idx.Value.r, idx.Value.c];
                if (cell == 2 || cell == 3) { await ReplyAsync("Already attacked that coordinate."); return; }
                string reply = string.Empty;
                if (cell == 1)
                {
                    opponentBoard[idx.Value.r, idx.Value.c] = 3; reply = $"💥 Hit at {coord}!";
                }
                else
                {
                    opponentBoard[idx.Value.r, idx.Value.c] = 2; reply = $"🌊 Miss at {coord}.";
                }
                var opponentHasShips = false;
                for (int r = 0; r < 5; r++) for (int c = 0; c < 5; c++) if (opponentBoard[r, c] == 1) opponentHasShips = true;
                if (!opponentHasShips)
                {
                    game.Finished = true;
                    reply += $"\n🏴‍☠️ <@{Context.User.Id}> sank all ships and won!";
                    var pdata = Players.GetOrAdd(Context.User.Id.ToString(), _ => new PlayerData());
                    pdata.Gold += 50; Players[Context.User.Id.ToString()] = pdata;
                    reply += " You received 50 gold.";
                }
                else
                {
                    game.Turn = game.PlayerA == Context.User.Id.ToString() ? game.PlayerB : game.PlayerA;
                    reply += $"\nNext: <@{game.Turn}>";
                }
                Battles[gid] = game;
                SaveGameData();
                await ReplyAsync(reply);
                return;
            }
            await ReplyAsync("Battleship commands: `!bs start @user` or `!bs attack A1`.");
        }

        // helpers
        private static string ParseUserId(string mentionOrId)
        {
            if (string.IsNullOrWhiteSpace(mentionOrId)) return null;
            var s = mentionOrId.Trim();
            if (s.StartsWith("<@") && s.EndsWith(">"))
            {
                s = s.Trim('<', '@', '!', '>');
            }
            if (ulong.TryParse(s, out var id)) return id.ToString();
            return null;
        }

        private static int[,] MakeEmptyBoard()
        {
            return new int[5, 5];
        }

        private static void PlaceRandomShips(int[,] board, int[] sizes = null)
        {
            sizes ??= new int[] { 2, 2, 2 };
            var rnd = new Random();
            foreach (var size in sizes)
            {
                bool placed = false;
                for (int attempt = 0; attempt < 500 && !placed; attempt++)
                {
                    bool horiz = rnd.NextDouble() < 0.5;
                    int r = rnd.Next(0, 5);
                    int c = rnd.Next(0, 5);
                    var cells = new List<(int, int)>();
                    for (int i = 0; i < size; i++)
                    {
                        int rr = horiz ? r : r + i;
                        int cc = horiz ? c + i : c;
                        if (rr > 4 || cc > 4) { cells.Clear(); break; }
                        cells.Add((rr, cc));
                    }
                    if (cells.Count == 0) continue;
                    if (cells.Any(cell => board[cell.Item1, cell.Item2] == 1)) continue;
                    foreach (var cell in cells) board[cell.Item1, cell.Item2] = 1;
                    placed = true;
                }
            }
        }

        private static (int r, int c)? CoordToIndex(string coord)
        {
            if (string.IsNullOrWhiteSpace(coord)) return null;
            coord = coord.Trim().ToUpper();
            if (coord.Length < 2) return null;
            char rowChar = coord[0];
            if (rowChar < 'A' || rowChar > 'E') return null;
            if (!int.TryParse(coord[1..], out int col)) return null;
            if (col < 1 || col > 5) return null;
            int r = rowChar - 'A'; int c = col - 1; return (r, c);
        }

        private class PlayerData { public int Gold = 0; public long LastRaid = 0; }
        private class BattleGame { public string PlayerA; public string PlayerB; public int[,] BoardA; public int[,] BoardB; public string Turn; public bool Finished; }
    }
}
