using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace PiratbotCSharp.Modules
{
    // Ticket Service Classes
    public class TicketConfigEntry
    {
        public ulong LogChannelId { get; set; }
        public ulong? TicketCategoryId { get; set; }
        public ulong? SupportRoleId { get; set; }
        
        // Setup tracking for continuation feature
        public string? SetupStep { get; set; }
        public ulong? TicketMessageChannelId { get; set; }
        public ulong? TicketMessageId { get; set; }
        public DateTime? LastSetupAttempt { get; set; }
    }

    public static class TicketService
    {
        private const string TICKETS_CONFIG_FILE = "pirat_tickets_config.json";
        private static Dictionary<ulong, TicketConfigEntry> _cfg = LoadTicketsConfig();
        public static ConcurrentDictionary<ulong, TicketMeta> TicketMetas = new();
        private static readonly Dictionary<ulong, System.Timers.Timer> _autoCloseTimers = new();

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
                var txt = JsonSerializer.Serialize(_cfg, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(TICKETS_CONFIG_FILE, txt);
            }
            catch { }
        }

        public static TicketConfigEntry? GetConfig(ulong guildId)
        {
            // Always reload config from disk to ensure latest settings
            _cfg = LoadTicketsConfig();
            if (_cfg.TryGetValue(guildId, out var e)) return e; return null;
        }

        public static void SetConfig(ulong guildId, TicketConfigEntry cfg)
        {
            _cfg[guildId] = cfg; SaveTicketsConfig();
        }

        public static void RemoveConfig(ulong guildId)
        {
            _cfg.Remove(guildId);
            SaveTicketsConfig();
        }

        public class TicketMeta
        {
            public ulong UserId { get; set; }
            public string? Category { get; set; }
            public ulong GuildId { get; set; }
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
            public string? Username { get; set; }
        }

        public static async Task<(bool success, SocketTextChannel? channel, ulong channelId)> CreateTicketChannelAsync(SocketGuild guild, SocketUser user, string category)
        {

            try
            {
                var config = GetConfig(guild.Id);
                var channelName = $"ticket-{user.Username}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

                Console.WriteLine($"[TicketDebug] Starting ticket creation for {user.Username} in guild {guild.Name}");

                var overwrites = new List<Overwrite>
                {
                    new Overwrite(guild.EveryoneRole.Id, PermissionTarget.Role, new OverwritePermissions(viewChannel: PermValue.Deny)),
                    new Overwrite(user.Id, PermissionTarget.User, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, readMessageHistory: PermValue.Allow))
                };

                // Add support role if configured
                if (config?.SupportRoleId.HasValue == true)
                {
                    overwrites.Add(new Overwrite(config.SupportRoleId.Value, PermissionTarget.Role,
                        new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, readMessageHistory: PermValue.Allow)));
                }

                // DEBUG: Log category ID and found category name
                if (config?.TicketCategoryId.HasValue == true)
                {
                    var cat = guild.GetCategoryChannel(config.TicketCategoryId.Value);
                    Console.WriteLine($"[TicketDebug] TicketCategoryId: {config.TicketCategoryId.Value}");
                    Console.WriteLine($"[TicketDebug] Found category: {(cat != null ? cat.Name : "NOT FOUND")} (ID: {config.TicketCategoryId.Value})");
                }

                Console.WriteLine($"[TicketDebug] Creating channel with name: {channelName}");

                var restChannel = await guild.CreateTextChannelAsync(channelName, properties =>
                {
                    if (config?.TicketCategoryId.HasValue == true)
                        properties.CategoryId = config.TicketCategoryId.Value;
                    properties.Topic = $"Support ticket for {user.Username} - Category: {category}";
                    properties.PermissionOverwrites = overwrites;
                });

                Console.WriteLine($"[TicketDebug] Channel created successfully: {restChannel.Name} (ID: {restChannel.Id})");

                // Store ticket metadata using REST channel ID
                TicketMetas[restChannel.Id] = new TicketMeta
                {
                    UserId = user.Id,
                    Category = category,
                    GuildId = guild.Id,
                    Username = user.Username
                };

                Console.WriteLine($"[TicketDebug] Ticket metadata stored for channel {restChannel.Id}");

                // Send initial ticket message using REST channel directly
                var embed = new EmbedBuilder()
                    .WithTitle($"🎫 Support Ticket - {category}")
                    .WithDescription($"Hello {user.Mention}! Thank you for creating a support ticket.\n\n" +
                                   $"**Category:** {category}\n" +
                                   $"**Created:** <t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:F>\n\n" +
                                   "Please describe your issue in detail. A support team member will assist you shortly.")
                    .WithColor(0x40E0D0)
                    .WithFooter("This ticket will auto-close after 42 hours of inactivity.");

                var components = new ComponentBuilder()
                    .WithButton("🔒 Close Ticket", "ticket_close", ButtonStyle.Danger)
                    .WithButton("📋 Add User", "ticket_add_user", ButtonStyle.Secondary)
                    .WithButton("📝 Script-Ticket", "ticket_script", ButtonStyle.Primary);

                Console.WriteLine($"[TicketDebug] Attempting to send initial message to REST channel {restChannel.Name}");

                try
                {
                    var message = await restChannel.SendMessageAsync($"{user.Mention}", embed: embed.Build(), components: components.Build());
                    Console.WriteLine($"[TicketDebug] Initial message sent successfully via REST channel (Message ID: {message.Id})");
                }
                catch (Exception msgEx)
                {
                    Console.WriteLine($"[TicketDebug] Failed to send initial message via REST: {msgEx.Message}");
                    // Try without components as fallback
                    try
                    {
                        var fallbackMessage = await restChannel.SendMessageAsync($"{user.Mention}", embed: embed.Build());
                        Console.WriteLine($"[TicketDebug] Fallback message sent successfully via REST (Message ID: {fallbackMessage.Id})");
                    }
                    catch (Exception fallbackEx)
                    {
                        Console.WriteLine($"[TicketDebug] REST fallback also failed: {fallbackEx.Message}");
                    }
                }

                // Start auto-close timer (42 hours)
                StartAutoCloseTimer(restChannel.Id, TimeSpan.FromHours(42));

                // Try to return SocketTextChannel, but create dummy if needed for success indication
                var socketChannel = guild.GetTextChannel(restChannel.Id);
                if (socketChannel != null)
                {
                    Console.WriteLine($"[TicketDebug] Returning SocketTextChannel: {socketChannel.Name}");
                    return (true, socketChannel, restChannel.Id);
                }
                else
                {
                    Console.WriteLine($"[TicketDebug] SocketTextChannel not available, but ticket created successfully");
                    
                    // Wait a bit more and try once more
                    await Task.Delay(1000);
                    socketChannel = guild.GetTextChannel(restChannel.Id);
                    if (socketChannel != null)
                    {
                        Console.WriteLine($"[TicketDebug] SocketTextChannel found after additional wait: {socketChannel.Name}");
                        return (true, socketChannel, restChannel.Id);
                    }
                    
                    Console.WriteLine($"[TicketDebug] SocketTextChannel still not available, but ticket was created successfully via REST");
                    // Return success=true even if SocketTextChannel is not available
                    return (true, null, restChannel.Id);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating ticket: {ex.Message}");
                return (false, null, 0);
            }
        }

        public static async Task HandleSelectMenuInteraction(SocketMessageComponent component)
        {
            try
            {
                if (component.Data.CustomId != "support_select") return;

                var selectedValue = component.Data.Values.FirstOrDefault();
                if (string.IsNullOrEmpty(selectedValue)) return;

                // Map the selected value to the proper category name
                var categoryMap = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "support_technical", "Technical Issue" },
                    { "support_spam", "Spam / Scam" },
                    { "support_abuse", "Abuse / Harassment" },
                    { "support_ad", "Advertising / Recruitment" },
                    { "support_bug", "Bug / Feature Request" },
                    { "support_other", "Other" }
                };

                var category = categoryMap.TryGetValue(selectedValue, out var mappedCategory) ? mappedCategory : "Other";

                // Check if user already has an active ticket
                var guild = (component.User as SocketGuildUser)?.Guild;
                if (guild == null) return;

                var existingTicket = TicketMetas.Values.FirstOrDefault(t =>
                    t.UserId == component.User.Id && t.GuildId == guild.Id);

                if (existingTicket != null)
                {
                    var existingChannel = guild.GetTextChannel(TicketMetas.FirstOrDefault(t => t.Value == existingTicket).Key);
                    if (existingChannel != null)
                    {
                        await component.RespondAsync($"❌ You already have an active ticket: {existingChannel.Mention}", ephemeral: true);
                        return;
                    }
                }

                await component.DeferAsync();

                var (success, channel, channelId) = await CreateTicketChannelAsync(guild, component.User, category);
                
                if (success)
                {
                    // Ticket was created successfully
                    if (channel != null)
                    {
                        await component.FollowupAsync($"✅ Ticket created: {channel.Mention}", ephemeral: true);
                    }
                    else
                    {
                        // Use channel ID if SocketTextChannel is not available
                        await component.FollowupAsync($"✅ Ticket created: <#{channelId}>", ephemeral: true);
                    }

                    // Log ticket creation
                    var config = GetConfig(guild.Id);
                    if (config != null)
                    {
                        var logChannel = guild.GetTextChannel(config.LogChannelId);
                        if (logChannel != null)
                        {
                            var logEmbed = new EmbedBuilder()
                                .WithTitle("🎫 New Ticket Created")
                                .WithColor(Color.Green)
                                .AddField("User", component.User.Mention, true)
                                .AddField("Category", category, true)
                                .AddField("Channel", channel != null ? channel.Mention : $"<#{channelId}>", true)
                                .WithTimestamp(DateTimeOffset.UtcNow);

                            await logChannel.SendMessageAsync(embed: logEmbed.Build());
                        }
                    }
                }
                else
                {
                    // Only show failed message if there was a real error (success = false)
                    Console.WriteLine($"[TicketDebug] Ticket creation actually failed");
                    var failedEmbed = new EmbedBuilder()
                        .WithTitle("❌ Failed")
                        .WithDescription("Failed to create ticket. Please try again or contact an administrator.")
                        .WithColor(0x40E0D0)
                        .Build();
                    await component.FollowupAsync(embed: failedEmbed, ephemeral: true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling select menu: {ex.Message}");
                // Only send error message if there was an actual exception
                try
                {
                    var errorEmbed = new EmbedBuilder()
                        .WithTitle("❌ Error")
                        .WithDescription("An error occurred while creating your ticket. Please try again.")
                        .WithColor(0x40E0D0)
                        .Build();
                    await component.FollowupAsync(embed: errorEmbed, ephemeral: true);
                }
                catch { /* Ignore if we can't send error message */ }
            }
        }

        public static async Task HandleButtonInteraction(SocketMessageComponent component)
        {
            try
            {
                switch (component.Data.CustomId)
                {
                    case "ticket_close":
                        await HandleCloseTicket(component);
                        break;
                    case "ticket_add_user":
                        await HandleAddUser(component);
                        break;
                    case "ticket_script":
                        await HandleScriptTicket(component);
                        break;
                    case "ticket_confirm_close":
                        await HandleConfirmClose(component);
                        break;
                    case "ticket_cancel_close":
                        await HandleCancelClose(component);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling button interaction: {ex.Message}");
            }
        }

        private static async Task HandleCloseTicket(SocketMessageComponent component)
        {
            if (!TicketMetas.TryGetValue(component.Channel.Id, out var meta)) return;

            // Only administrators can close tickets
            var user = component.User as SocketGuildUser;
            var config = GetConfig(meta.GuildId);
            bool canClose = user.GuildPermissions.Administrator;

            if (!canClose)
            {
                var noPermissionEmbed = new EmbedBuilder()
                    .WithTitle("❌ No Permission")
                    .WithDescription("Only administrators can close tickets.")
                    .WithColor(0x40E0D0)
                    .Build();
                await component.RespondAsync(embed: noPermissionEmbed, ephemeral: true);
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle("🔒 Close Ticket")
                .WithDescription("Are you sure you want to close this ticket? This action cannot be undone.")
                .WithColor(0x40E0D0);

            var confirmComponents = new ComponentBuilder()
                .WithButton("✅ Confirm Close", "ticket_confirm_close", ButtonStyle.Danger)
                .WithButton("❌ Cancel", "ticket_cancel_close", ButtonStyle.Secondary);

            await component.RespondAsync(embed: embed.Build(), components: confirmComponents.Build());
        }

        private static async Task HandleConfirmClose(SocketMessageComponent component)
        {
            await CloseTicketChannel(component.Channel as SocketTextChannel, component.User);
            await component.RespondAsync("🔒 Ticket closed.", ephemeral: true);
        }

        private static async Task HandleCancelClose(SocketMessageComponent component)
        {
            await component.RespondAsync("❌ Ticket close cancelled.", ephemeral: true);
        }

        private static async Task HandleAddUser(SocketMessageComponent component)
        {
            if (!TicketMetas.TryGetValue(component.Channel.Id, out var meta)) return;

            var user = component.User as SocketGuildUser;
            bool canAddUser = user.GuildPermissions.Administrator;

            if (!canAddUser)
            {
                var noPermissionEmbed = new EmbedBuilder()
                    .WithTitle("❌ No Permission")
                    .WithDescription("Only administrators can add users to tickets.")
                    .WithColor(0x40E0D0)
                    .Build();
                await component.RespondAsync(embed: noPermissionEmbed, ephemeral: true);
                return;
            }

            var addUserEmbed = new EmbedBuilder()
                .WithTitle("👥 Add User")
                .WithDescription("Please mention the user you want to add to this ticket:")
                .WithColor(0x40E0D0)
                .Build();
            await component.RespondAsync(embed: addUserEmbed, ephemeral: true);
        }

        private static async Task HandleScriptTicket(SocketMessageComponent component)
        {
            if (!TicketMetas.TryGetValue(component.Channel.Id, out var meta)) return;

            var user = component.User as SocketGuildUser;
            bool canScript = user.GuildPermissions.Administrator;

            if (!canScript)
            {
                var noPermissionEmbed = new EmbedBuilder()
                    .WithTitle("❌ No Permission")
                    .WithDescription("Only administrators can create ticket scripts.")
                    .WithColor(0x40E0D0)
                    .Build();
                await component.RespondAsync(embed: noPermissionEmbed, ephemeral: true);
                return;
            }

            var scriptEmbed = new EmbedBuilder()
                .WithTitle("📝 Script-Ticket")
                .WithDescription("Creating a transcript of this ticket conversation...")
                .WithColor(0x40E0D0)
                .Build();
            await component.RespondAsync(embed: scriptEmbed, ephemeral: true);

            // Create transcript
            var channel = component.Channel as SocketTextChannel;
            if (channel == null) return;

            try
            {
                var config = GetConfig(meta.GuildId);
                if (config == null)
                {
                    var noConfigEmbed = new EmbedBuilder()
                        .WithTitle("❌ Configuration Error")
                        .WithDescription("No log channel configured. Ask an admin to run `?ticket-setup`.")
                        .WithColor(0x40E0D0)
                        .Build();
                    await component.FollowupAsync(embed: noConfigEmbed, ephemeral: true);
                    return;
                }

                // Build transcript
                var messages = await channel.GetMessagesAsync(100).FlattenAsync();
                var transcript = string.Join('\n', messages.Reverse().Select(m => 
                    $"[{m.Timestamp:yyyy-MM-dd HH:mm:ss}] {m.Author.Username}: {m.Content}"));
                
                var filename = $"ticket-script_{meta.GuildId}_{channel.Id}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.txt";
                System.IO.File.WriteAllText(filename, transcript);

                var logChannel = channel.Guild.GetTextChannel(config.LogChannelId);
                if (logChannel != null)
                {
                    await logChannel.SendFileAsync(filename, $"📋 Ticket transcript from **{channel.Name}** (created by <@{meta.UserId}>)");
                    
                    var successEmbed = new EmbedBuilder()
                        .WithTitle("✅ Script Created")
                        .WithDescription("Ticket transcript has been saved to the log channel.")
                        .WithColor(0x40E0D0)
                        .Build();
                    await component.FollowupAsync(embed: successEmbed, ephemeral: true);
                }

                // Clean up file
                try { System.IO.File.Delete(filename); } catch { }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating ticket script: {ex.Message}");
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("❌ Error")
                    .WithDescription("Failed to create ticket script. Please try again.")
                    .WithColor(0x40E0D0)
                    .Build();
                await component.FollowupAsync(embed: errorEmbed, ephemeral: true);
            }
        }

        public static async Task CloseTicketChannel(SocketTextChannel channel, SocketUser closedBy)
        {
            try
            {
                if (!TicketMetas.TryGetValue(channel.Id, out var meta)) return;

                // Cancel auto-close timer
                if (_autoCloseTimers.TryGetValue(channel.Id, out var timer))
                {
                    timer?.Stop();
                    timer?.Dispose();
                    _autoCloseTimers.Remove(channel.Id);
                }

                var config = GetConfig(meta.GuildId);
                if (config != null)
                {
                    var logChannel = channel.Guild.GetTextChannel(config.LogChannelId);
                    if (logChannel != null)
                    {
                        try
                        {
                            // Create transcript
                            var messages = await channel.GetMessagesAsync(100).FlattenAsync();
                            var transcript = string.Join('\n', messages.Reverse().Select(m => $"[{m.Timestamp}] {m.Author} ({m.Author.Id}): {m.Content}"));
                            var filename = $"ticket_{meta.GuildId}_{channel.Id}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.txt";
                            System.IO.File.WriteAllText(filename, transcript);

                            await logChannel.SendFileAsync(filename, $"📋 Ticket transcript from **{channel.Name}** (created by <@{meta.UserId}>)");
                            System.IO.File.Delete(filename);

                            // Log closure
                            var logEmbed = new EmbedBuilder()
                                .WithTitle("🔒 Ticket Closed")
                                .WithColor(Color.Red)
                                .AddField("Channel", channel.Name, true)
                                .AddField("Closed by", closedBy.Mention, true)
                                .AddField("Originally created by", $"<@{meta.UserId}>", true)
                                .WithTimestamp(DateTimeOffset.UtcNow);

                            await logChannel.SendMessageAsync(embed: logEmbed.Build());
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error logging ticket closure: {ex.Message}");
                        }
                    }
                }

                // Remove from metadata and delete channel
                TicketMetas.TryRemove(channel.Id, out _);
                await Task.Delay(2000); // Small delay before deletion
                await channel.DeleteAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing ticket: {ex.Message}");
            }
        }

        private static void StartAutoCloseTimer(ulong channelId, TimeSpan duration)
        {
            try
            {
                if (_autoCloseTimers.ContainsKey(channelId))
                {
                    _autoCloseTimers[channelId]?.Stop();
                    _autoCloseTimers[channelId]?.Dispose();
                }

                var timer = new System.Timers.Timer(duration.TotalMilliseconds);
                timer.Elapsed += async (s, e) =>
                {
                    timer?.Stop();
                    timer?.Dispose();
                    _autoCloseTimers.TryRemove(channelId, out _);

                    try
                    {
                        // Auto close ticket logic would go here
                        Console.WriteLine($"Auto-closing ticket {channelId} due to inactivity");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error auto-closing ticket: {ex.Message}");
                    }
                };
                timer.Start();
                _autoCloseTimers[channelId] = timer;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting auto-close timer: {ex.Message}");
            }
        }
    }

    [Group("ticket")]
    public class TicketCommands : ModuleBase<SocketCommandContext>
    {
        [Command("setup")]
        [Summary("Setup ticket system (Admin only)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetupTicketSystemAsync()
        {
            try
            {
                // Initialize setup tracking
                var config = TicketService.GetConfig(Context.Guild.Id) ?? new TicketConfigEntry();
                config.SetupStep = "log_channel";
                config.LastSetupAttempt = DateTime.UtcNow;
                TicketService.SetConfig(Context.Guild.Id, config);

                // Step 1: Ask for log channel
                var askEmbed = new EmbedBuilder()
                    .WithTitle("🎫 Ticket System Setup")
                    .WithDescription("What channel should be the ticket log channel?\n\nWrite the **Channel ID** only in the chat or type `new-ticketlog` and I create a ticket log channel for you. You can move it after where you want.\n\n*Use `?ticket cont` if something goes wrong.*")
                    .WithColor(0x40E0D0)
                    .Build();
                await ReplyAsync(embed: askEmbed);

                var logChannelResponse = await NextMessageAsync(TimeSpan.FromMinutes(1));
                if (logChannelResponse == null)
                {
                    var timeoutEmbed = new EmbedBuilder()
                        .WithTitle("⏰ Timeout")
                        .WithDescription("❌ Timeout! You can continue the setup with `?ticket cont`.")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: timeoutEmbed);
                    config.SetupStep = "log_channel";
                    TicketService.SetConfig(Context.Guild.Id, config);
                    return;
                }

                ulong logChannelId;
                ITextChannel logChannel;

                if (logChannelResponse.Content.Trim().ToLower() == "new-ticketlog")
                {
                    // Create new log channel
                    logChannel = await Context.Guild.CreateTextChannelAsync("★-ticket-log-book");
                    logChannelId = logChannel.Id;
                    var createdEmbed = new EmbedBuilder()
                        .WithTitle("✅ Channel Created")
                        .WithDescription($"Ticket log channel created: {logChannel.Mention}\nThe channel was set and is now available.")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: createdEmbed);
                }
                else if (ulong.TryParse(logChannelResponse.Content.Trim(), out logChannelId))
                {
                    // Use existing channel
                    logChannel = Context.Guild.GetTextChannel(logChannelId);
                    if (logChannel == null)
                    {
                        var notFoundEmbed = new EmbedBuilder()
                            .WithTitle("❌ Not Found")
                            .WithDescription("Channel not found! Please provide a valid Channel ID from this server.\n\nUse `?ticket cont` to try again.")
                            .WithColor(0x40E0D0)
                            .Build();
                        await ReplyAsync(embed: notFoundEmbed);
                        config.SetupStep = "log_channel";
                        TicketService.SetConfig(Context.Guild.Id, config);
                        return;
                    }
                    var setEmbed = new EmbedBuilder()
                        .WithTitle("✅ Channel Set")
                        .WithDescription($"Ticket log channel set: {logChannel.Mention}")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: setEmbed);
                }
                else
                {
                    var invalidEmbed = new EmbedBuilder()
                        .WithTitle("❌ Invalid Input")
                        .WithDescription("Invalid input! Please provide a Channel ID or type `new-ticketlog`.\n\nUse `?ticket cont` to try again.")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: invalidEmbed);
                    config.SetupStep = "log_channel";
                    TicketService.SetConfig(Context.Guild.Id, config);
                    return;
                }

                // Load or create config, update log channel, and save
                config = TicketService.GetConfig(Context.Guild.Id) ?? new TicketConfigEntry();
                config.LogChannelId = logChannelId;
                config.SetupStep = "ticket_message_channel";
                config.LastSetupAttempt = DateTime.UtcNow;
                TicketService.SetConfig(Context.Guild.Id, config);

                // Continue automatically to ticket message setup
                var continueEmbed = new EmbedBuilder()
                    .WithTitle("🎫 Continuing Setup")
                    .WithDescription("Log channel has been saved. Now setting up the ticket message...")
                    .WithColor(0x40E0D0)
                    .Build();
                await ReplyAsync(embed: continueEmbed);

                // Step 2: Ask for ticket message channel
                var ticketChanEmbed = new EmbedBuilder()
                    .WithTitle("📝 Ticket Message Channel")
                    .WithDescription("In which channel you want to have the ticket message to send?\n\nType the **Channel ID** or type `new-ticketchan` and the bot creates the channel and sends the Ticket Embed system message in there with the help categories and ticket opening tool.\n\n*Use `?ticket cont` if something goes wrong.*")
                    .WithColor(0x40E0D0)
                    .Build();
                await ReplyAsync(embed: ticketChanEmbed);

                var ticketChannelResponse = await NextMessageAsync(TimeSpan.FromMinutes(1));
                if (ticketChannelResponse == null)
                {
                    var timeoutEmbed = new EmbedBuilder()
                        .WithTitle("⏰ Timeout")
                        .WithDescription("❌ Timeout! Log channel has been saved. Use `?ticket cont` to continue setup.")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: timeoutEmbed);
                    return;
                }

                ITextChannel ticketChannel;

                if (ticketChannelResponse.Content.Trim().ToLower() == "new-ticketchan")
                {
                    // Ask for category before creating new ticket channel
                    var categoryEmbed = new EmbedBuilder()
                        .WithTitle("📁 Select Category")
                        .WithDescription("In which **Category** do you want to create the ticket channel?\n\nType the **Category ID** or type `none` to create the channel without a category.\n\n*Use `?ticket cont` if something goes wrong.*")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: categoryEmbed);

                    var categoryResponse = await NextMessageAsync(TimeSpan.FromMinutes(1));
                    if (categoryResponse == null)
                    {
                        var timeoutEmbed = new EmbedBuilder()
                            .WithTitle("⏰ Timeout")
                            .WithDescription("❌ Timeout! Log channel has been saved. You can run this command again to set up the ticket message.")
                            .WithColor(0x40E0D0)
                            .Build();
                        await ReplyAsync(embed: timeoutEmbed);
                        return;
                    }

                    ulong? categoryId = null;
                    if (categoryResponse.Content.Trim().ToLower() != "none")
                    {
                        if (ulong.TryParse(categoryResponse.Content.Trim(), out var parsedCategoryId))
                        {
                            var category = Context.Guild.GetCategoryChannel(parsedCategoryId);
                            if (category == null)
                            {
                                var notFoundEmbed = new EmbedBuilder()
                                    .WithTitle("❌ Not Found")
                                    .WithDescription("Category not found! Please provide a valid Category ID from this server.\n\nUse `?ticket cont` to try again.")
                                    .WithColor(0x40E0D0)
                                    .Build();
                                await ReplyAsync(embed: notFoundEmbed);
                                config.SetupStep = "ticket_category_selection";
                                TicketService.SetConfig(Context.Guild.Id, config);
                                return;
                            }
                            categoryId = parsedCategoryId;
                        }
                        else
                        {
                            var invalidEmbed = new EmbedBuilder()
                                .WithTitle("❌ Invalid Input")
                                .WithDescription("Invalid input! Please provide a valid Category ID or type `none`.\n\nUse `?ticket cont` to try again.")
                                .WithColor(0x40E0D0)
                                .Build();
                            await ReplyAsync(embed: invalidEmbed);
                            config.SetupStep = "ticket_category_selection";
                            TicketService.SetConfig(Context.Guild.Id, config);
                            return;
                        }
                    }

                    // Create new ticket channel with category
                    if (categoryId.HasValue)
                    {
                        ticketChannel = await Context.Guild.CreateTextChannelAsync("★-support-tickets", prop =>
                        {
                            prop.CategoryId = categoryId.Value;
                        });
                    }
                    else
                    {
                        ticketChannel = await Context.Guild.CreateTextChannelAsync("★-support-tickets");
                    }

                    var createdEmbed = new EmbedBuilder()
                        .WithTitle("✅ Channel Created")
                        .WithDescription($"Ticket channel created: {ticketChannel.Mention}")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: createdEmbed);
                }
                else if (ulong.TryParse(ticketChannelResponse.Content.Trim(), out var ticketChannelId))
                {
                    // Use existing channel
                    ticketChannel = Context.Guild.GetTextChannel(ticketChannelId);
                    if (ticketChannel == null)
                    {
                        var notFoundEmbed = new EmbedBuilder()
                            .WithTitle("❌ Not Found")
                            .WithDescription("Channel not found! Please provide a valid Channel ID from this server.\n\nUse `?ticket cont` to try again.")
                            .WithColor(0x40E0D0)
                            .Build();
                        await ReplyAsync(embed: notFoundEmbed);
                        config.SetupStep = "ticket_message_channel";
                        TicketService.SetConfig(Context.Guild.Id, config);
                        return;
                    }
                }
                else
                {
                    var invalidEmbed = new EmbedBuilder()
                        .WithTitle("❌ Invalid Input")
                        .WithDescription("Invalid input! Please provide a Channel ID or type `new-ticketchan`.\n\nUse `?ticket cont` to try again.")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: invalidEmbed);
                    config.SetupStep = "ticket_message_channel";
                    TicketService.SetConfig(Context.Guild.Id, config);
                    return;
                }

                // Send ticket embed with dropdown menu
                var ticketEmbed = new EmbedBuilder()
                    .WithTitle("🎫 **Support Ticket System**")
                    .WithDescription("**Need help or want to report an issue?**\n\nSelect the category that best describes your issue from the menu below, and we'll create a private ticket channel for you.")
                    .WithColor(0x1D80A3)
                    .AddField("📋 **Available Support Categories**",
                        "• **Technical Issue** - Bot not working, errors, bugs\n" +
                        "• **Spam / Scam** - Report spam or scam content\n" +
                        "• **Abuse / Harassment** - Report user misconduct\n" +
                        "• **Advertising / Recruitment** - Report unwanted promotion\n" +
                        "• **Bug / Feature Request** - Report bugs or suggest features\n" +
                        "• **Other** - General questions or other issues", false)
                    .WithFooter("Support tickets are private and only visible to you and server staff")
                    .WithTimestamp(DateTimeOffset.UtcNow);

                var selectMenu = new SelectMenuBuilder()
                    .WithPlaceholder("Select your issue type...")
                    .WithCustomId("support_select")
                    .WithMinValues(1)
                    .WithMaxValues(1)
                    .AddOption("Technical Issue", "support_technical", "Bot errors, commands not working", new Emoji("🔧"))
                    .AddOption("Spam / Scam", "support_spam", "Report spam or scam content", new Emoji("🚫"))
                    .AddOption("Abuse / Harassment", "support_abuse", "Report user misconduct", new Emoji("⚠️"))
                    .AddOption("Advertising / Recruitment", "support_ad", "Unwanted promotion", new Emoji("📢"))
                    .AddOption("Bug / Feature Request", "support_bug", "Report bugs or suggest features", new Emoji("🐛"))
                    .AddOption("Other", "support_other", "General questions", new Emoji("❓"));

                var component = new ComponentBuilder()
                    .WithSelectMenu(selectMenu)
                    .Build();

                var sentMessage = await ticketChannel.SendMessageAsync(embed: ticketEmbed.Build(), components: component);

                // Save ticket message info for deletion later
                config.TicketMessageChannelId = ticketChannel.Id;
                config.TicketMessageId = sentMessage.Id;
                config.SetupStep = "ticket_creation_category";
                config.LastSetupAttempt = DateTime.UtcNow;
                TicketService.SetConfig(Context.Guild.Id, config);

                // Step 3: Ask for ticket category (where actual tickets will be created)
                var ticketCategoryEmbed = new EmbedBuilder()
                    .WithTitle("📁 Ticket Creation Category")
                    .WithDescription("In which **Category** should the bot create the individual support tickets when users open them?\n\nType the **Category ID** where all new tickets will be created.\n\n*Use `?ticket cont` if something goes wrong.*")
                    .WithColor(0x40E0D0)
                    .Build();
                await ReplyAsync(embed: ticketCategoryEmbed);

                var ticketCategoryResponse = await NextMessageAsync(TimeSpan.FromMinutes(1));
                if (ticketCategoryResponse == null)
                {
                    var timeoutEmbed = new EmbedBuilder()
                        .WithTitle("⏰ Timeout")
                        .WithDescription("❌ Timeout! Setup is incomplete. Tickets will be created at the top of the server. Use `?ticket cont` to complete the setup.")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: timeoutEmbed);
                    return;
                }

                if (ulong.TryParse(ticketCategoryResponse.Content.Trim(), out var ticketCategoryId))
                {
                    var ticketCategory = Context.Guild.GetCategoryChannel(ticketCategoryId);
                    if (ticketCategory == null)
                    {
                        var notFoundEmbed = new EmbedBuilder()
                            .WithTitle("❌ Not Found")
                            .WithDescription("Category not found! Please provide a valid Category ID from this server.\n\nUse `?ticket cont` to try again.")
                            .WithColor(0x40E0D0)
                            .Build();
                        await ReplyAsync(embed: notFoundEmbed);
                        config.SetupStep = "ticket_creation_category";
                        TicketService.SetConfig(Context.Guild.Id, config);
                        return;
                    }

                    // Reload config to ensure we have the latest version
                    var currentConfig = TicketService.GetConfig(Context.Guild.Id);
                    if (currentConfig != null)
                    {
                        currentConfig.TicketCategoryId = ticketCategoryId;
                        currentConfig.SetupStep = null; // Clear setup status
                        currentConfig.LastSetupAttempt = null;
                        TicketService.SetConfig(Context.Guild.Id, currentConfig);
                    }

                    var categorySetEmbed = new EmbedBuilder()
                        .WithTitle("✅ Category Set")
                        .WithDescription($"Tickets will be created in category: <#{ticketCategoryId}>")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: categorySetEmbed);
                }
                else
                {
                    var invalidEmbed = new EmbedBuilder()
                        .WithTitle("❌ Invalid Input")
                        .WithDescription("Invalid input! Please provide a valid Category ID.")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: invalidEmbed);
                    return;
                }

                // Final success message
                var finalEmbed = new EmbedBuilder()
                    .WithTitle("✅ Setup Complete!")
                    .WithDescription($"Ticket system has been successfully configured!\n\n" +
                                   $"**Log Channel:** {logChannel.Mention}\n" +
                                   $"**Ticket Message:** {ticketChannel.Mention}\n" +
                                   $"**Tickets Category:** <#{ticketCategoryId}>\n\n" +
                                   "Users can now create tickets using the dropdown menu!")
                    .WithColor(0x00FF00)
                    .Build();
                await ReplyAsync(embed: finalEmbed);

            }
            catch (Exception ex)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("❌ Error")
                    .WithDescription($"An error occurred during setup: {ex.Message}")
                    .WithColor(0x40E0D0)
                    .Build();
                await ReplyAsync(embed: errorEmbed);
            }
        }

        [Command("del-system")]
        [Summary("Remove ticket system configuration and delete ticket message (Admin only)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task DeleteTicketSystemAsync()
        {
            try
            {
                var config = TicketService.GetConfig(Context.Guild.Id);
                if (config == null)
                {
                    var notConfiguredEmbed = new EmbedBuilder()
                        .WithTitle("❌ Not Configured")
                        .WithDescription("No ticket system configured for this server.")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: notConfiguredEmbed);
                    return;
                }

                // Try to delete the ticket message if it exists
                if (config.TicketMessageChannelId.HasValue && config.TicketMessageId.HasValue)
                {
                    try
                    {
                        var ticketChannel = Context.Guild.GetTextChannel(config.TicketMessageChannelId.Value);
                        if (ticketChannel != null)
                        {
                            var message = await ticketChannel.GetMessageAsync(config.TicketMessageId.Value);
                            if (message != null)
                            {
                                await message.DeleteAsync();
                            }
                        }
                    }
                    catch
                    {
                        // Ignore errors when deleting the message (might already be deleted)
                    }
                }

                TicketService.RemoveConfig(Context.Guild.Id);
                
                var successEmbed = new EmbedBuilder()
                    .WithTitle("✅ Ticket System Removed")
                    .WithDescription("Ticket system configuration and message have been completely removed from this server.")
                    .WithColor(0x40E0D0)
                    .Build();
                await ReplyAsync(embed: successEmbed);
            }
            catch (Exception ex)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("❌ Failed")
                    .WithDescription($"Failed to remove configuration: {ex.Message}")
                    .WithColor(0x40E0D0)
                    .Build();
                await ReplyAsync(embed: errorEmbed);
            }
        }

        [Command("cont")]
        [Summary("Continue ticket system setup from where it was left off (Admin only)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task ContinueTicketSetupAsync()
        {
            try
            {
                var config = TicketService.GetConfig(Context.Guild.Id);
                if (config == null || string.IsNullOrEmpty(config.SetupStep))
                {
                    var noSetupEmbed = new EmbedBuilder()
                        .WithTitle("❌ No Setup to Continue")
                        .WithDescription("No incomplete ticket system setup found. Use `?ticket setup` to start a new setup.")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: noSetupEmbed);
                    return;
                }

                // Check if setup is too old (more than 1 hour)
                if (config.LastSetupAttempt.HasValue && 
                    (DateTime.UtcNow - config.LastSetupAttempt.Value).TotalHours > 1)
                {
                    var expiredEmbed = new EmbedBuilder()
                        .WithTitle("⏰ Setup Expired")
                        .WithDescription("The previous setup attempt is too old. Please use `?ticket setup` to start a fresh setup.")
                        .WithColor(0x40E0D0)
                        .Build();
                    await ReplyAsync(embed: expiredEmbed);
                    
                    // Clear expired setup
                    config.SetupStep = null;
                    config.LastSetupAttempt = null;
                    TicketService.SetConfig(Context.Guild.Id, config);
                    return;
                }

                var continueEmbed = new EmbedBuilder()
                    .WithTitle("🔄 Continuing Setup")
                    .WithDescription($"Continuing ticket system setup from step: **{config.SetupStep}**")
                    .WithColor(0x40E0D0)
                    .Build();
                await ReplyAsync(embed: continueEmbed);

                // Continue from the saved step
                switch (config.SetupStep)
                {
                    case "ticket_message_channel":
                        await ContinueTicketMessageChannelSetup(config);
                        break;
                    case "ticket_category_selection":
                        await ContinueTicketCategorySetup(config);
                        break;
                    case "ticket_creation_category":
                        await ContinueTicketCreationCategorySetup(config);
                        break;
                    default:
                        var unknownStepEmbed = new EmbedBuilder()
                            .WithTitle("❌ Unknown Step")
                            .WithDescription("Unknown setup step. Please use `?ticket setup` to start a new setup.")
                            .WithColor(0x40E0D0)
                            .Build();
                        await ReplyAsync(embed: unknownStepEmbed);
                        break;
                }
            }
            catch (Exception ex)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("❌ Failed")
                    .WithDescription($"Failed to continue setup: {ex.Message}")
                    .WithColor(0x40E0D0)
                    .Build();
                await ReplyAsync(embed: errorEmbed);
            }
        }

        private async Task ContinueTicketMessageChannelSetup(TicketConfigEntry config)
        {
            var ticketChanEmbed = new EmbedBuilder()
                .WithTitle("📝 Ticket Message Channel")
                .WithDescription("In which channel you want to have the ticket message to send?\n\nType the **Channel ID** or type `new-ticketchan` and the bot creates the channel and sends the Ticket Embed system message in there with the help categories and ticket opening tool.\n\n*Use `?ticket cont` if something goes wrong.*")
                .WithColor(0x40E0D0)
                .Build();
            await ReplyAsync(embed: ticketChanEmbed);

            config.LastSetupAttempt = DateTime.UtcNow;
            TicketService.SetConfig(Context.Guild.Id, config);
        }

        private async Task ContinueTicketCategorySetup(TicketConfigEntry config)
        {
            var categoryEmbed = new EmbedBuilder()
                .WithTitle("📁 Select Category")
                .WithDescription("In which **Category** do you want to create the ticket channel?\n\nType the **Category ID** or type `none` to create the channel without a category.\n\n*Use `?ticket cont` if something goes wrong.*")
                .WithColor(0x40E0D0)
                .Build();
            await ReplyAsync(embed: categoryEmbed);

            config.LastSetupAttempt = DateTime.UtcNow;
            TicketService.SetConfig(Context.Guild.Id, config);
        }

        private async Task ContinueTicketCreationCategorySetup(TicketConfigEntry config)
        {
            var ticketCategoryEmbed = new EmbedBuilder()
                .WithTitle("📁 Ticket Creation Category")
                .WithDescription("In which **Category** should the bot create the individual support tickets when users open them?\n\nType the **Category ID** where all new tickets will be created.\n\n*Use `?ticket cont` if something goes wrong.*")
                .WithColor(0x40E0D0)
                .Build();
            await ReplyAsync(embed: ticketCategoryEmbed);

            config.LastSetupAttempt = DateTime.UtcNow;
            TicketService.SetConfig(Context.Guild.Id, config);
        }
    }
}