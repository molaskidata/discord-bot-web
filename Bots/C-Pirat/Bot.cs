using System;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using PiratBotCSharp.Modules;
using System.Threading;

namespace PiratBotCSharp
{
    public class Bot
    {
        private DiscordSocketClient? _client;
        private CommandService? _commands;
        private IServiceProvider? _services;

        public static async Task Main()
        {
            var bot = new Bot();
            await bot.RunAsync();
        }

        public async Task RunAsync()
        {
            try
            {
                var token = Environment.GetEnvironmentVariable("PIRAT_TOKEN");
                if (string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("❌ PIRAT_TOKEN environment variable not set!");
                    Console.WriteLine("Create a .env file or set the environment variable.");
                    return;
                }

                var config = new DiscordSocketConfig
                {
                    GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent | GatewayIntents.GuildVoiceStates
                };
                _client = new DiscordSocketClient(config);
                _commands = new CommandService();

                _services = new ServiceCollection()
                    .AddSingleton(_client)
                    .AddSingleton(_commands)
                    .BuildServiceProvider();

                string commandPrefix = "?";
                await RegisterCommandsAsync();

                _client.MessageReceived += HandleCommandAsync;
                _client.Ready += ReadyAsync;
                _client.Log += LogAsync;

                await _client.LoginAsync(TokenType.Bot, token);
                await _client.StartAsync();

                Console.WriteLine("🏴‍☠️ Mary the Red is sailing the seven seas!");
                await Task.Delay(-1);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error starting PiratBot: {ex.Message}");
            }
        }

        private async Task RegisterCommandsAsync()
        {
            if (_client == null || _commands == null || _services == null) return;

            _client.MessageReceived += HandleCommandAsync;
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        private async Task HandleCommandAsync(SocketMessage messageParam)
        {
            if (_client == null || _commands == null || _services == null) return;

            var message = messageParam as SocketUserMessage;
            if (message == null) return;
            if (message.Author.IsBot) return;

            int argPos = 0;
            if (!(message.HasCharPrefix('?', ref argPos) ||
                  message.HasMentionPrefix(_client.CurrentUser, ref argPos)))
                return;

            var context = new SocketCommandContext(_client, message);
            await _commands.ExecuteAsync(context, argPos, _services);
        }

        private async Task ReadyAsync()
        {
            if (_client == null) return;

            Console.WriteLine($"🏴‍☠️ {_client.CurrentUser} is ready and sailing!");
            Console.WriteLine($"⚓ Connected to {_client.Guilds.Count} servers");

            await _client.SetGameAsync("the seven seas! | ?helpme", null, ActivityType.Playing);

            // Start periodic presence update
            _ = Task.Run(async () =>
            {
                var activities = new[]
                {
                    "the seven seas! | ?helpme",
                    "with me cutlass! | ?games",
                    "for buried treasure! | ?mine",
                    "Battleship! | ?bs_start",
                    "with the crew! | ?crew"
                };

                int index = 0;
                while (true)
                {
                    await Task.Delay(TimeSpan.FromHours(1));
                    if (_client?.ConnectionState == ConnectionState.Connected)
                    {
                        await _client.SetGameAsync(activities[index % activities.Length], null, ActivityType.Playing);
                        index++;
                    }
                }
            });
        }

        private static Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }
    }
}
