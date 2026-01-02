using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace MainbotCSharp.Services
{
    public class BirthdayData
    {
        public ulong ChannelId { get; set; }
        public Dictionary<ulong, string> Users { get; set; } = new Dictionary<ulong, string>();
    }

    public static class BirthdayService
    {
        private const string BIRTHDAYS_FILE = "birthdays.json";
        private static Dictionary<ulong, BirthdayData> _guildBirthdays = LoadBirthdayData();
        private static System.Threading.Timer _dailyTimer;
        private static DiscordSocketClient _client;

        private static Dictionary<ulong, BirthdayData> LoadBirthdayData()
        {
            try
            {
                if (!File.Exists(BIRTHDAYS_FILE))
                    return new Dictionary<ulong, BirthdayData>();

                var json = File.ReadAllText(BIRTHDAYS_FILE);
                return JsonSerializer.Deserialize<Dictionary<ulong, BirthdayData>>(json) ?? new Dictionary<ulong, BirthdayData>();
            }
            catch
            {
                return new Dictionary<ulong, BirthdayData>();
            }
        }

        private static void SaveBirthdayData()
        {
            try
            {
                var json = JsonSerializer.Serialize(_guildBirthdays, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(BIRTHDAYS_FILE, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Birthday save error: " + ex);
            }
        }

        public static void Initialize(DiscordSocketClient client)
        {
            _client = client;

            // Start daily birthday check timer (every hour)
            _dailyTimer = new System.Threading.Timer(async _ =>
            {
                try
                {
                    await CheckBirthdaysAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Birthday check error: " + ex);
                }
            }, null, TimeSpan.Zero, TimeSpan.FromHours(1));

            Console.WriteLine("BirthdayService initialized");
        }

        public static void SetBirthdayChannel(ulong guildId, ulong channelId)
        {
            if (!_guildBirthdays.TryGetValue(guildId, out var data))
            {
                data = new BirthdayData();
                _guildBirthdays[guildId] = data;
            }

            data.ChannelId = channelId;
            SaveBirthdayData();
        }

        public static bool SetUserBirthday(ulong guildId, ulong userId, string birthday)
        {
            // Validate date format dd/mm/yyyy
            if (!Regex.IsMatch(birthday, @"^\d{2}\/\d{2}\/\d{4}$"))
                return false;

            // Try to parse to validate it's a real date
            try
            {
                var parts = birthday.Split('/');
                var day = int.Parse(parts[0]);
                var month = int.Parse(parts[1]);
                var year = int.Parse(parts[2]);

                // Basic validation
                if (day < 1 || day > 31 || month < 1 || month > 12 || year < 1900 || year > DateTime.Now.Year)
                    return false;

                // Try to create DateTime to validate
                new DateTime(year, month, day);
            }
            catch
            {
                return false;
            }

            if (!_guildBirthdays.TryGetValue(guildId, out var data))
            {
                data = new BirthdayData();
                _guildBirthdays[guildId] = data;
            }

            data.Users[userId] = birthday;
            SaveBirthdayData();
            return true;
        }

        public static bool RemoveUserBirthday(ulong guildId, ulong userId)
        {
            if (!_guildBirthdays.TryGetValue(guildId, out var data))
                return false;

            var removed = data.Users.Remove(userId);
            if (removed)
                SaveBirthdayData();

            return removed;
        }

        public static BirthdayData GetGuildBirthdayData(ulong guildId)
        {
            return _guildBirthdays.TryGetValue(guildId, out var data) ? data : null;
        }

        private static async Task CheckBirthdaysAsync()
        {
            if (_client == null) return;

            var today = DateTime.Now;
            var todayString = today.ToString("dd/MM");

            foreach (var kvp in _guildBirthdays)
            {
                var guildId = kvp.Key;
                var data = kvp.Value;

                if (data.ChannelId == 0) continue; // No channel set

                try
                {
                    var guild = _client.GetGuild(guildId);
                    if (guild == null) continue;

                    var channel = guild.GetTextChannel(data.ChannelId);
                    if (channel == null) continue;

                    // Check each user's birthday
                    foreach (var userKvp in data.Users)
                    {
                        var userId = userKvp.Key;
                        var birthday = userKvp.Value;

                        // Extract day/month from stored birthday (dd/mm/yyyy)
                        var birthdayDayMonth = birthday.Substring(0, 5); // Gets dd/MM

                        if (birthdayDayMonth == todayString)
                        {
                            try
                            {
                                await channel.SendMessageAsync($"🎉 **Happy Birthdayyyy** <@{userId}>! 🎂🎈");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to send birthday message for user {userId}: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Birthday check error for guild {guildId}: {ex.Message}");
                }
            }
        }
    }
}