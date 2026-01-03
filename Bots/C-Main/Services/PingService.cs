using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace MainbotCSharp.Services
{
    public static class PingService
    {
        // Bot-to-Bot communication IDs from original infobot.js
        private const ulong PING_GUILD_ID = 1415044198792691858;
        private const ulong PING_CHANNEL_ID = 1448640396359106672;

        private static DiscordSocketClient _client;
        private static System.Threading.Timer _pingTimer;

        public static void Initialize(DiscordSocketClient client)
        {
            _client = client;

            // Start ping timer - send !pingme every 60 minutes to Pingbot
            _pingTimer = new System.Threading.Timer(async _ =>
            {
                await SendPingToPingBotAsync();
            }, null, TimeSpan.Zero, TimeSpan.FromMinutes(60));

            Console.WriteLine("PingService initialized - will ping Pingbot every 60 minutes");
        }

        public static async Task SendPingToPingBotAsync()
        {
            try
            {
                if (_client == null)
                {
                    Console.WriteLine("⚠️ PingService: Client not initialized");
                    return;
                }

                var guild = _client.GetGuild(PING_GUILD_ID);
                if (guild == null)
                {
                    Console.WriteLine($"⚠️ PingService: Guild {PING_GUILD_ID} not found");
                    return;
                }

                var channel = guild.GetTextChannel(PING_CHANNEL_ID);
                if (channel == null)
                {
                    Console.WriteLine($"⚠️ PingService: Channel {PING_CHANNEL_ID} not found");
                    return;
                }

                await channel.SendMessageAsync("!pingme");
                Console.WriteLine($"✅ Ping sent to Pingbot in {guild.Name} #{channel.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error sending ping to Pingbot: {ex.Message}");
            }
        }

        public static async Task HandlePingResponseAsync(SocketMessage message)
        {
            try
            {
                // Handle response from Pingbot (!ponggg)
                var guild = (message.Channel as SocketGuildChannel)?.Guild;
                if (message.Content == "!ponggg" &&
                    guild?.Id == PING_GUILD_ID &&
                    message.Channel.Id == PING_CHANNEL_ID)
                {
                    Console.WriteLine("✅ Received pong from Pingbot - connection confirmed");

                    // Optional: Send our own response back
                    if (message.Channel is ITextChannel textChannel)
                    {
                        await textChannel.SendMessageAsync("!pongez");
                        Console.WriteLine("✅ Sent pongez response to Pingbot");
                    }
                }

                // Handle incoming ping from Pingbot (!pingmeee)
                else if (message.Content == "!pingmeee" &&
                         guild?.Id == PING_GUILD_ID &&
                         message.Channel.Id == PING_CHANNEL_ID)
                {
                    Console.WriteLine("✅ Received ping from Pingbot");

                    if (message.Channel is ITextChannel textChannel)
                    {
                        await textChannel.SendMessageAsync("!ponggg");
                        Console.WriteLine("✅ Sent ponggg response to Pingbot");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error handling ping response: {ex.Message}");
            }
        }

        public static void Dispose()
        {
            _pingTimer?.Dispose();
            _pingTimer = null;
            Console.WriteLine("PingService disposed");
        }

        public static bool IsHealthy()
        {
            try
            {
                if (_client == null) return false;

                var guild = _client.GetGuild(PING_GUILD_ID);
                if (guild == null) return false;

                var channel = guild.GetTextChannel(PING_CHANNEL_ID);
                return channel != null;
            }
            catch
            {
                return false;
            }
        }

        public static string GetPingStatus()
        {
            try
            {
                if (_client == null) return "❌ Client not initialized";

                var guild = _client.GetGuild(PING_GUILD_ID);
                if (guild == null) return $"❌ Guild {PING_GUILD_ID} not found";

                var channel = guild.GetTextChannel(PING_CHANNEL_ID);
                if (channel == null) return $"❌ Channel {PING_CHANNEL_ID} not found";

                return $"✅ Connected to {guild.Name} #{channel.Name}";
            }
            catch (Exception ex)
            {
                return $"❌ Error: {ex.Message}";
            }
        }
    }
}