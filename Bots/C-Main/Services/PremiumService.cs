using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace MainbotCSharp.Services
{
    public class PremiumService
    {
        private static readonly string PremiumFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "premium_guilds.json");
        private static Dictionary<ulong, PremiumGuild> _premiumGuilds = new Dictionary<ulong, PremiumGuild>();

        public class PremiumGuild
        {
            public ulong GuildId { get; set; }
            public ulong OwnerId { get; set; }
            public string SubscriptionType { get; set; } // "monthly" or "yearly"
            public DateTime ActivatedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
            public bool IsActive { get; set; }
            public string? StripeCustomerId { get; set; }
            public string? StripeSubscriptionId { get; set; }
        }

        static PremiumService()
        {
            LoadPremiumGuilds();
        }

        private static void LoadPremiumGuilds()
        {
            try
            {
                if (File.Exists(PremiumFilePath))
                {
                    var json = File.ReadAllText(PremiumFilePath);
                    var guilds = JsonSerializer.Deserialize<List<PremiumGuild>>(json);
                    if (guilds != null)
                    {
                        _premiumGuilds = guilds.ToDictionary(g => g.GuildId);
                        Console.WriteLine($"✅ Loaded {_premiumGuilds.Count} premium guilds");
                    }
                }
                else
                {
                    Console.WriteLine("ℹ️  No premium guilds file found, starting fresh");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading premium guilds: {ex.Message}");
            }
        }

        private static void SavePremiumGuilds()
        {
            try
            {
                var json = JsonSerializer.Serialize(_premiumGuilds.Values.ToList(), new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(PremiumFilePath, json);
                Console.WriteLine("✅ Premium guilds saved");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error saving premium guilds: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a guild has active premium
        /// </summary>
        public static bool HasPremium(ulong guildId)
        {
            if (!_premiumGuilds.TryGetValue(guildId, out var guild))
                return false;

            // Check if expired
            if (guild.ExpiresAt < DateTime.UtcNow)
            {
                guild.IsActive = false;
                SavePremiumGuilds();
                return false;
            }

            return guild.IsActive;
        }

        /// <summary>
        /// Get premium guild info
        /// </summary>
        public static PremiumGuild? GetPremiumGuild(ulong guildId)
        {
            return _premiumGuilds.TryGetValue(guildId, out var guild) ? guild : null;
        }

        /// <summary>
        /// Activate premium for a guild (manual or via Stripe webhook)
        /// </summary>
        public static void ActivatePremium(ulong guildId, ulong ownerId, string subscriptionType, string? stripeCustomerId = null, string? stripeSubscriptionId = null)
        {
            var duration = subscriptionType == "yearly" ? 365 : 30;
            var expiresAt = DateTime.UtcNow.AddDays(duration);

            var premiumGuild = new PremiumGuild
            {
                GuildId = guildId,
                OwnerId = ownerId,
                SubscriptionType = subscriptionType,
                ActivatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                IsActive = true,
                StripeCustomerId = stripeCustomerId,
                StripeSubscriptionId = stripeSubscriptionId
            };

            _premiumGuilds[guildId] = premiumGuild;
            SavePremiumGuilds();

            Console.WriteLine($"✅ Premium activated for guild {guildId} ({subscriptionType}) until {expiresAt}");
        }

        /// <summary>
        /// Deactivate premium (cancellation or expiration)
        /// </summary>
        public static void DeactivatePremium(ulong guildId)
        {
            if (_premiumGuilds.TryGetValue(guildId, out var guild))
            {
                guild.IsActive = false;
                SavePremiumGuilds();
                Console.WriteLine($"❌ Premium deactivated for guild {guildId}");
            }
        }

        /// <summary>
        /// Extend premium subscription (renewal)
        /// </summary>
        public static void ExtendPremium(ulong guildId)
        {
            if (_premiumGuilds.TryGetValue(guildId, out var guild))
            {
                var duration = guild.SubscriptionType == "yearly" ? 365 : 30;
                guild.ExpiresAt = guild.ExpiresAt.AddDays(duration);
                guild.IsActive = true;
                SavePremiumGuilds();
                Console.WriteLine($"✅ Premium extended for guild {guildId} until {guild.ExpiresAt}");
            }
        }

        /// <summary>
        /// Get all premium guilds (for admin dashboard)
        /// </summary>
        public static List<PremiumGuild> GetAllPremiumGuilds()
        {
            return _premiumGuilds.Values.ToList();
        }
    }
}
