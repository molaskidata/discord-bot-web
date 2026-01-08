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
            _commands = new CommandService();

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
                return;

            var context = new SocketCommandContext(_client, message);

            // Start typing indicator for ALL commands
            using (context.Channel.EnterTypingState())
            {
                var commandTask = _commands.ExecuteAsync(context, argPos, _services);
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(120)); // 120 second timeout

                var completedTask = await Task.WhenAny(commandTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    // Command took too long
                    await context.Channel.SendMessageAsync("Command timeout exceeded (120 seconds). Please try again or contact an administrator if the issue persists.");
                }
                else
                {
                    // Wait for command to complete
                    await commandTask;
                }
            }
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

                    // support select menu
                    if (customId == "support_select")
                    {
                        var value = comp.Data.Values.FirstOrDefault();
                        var selectionMap = new System.Collections.Generic.Dictionary<string, string>
                        {
                            { "support_technical", "Technical Issue" },
                            { "support_spam", "Spam / Scam" },
                            { "support_abuse", "Abuse / Harassment" },
                            { "support_ad", "Advertising / Recruitment" },
                            { "support_bug", "Bug / Feature Request" },
                            { "support_other", "Other" }
                        };
                        string label;
                        if (value != null && selectionMap.TryGetValue(value, out var mappedLabel))
                        {
                            label = mappedLabel;
                        }
                        else
                        {
                            label = value ?? "Unknown";
                        }
                        // create ticket channel
                        var guild = (comp.Channel as SocketGuildChannel)?.Guild;
                        if (guild == null) { await comp.RespondAsync("Cannot create ticket: guild not found", ephemeral: true); return; }
                        var user = comp.User;
                        var chanName = $"ticket-{user.Username.ToLowerInvariant().Replace('\u0020', '-')}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 10000}";
                        var overwrites = new System.Collections.Generic.List<Overwrite>();
                        // deny @everyone
                        overwrites.Add(new Overwrite(guild.EveryoneRole.Id, PermissionTarget.Role, new OverwritePermissions(viewChannel: PermValue.Deny)));
                        // allow user
                        overwrites.Add(new Overwrite(user.Id, PermissionTarget.User, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, readMessageHistory: PermValue.Allow)));
                        // allow administrators
                        foreach (var r in guild.Roles.Where(r => r.Permissions.Administrator))
                        {
                            overwrites.Add(new Overwrite(r.Id, PermissionTarget.Role, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, readMessageHistory: PermValue.Allow)));
                        }
                        // create channel
                        var ticketChan = await guild.CreateTextChannelAsync(chanName, prop => { prop.PermissionOverwrites = overwrites; });
                        var embed = new EmbedBuilder().WithColor(Color.DarkGrey).WithTitle($"Ticket: {label}").WithDescription($"Opened by <@{user.Id}> ({user.Id})\n\nPlease describe your issue.").WithFooter("Category: " + label);
                        var row = new ComponentBuilder()
                            .WithButton("Close Ticket", "ticket_close", ButtonStyle.Danger)
                            .WithButton("Log Ticket", "ticket_log", ButtonStyle.Secondary)
                            .WithButton("Save Transcript", "ticket_save", ButtonStyle.Primary);
                        await ticketChan.SendMessageAsync($"<@{user.Id}>", embed: embed.Build(), components: row.Build());
                        MainbotCSharp.Modules.TicketService.TicketMetas.TryAdd(ticketChan.Id, new MainbotCSharp.Modules.TicketService.TicketMeta { GuildId = guild.Id, UserId = user.Id, Category = label });
                        await comp.RespondAsync($"✅ Ticket created: {ticketChan.Mention}", ephemeral: true);
                        return;
                    }

                    // handle ticket buttons
                    if (customId == "ticket_close" || customId == "ticket_log" || customId == "ticket_save")
                    {
                        var channel = (comp.Channel as SocketGuildChannel)?.Guild?.GetTextChannel(comp.Channel.Id);
                        if (channel == null) { await comp.RespondAsync("This button can only be used inside a ticket channel.", ephemeral: true); return; }
                        if (!MainbotCSharp.Modules.TicketService.TicketMetas.TryGetValue(channel.Id, out var meta)) { await comp.RespondAsync("This button is only available inside a ticket channel.", ephemeral: true); return; }

                        if (customId == "ticket_close")
                        {
                            await comp.RespondAsync("Closing ticket...", ephemeral: true);
                            _ = Task.Run(async () => { await Task.Delay(1000); try { await channel.DeleteAsync(); } catch { } MainbotCSharp.Modules.TicketService.TicketMetas.TryRemove(channel.Id, out _); });
                            return;
                        }

                        var cfg = MainbotCSharp.Modules.TicketService.GetConfig(meta.GuildId);
                        if (cfg == null) { await comp.RespondAsync("No log channel configured. Ask an admin to run `!ticket-setup`.", ephemeral: true); return; }

                        // build transcript
                        var messages = await channel.GetMessagesAsync(100).FlattenAsync();
                        var transcript = string.Join('\n', messages.Reverse().Select(m => $"[{m.Timestamp}] {m.Author} ({m.Author.Id}): {m.Content}"));
                        var filename = $"ticket_{meta.GuildId}_{channel.Id}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.txt";
                        try { System.IO.File.WriteAllText(filename, transcript); } catch { }
                        var logChan = channel.Guild.GetTextChannel(cfg.LogChannelId);
                        if (logChan != null)
                        {
                            try { await logChan.SendFileAsync(filename, $"Ticket transcript from {channel.Name} (created by <@{meta.UserId}>):"); } catch { }
                        }
                        try { System.IO.File.Delete(filename); } catch { }
                        await comp.RespondAsync("✅ Ticket transcript saved to log channel.", ephemeral: true);
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
