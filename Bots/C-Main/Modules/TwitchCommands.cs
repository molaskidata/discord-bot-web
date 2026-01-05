using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mainbot.Modules
{
    public class TwitchCommands : ModuleBase<SocketCommandContext>
    {
        private const string TWITCH_FILE = "twitch_links.json";
        private static readonly HttpClient _httpClient = new HttpClient();

        // Twitch OAuth credentials (from environment or config)
        private static string TwitchClientId => Environment.GetEnvironmentVariable("TWITCH_CLIENT_ID") ?? "";
        private static string TwitchClientSecret => Environment.GetEnvironmentVariable("TWITCH_CLIENT_SECRET") ?? "";

        #region Data Persistence

        private Dictionary<string, Dictionary<string, TwitchUserData>> LoadTwitchLinks()
        {
            if (File.Exists(TWITCH_FILE))
            {
                try
                {
                    var json = File.ReadAllText(TWITCH_FILE);
                    return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, TwitchUserData>>>(json)
                           ?? new Dictionary<string, Dictionary<string, TwitchUserData>>();
                }
                catch
                {
                    return new Dictionary<string, Dictionary<string, TwitchUserData>>();
                }
            }
            return new Dictionary<string, Dictionary<string, TwitchUserData>>();
        }

        private void SaveTwitchLinks(Dictionary<string, Dictionary<string, TwitchUserData>> data)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(TWITCH_FILE, JsonSerializer.Serialize(data, options));
        }

        #endregion

        [Command("settwitch")]
        [Summary("Link your Twitch account and configure clip notifications")]
        public async Task SetTwitchAsync()
        {
            var guildId = Context.Guild.Id.ToString();
            var userId = Context.User.Id.ToString();

            await ReplyAsync("Write your correct Twitch username in the Channel to synchronize and connect with Discord. Format: example");

            // Wait for username input
            var usernameMsg = await NextMessageAsync(timeout: TimeSpan.FromSeconds(60));
            if (usernameMsg == null || string.IsNullOrWhiteSpace(usernameMsg.Content))
            {
                await ReplyAsync("⏰ Timeout! Please run the command again.");
                return;
            }

            var twitchUsername = usernameMsg.Content.Trim();
            var twitchLinks = LoadTwitchLinks();

            if (!twitchLinks.ContainsKey(guildId))
                twitchLinks[guildId] = new Dictionary<string, TwitchUserData>();

            var existingData = twitchLinks[guildId].ContainsKey(userId) ? twitchLinks[guildId][userId] : null;

            if (existingData != null)
            {
                // User already has Twitch linked - ask to update
                await ReplyAsync($"You already have **{existingData.TwitchUsername}** linked.\n" +
                               "Do you want to:\n" +
                               "1️⃣ Update Username\n" +
                               "2️⃣ Update Clip Channel\n" +
                               "3️⃣ Cancel\n" +
                               "Reply with 1, 2, or 3");

                var choiceMsg = await NextMessageAsync(timeout: TimeSpan.FromSeconds(60));
                if (choiceMsg == null) return;

                switch (choiceMsg.Content.Trim())
                {
                    case "1":
                        existingData.TwitchUsername = twitchUsername;
                        SaveTwitchLinks(twitchLinks);
                        await ReplyAsync($"✅ Username updated to **{twitchUsername}**!");
                        return;

                    case "2":
                        await ReplyAsync("Send the Channel ID where clips should be posted:");
                        var newChannelMsg = await NextMessageAsync(timeout: TimeSpan.FromSeconds(60));
                        if (newChannelMsg == null) return;

                        if (ulong.TryParse(newChannelMsg.Content.Trim(), out var newChannelId))
                        {
                            var newChannel = Context.Guild.GetTextChannel(newChannelId);
                            if (newChannel != null)
                            {
                                existingData.ClipChannelId = newChannelId.ToString();
                                SaveTwitchLinks(twitchLinks);
                                await ReplyAsync($"✅ Clip channel updated to <#{newChannelId}>!");
                            }
                            else
                            {
                                await ReplyAsync("❌ Channel not found!");
                            }
                        }
                        else
                        {
                            await ReplyAsync("❌ Invalid channel ID!");
                        }
                        return;

                    case "3":
                        await ReplyAsync("Cancelled.");
                        return;

                    default:
                        await ReplyAsync("Invalid choice. Cancelled.");
                        return;
                }
            }

            // New Twitch setup
            await ReplyAsync("Send the Channel ID where clips should be posted, or type `!create` to create a new thread-only channel:");

            var channelMsg = await NextMessageAsync(timeout: TimeSpan.FromSeconds(60));
            if (channelMsg == null) return;

            ulong clipChannelId = 0;
            var channelInput = channelMsg.Content.Trim();

            if (channelInput.ToLower() == "!create")
            {
                // Create new thread-only channel
                try
                {
                    var newChannel = await Context.Guild.CreateTextChannelAsync($"clips-{twitchUsername.ToLower()}", props =>
                    {
                        props.Topic = $"Twitch clips from {twitchUsername}";
                    });

                    clipChannelId = newChannel.Id;
                    await ReplyAsync($"✅ Created new channel: <#{clipChannelId}>");
                }
                catch (Exception ex)
                {
                    await ReplyAsync($"❌ Failed to create channel: {ex.Message}");
                    return;
                }
            }
            else if (channelInput.ToLower() == "!setchannel")
            {
                await ReplyAsync("Please run `!setchannel` command separately to create a thread-only channel.");
                return;
            }
            else
            {
                // User provided channel ID
                if (ulong.TryParse(channelInput, out clipChannelId))
                {
                    var channel = Context.Guild.GetTextChannel(clipChannelId);
                    if (channel == null)
                    {
                        await ReplyAsync("❌ Channel not found! Please check the ID.");
                        return;
                    }
                }
                else
                {
                    await ReplyAsync("❌ Invalid channel ID!");
                    return;
                }
            }

            // Generate Twitch OAuth URL
            var redirectUri = "http://localhost:3000/twitch/callback"; // Adjust based on your setup
            var scopes = "clips:edit user:read:email";
            var state = $"{guildId}:{userId}";
            var authUrl = $"https://id.twitch.tv/oauth2/authorize?" +
                         $"client_id={TwitchClientId}&" +
                         $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                         $"response_type=code&" +
                         $"scope={Uri.EscapeDataString(scopes)}&" +
                         $"state={state}";

            // Save data
            twitchLinks[guildId][userId] = new TwitchUserData
            {
                TwitchUsername = twitchUsername,
                ClipChannelId = clipChannelId.ToString(),
                LinkedAt = DateTime.UtcNow
            };
            SaveTwitchLinks(twitchLinks);

            var embed = new EmbedBuilder()
                .WithColor(0x9147ff)
                .WithTitle("🎮 Twitch Account Setup")
                .WithDescription($"**Username:** {twitchUsername}\n" +
                               $"**Clip Channel:** <#{clipChannelId}>\n\n" +
                               $"Click the link below to authorize the bot:\n" +
                               $"[Authorize Twitch]({authUrl})")
                .WithFooter("Your clips will be automatically posted to the configured channel!")
                .WithCurrentTimestamp()
                .Build();

            await ReplyAsync(embed: embed);
        }

        [Command("setchannel")]
        [Summary("Create a new thread-only channel for Twitch clips")]
        public async Task SetChannelAsync([Remainder] string channelName = "twitch-clips")
        {
            try
            {
                var newChannel = await Context.Guild.CreateTextChannelAsync(channelName, props =>
                {
                    props.Topic = "Twitch clips - Thread-only channel";
                });

                await ReplyAsync($"✅ Created new clip channel: <#{newChannel.Id}>\n" +
                               "You can now use this channel ID in `!settwitch` setup!");
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Failed to create channel: {ex.Message}");
            }
        }

        [Command("deletetwitch")]
        [Summary("Remove your Twitch integration")]
        public async Task DeleteTwitchAsync()
        {
            var guildId = Context.Guild.Id.ToString();
            var userId = Context.User.Id.ToString();

            var twitchLinks = LoadTwitchLinks();

            if (!twitchLinks.ContainsKey(guildId) || !twitchLinks[guildId].ContainsKey(userId))
            {
                await ReplyAsync("❌ You don't have any Twitch data linked in this server.");
                return;
            }

            var userData = twitchLinks[guildId][userId];
            var username = userData.TwitchUsername;

            twitchLinks[guildId].Remove(userId);

            if (twitchLinks[guildId].Count == 0)
                twitchLinks.Remove(guildId);

            SaveTwitchLinks(twitchLinks);

            await ReplyAsync($"✅ Your Twitch data for **{username}** has been successfully deleted! 🗑️");
        }

        [Command("testtwitch")]
        [Alias("testingtwitch")]
        [Summary("Test clip posting by fetching your latest Twitch clip")]
        public async Task TestTwitchAsync()
        {
            var guildId = Context.Guild.Id.ToString();
            var userId = Context.User.Id.ToString();

            var twitchLinks = LoadTwitchLinks();

            if (!twitchLinks.ContainsKey(guildId) || !twitchLinks[guildId].ContainsKey(userId))
            {
                await ReplyAsync("❌ You need to set up Twitch first with `!settwitch`");
                return;
            }

            var userData = twitchLinks[guildId][userId];
            var twitchUsername = userData.TwitchUsername;
            var clipChannelId = ulong.Parse(userData.ClipChannelId);

            var clipChannel = Context.Guild.GetTextChannel(clipChannelId);
            if (clipChannel == null)
            {
                await ReplyAsync("❌ Clip channel not found! Please update your settings with `!settwitch`");
                return;
            }

            await ReplyAsync($"🔍 Fetching latest clip from **{twitchUsername}**...");

            try
            {
                // Get Twitch App Access Token
                var tokenUrl = "https://id.twitch.tv/oauth2/token";
                var tokenParams = new Dictionary<string, string>
                {
                    { "client_id", TwitchClientId },
                    { "client_secret", TwitchClientSecret },
                    { "grant_type", "client_credentials" }
                };

                var tokenResponse = await _httpClient.PostAsync(tokenUrl, new FormUrlEncodedContent(tokenParams));
                var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);
                var accessToken = tokenData.GetProperty("access_token").GetString();

                // Get Broadcaster ID
                var userRequest = new HttpRequestMessage(HttpMethod.Get, $"https://api.twitch.tv/helix/users?login={twitchUsername}");
                userRequest.Headers.Add("Client-ID", TwitchClientId);
                userRequest.Headers.Add("Authorization", $"Bearer {accessToken}");

                var userResponse = await _httpClient.SendAsync(userRequest);
                var userJson = await userResponse.Content.ReadAsStringAsync();
                var userData_api = JsonSerializer.Deserialize<JsonElement>(userJson);

                if (!userData_api.GetProperty("data").EnumerateArray().Any())
                {
                    await ReplyAsync($"❌ Twitch user **{twitchUsername}** not found!");
                    return;
                }

                var broadcasterId = userData_api.GetProperty("data")[0].GetProperty("id").GetString();

                // Get Latest Clip
                var clipsRequest = new HttpRequestMessage(HttpMethod.Get,
                    $"https://api.twitch.tv/helix/clips?broadcaster_id={broadcasterId}&first=1");
                clipsRequest.Headers.Add("Client-ID", TwitchClientId);
                clipsRequest.Headers.Add("Authorization", $"Bearer {accessToken}");

                var clipsResponse = await _httpClient.SendAsync(clipsRequest);
                var clipsJson = await clipsResponse.Content.ReadAsStringAsync();
                var clipsData = JsonSerializer.Deserialize<JsonElement>(clipsJson);

                if (!clipsData.GetProperty("data").EnumerateArray().Any())
                {
                    await ReplyAsync($"❌ No clips found for **{twitchUsername}**!");
                    return;
                }

                var clip = clipsData.GetProperty("data")[0];
                var clipTitle = clip.GetProperty("title").GetString();
                var clipUrl = clip.GetProperty("url").GetString();
                var clipCreator = clip.GetProperty("creator_name").GetString();
                var clipViews = clip.GetProperty("view_count").GetInt32();
                var clipThumbnail = clip.GetProperty("thumbnail_url").GetString();

                // Post clip to channel
                var clipEmbed = new EmbedBuilder()
                    .WithColor(0x9147ff)
                    .WithAuthor(twitchUsername, iconUrl: "https://i.imgur.com/aw5WxpI.png")
                    .WithTitle($"🎬 {clipTitle}")
                    .WithUrl(clipUrl)
                    .WithDescription($"Clipped by **{clipCreator}**\n👁️ {clipViews:N0} views")
                    .WithImageUrl(clipThumbnail)
                    .WithFooter("Twitch Clip")
                    .WithCurrentTimestamp()
                    .Build();

                await clipChannel.SendMessageAsync(embed: clipEmbed);
                await ReplyAsync($"✅ Test successful! Clip posted to <#{clipChannelId}>");
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Failed to fetch clip: {ex.Message}\n" +
                               "Make sure your Twitch credentials are configured correctly.");
            }
        }

        #region Helper Methods

        private async Task<IUserMessage> NextMessageAsync(TimeSpan timeout, bool fromSameUser = true)
        {
            var tcs = new TaskCompletionSource<IUserMessage>();

            async Task Handler(SocketMessage msg)
            {
                if (msg is IUserMessage userMsg &&
                    msg.Channel.Id == Context.Channel.Id &&
                    (!fromSameUser || msg.Author.Id == Context.User.Id) &&
                    !msg.Author.IsBot)
                {
                    tcs.TrySetResult(userMsg);
                }
                await Task.CompletedTask;
            }

            Context.Client.MessageReceived += Handler;

            var timeoutTask = Task.Delay(timeout);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            Context.Client.MessageReceived -= Handler;

            return completedTask == tcs.Task ? await tcs.Task : null;
        }

        #endregion
    }

    public class TwitchUserData
    {
        public string TwitchUsername { get; set; }
        public string ClipChannelId { get; set; }
        public DateTime LinkedAt { get; set; }
        public string AccessToken { get; set; } // For future OAuth implementation
    }
}
