using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using MainbotCSharp.Modules;
using MainbotCSharp.Services;
using static MainbotCSharp.Modules.TicketService;

namespace MainbotCSharp
{
    public class Bot
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;

        public Bot()
        {
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent | GatewayIntents.GuildVoiceStates
            };
            _client = new DiscordSocketClient(config);
            
            var commandConfig = new CommandServiceConfig
            {
                DefaultRunMode = RunMode.Async  // Run commands asynchronously to prevent blocking
            };
            _commands = new CommandService(commandConfig);

            _services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_commands)
                .AddSingleton<MainbotCSharp.Modules.MonitorService>()
                .BuildServiceProvider();

            _client.Log += Client_Log;
            _commands.Log += Commands_Log;
            _client.Ready += Client_Ready;
            _client.MessageReceived += HandleMessageAsync;
            _client.InteractionCreated += InteractionCreatedAsync;
            _client.UserVoiceStateUpdated += MainbotCSharp.Modules.VoiceService.HandleVoiceStateUpdatedAsync;
        }

        public async Task InitializeAsync()
        {
            var token = Environment.GetEnvironmentVariable("MAINBOT_TOKEN");
            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("ERROR: Please set environment variable MAINBOT_TOKEN with your bot token.");
                return;
            }

            await _commands.AddModulesAsync(Assembly.GetExecutingAssembly(), _services);

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
        }

        private async Task Client_Ready()
        {
            Console.WriteLine($"Mainbot ready. Logged in as {_client.CurrentUser}");
            await _client.SetActivityAsync(new Game("Managing the server"));
            try { await MainbotCSharp.Modules.VoiceService.StartBackgroundTasks(_client); } catch (Exception ex) { Console.WriteLine("VoiceService start error: " + ex); }
            try
            {
                var mon = _services.GetService(typeof(MainbotCSharp.Modules.MonitorService)) as MainbotCSharp.Modules.MonitorService;
                if (mon != null) await mon.StartAsync(_client);
            }
            catch (Exception ex) { Console.WriteLine("MonitorService start error: " + ex); }

            // Initialize Birthday Service
            try
            {
                MainbotCSharp.Modules.BirthdayService.Initialize(_client);
            }
            catch (Exception ex)
            {
                Console.WriteLine("BirthdayService initialization error: " + ex);
            }

            // Initialize Bump Reminder Service
            try
            {
                BumpReminderService.Initialize(_client);
            }
            catch (Exception ex)
            {
                Console.WriteLine("BumpReminderService initialization error: " + ex);
            }

            // Initialize Ping Service (Bot-to-Bot communication)
            try
            {
                PingService.Initialize(_client);
            }
            catch (Exception ex)
            {
                Console.WriteLine("PingService initialization error: " + ex);
            }
        }

        private Task Client_Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private Task Commands_Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private async Task HandleMessageAsync(SocketMessage rawMessage)
        {
            // DEBUG: Log all received messages
            Console.WriteLine($"[DEBUG] Message received: Author={rawMessage.Author.Username} ({rawMessage.Author.Id}), Channel={rawMessage.Channel.Id}, Content='{rawMessage.Content}', IsBot={rawMessage.Author.IsBot}");
            
            // Handle Disboard bump detection for bump reminders
            try
            {
                BumpReminderService.HandleDisboardMessage(rawMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine("BumpReminderService HandleDisboardMessage error: " + ex);
            }

            // Handle ping system messages
            try
            {
                await PingService.HandlePingResponseAsync(rawMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine("PingService HandlePingResponseAsync error: " + ex);
            }

            // run security checks first
            try
            {
                await MainbotCSharp.Modules.SecurityService.HandleMessageAsync(rawMessage);
            }
            catch { }

            if (!(rawMessage is SocketUserMessage message)) return;
            if (message.Author.IsBot) return;

            int argPos = 0;
            // prefix '!' or mention
            if (!(message.HasCharPrefix('!', ref argPos) || message.HasMentionPrefix(_client.CurrentUser, ref argPos)))
            {
                Console.WriteLine($"[DEBUG] Message '{message.Content}' doesn't have command prefix, but MessageReceived event was fired");
                return;
            }

            var context = new SocketCommandContext(_client, message);

            // Execute command - let it handle its own timeouts internally
            await _commands.ExecuteAsync(context, argPos, _services);
        }

        private async Task InteractionCreatedAsync(SocketInteraction interaction)
        {
            try
            {
                if (interaction.Type == Discord.InteractionType.MessageComponent)
                {
                    var comp = interaction as SocketMessageComponent;
                    if (comp == null) return;
                    var customId = comp.Data.CustomId;

                    // helpdesk select menu
                    if (customId == "helpdesk_select")
                    {
                        var value = comp.Data.Values.FirstOrDefault();
                        string replyText = "";
                        switch (value)
                        {
                            case "help_all":
                                replyText = "Full command list: run `!help` in your server channel to see all commands.";
                                break;
                            case "help_voice":
                                replyText = "Voice help: run `!helpvoice` in your server channel for voice commands.";
                                break;
                            case "help_secure":
                                replyText = "Security help: run `!helpsecure` to see moderation commands.";
                                break;
                            case "help_bump":
                                replyText = "Bump help: run `!helpbump` for bump/reminder commands.";
                                break;
                            case "help_birth":
                                replyText = "Birthday help: run `!helpbirth` to configure birthdays.";
                                break;
                            default:
                                replyText = "No information available for this selection.";
                                break;
                        }
                        await comp.RespondAsync(replyText, ephemeral: true);
                        return;
                    }

                    // support select menu - use the proper ticket service
                    if (customId == "support_select")
                    {
                        await TicketService.HandleSelectMenuInteraction(comp);
                        return;
                    }

                    // handle ticket buttons - use the proper ticket service
                    if (customId.StartsWith("ticket_"))
                    {
                        await TicketService.HandleButtonInteraction(comp);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                try { await interaction.RespondAsync("An error occurred while handling the interaction.", ephemeral: true); } catch { }
                Console.WriteLine("Interaction handler error: " + ex);
            }
        }
    }
}
