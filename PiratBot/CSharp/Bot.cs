using System;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace PiratBotCSharp
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
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent
            };
            _client = new DiscordSocketClient(config);
            _commands = new CommandService();

            _services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_commands)
                .BuildServiceProvider();

            _client.Log += Client_Log;
            _commands.Log += Commands_Log;
            _client.Ready += Client_Ready;
            _client.MessageReceived += HandleMessageAsync;
            _client.InteractionCreated += InteractionCreatedAsync;
        }

        public async Task InitializeAsync()
        {
            var token = Environment.GetEnvironmentVariable("PIRATBOT_TOKEN");
            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("ERROR: Please set environment variable PIRATBOT_TOKEN with your bot token.");
                return;
            }

            await _commands.AddModulesAsync(Assembly.GetExecutingAssembly(), _services);

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
        }

        private Task Client_Ready()
        {
            Console.WriteLine($"PiratBot ready. Logged in as {_client.CurrentUser}");
            _client.SetActivityAsync(new Game("Sea of Thieves ⚓"));
            return Task.CompletedTask;
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
            // run security checks first (best-effort)
            try { await PiratBotCSharp.Services.SecurityService.HandleMessageAsync(rawMessage); } catch { }

            if (!(rawMessage is SocketUserMessage message)) return;
            if (message.Author.IsBot) return;

            int argPos = 0;
            if (!(message.HasCharPrefix('!', ref argPos) || message.HasMentionPrefix(_client.CurrentUser, ref argPos)))
                return;

            var context = new SocketCommandContext(_client, message);
            await _commands.ExecuteAsync(context, argPos, _services);
        }

        private async Task InteractionCreatedAsync(SocketInteraction interaction)
        {
            try
            {
                if (interaction.Type != InteractionType.MessageComponent) return;
                var comp = interaction as SocketMessageComponent;
                if (comp == null) return;

                var customId = comp.Data.CustomId;
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
                    var label = selectionMap.ContainsKey(value) ? selectionMap[value] : value;
                    var guild = (comp.Channel as SocketGuildChannel)?.Guild;
                    if (guild == null) { await comp.RespondAsync("Cannot create ticket: guild not found", ephemeral: true); return; }
                    var user = comp.User;
                    var chanName = $"ticket-{user.Username.ToLowerInvariant().Replace(' ', '-')}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 10000}";
                    // ensure bot has permission to create/manage channels
                    var botMember = guild.CurrentUser;
                    if (botMember == null || !botMember.GuildPermissions.ManageChannels)
                    {
                        await comp.RespondAsync("I don't have Manage Channels permission and cannot create ticket channels. Ask an admin to grant me Manage Channels.", ephemeral: true);
                        return;
                    }

                    var ticketChan = await guild.CreateTextChannelAsync(chanName);
                    try { await ticketChan.AddPermissionOverwriteAsync(guild.EveryoneRole, new OverwritePermissions(viewChannel: PermValue.Deny)); } catch { }
                    try { await ticketChan.AddPermissionOverwriteAsync(user, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, readMessageHistory: PermValue.Allow)); } catch { }
                    foreach (var r in guild.Roles.Where(r => r.Permissions.Administrator))
                    {
                        try { await ticketChan.AddPermissionOverwriteAsync(r, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, readMessageHistory: PermValue.Allow)); } catch { }
                    }
                    var embed = new EmbedBuilder().WithColor(Color.DarkGrey).WithTitle($"Ticket: {label}").WithDescription($"Opened by <@{user.Id}> ({user.Id})\n\nPlease describe your issue.").WithFooter("Category: " + label);
                    var row = new ComponentBuilder()
                        .WithButton("Close Ticket", "ticket_close", ButtonStyle.Danger)
                        .WithButton("Log Ticket", "ticket_log", ButtonStyle.Secondary)
                        .WithButton("Save Transcript", "ticket_save", ButtonStyle.Primary);
                    await ticketChan.SendMessageAsync(content: $"<@{user.Id}>", embed: embed.Build(), components: row.Build());
                    TicketService.AddMeta(ticketChan.Id, new TicketService.TicketMeta { GuildId = guild.Id, UserId = user.Id, Category = label });
                    await comp.RespondAsync($"✅ Ticket created: {ticketChan.Mention}", ephemeral: true);
                    return;
                }

                if (customId == "ticket_close" || customId == "ticket_log" || customId == "ticket_save")
                {
                    var channel = comp.Channel as SocketGuildChannel;
                    if (channel == null) { await comp.RespondAsync("This button is only available inside a ticket channel.", ephemeral: true); return; }
                    if (!TicketService.TicketMeta.TryGetValue(channel.Id, out var meta)) { await comp.RespondAsync("This button is only available inside a ticket channel.", ephemeral: true); return; }

                    if (customId == "ticket_close")
                    {
                        await comp.RespondAsync("Closing ticket...", ephemeral: true);
                        _ = Task.Run(async () => { await Task.Delay(1000); try { await channel.DeleteAsync(); } catch { } TicketService.RemoveMeta(channel.Id); });
                        return;
                    }

                    var cfg = TicketService.GetConfig(meta.GuildId);
                    if (cfg == null) { await comp.RespondAsync("No log channel configured. Ask an admin to run !munga-ticketsystem.", ephemeral: true); return; }

                    var messages = await channel.GetMessagesAsync(100).FlattenAsync();
                    var transcript = string.Join('\n', messages.Reverse().Select(m => $"[{m.Timestamp}] {m.Author} ({m.Author.Id}): {m.Content}"));
                    var filename = $"pirate_ticket_{meta.GuildId}_{channel.Id}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.txt";
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
            catch (Exception ex)
            {
                try { await interaction.RespondAsync("An error occurred while handling the interaction.", ephemeral: true); } catch { }
                Console.WriteLine("PiratBot interaction error: " + ex);
            }
        }
    }
}
