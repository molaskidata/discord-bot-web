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
        [Summary("Link Twitch account")]
        public async Task SetTwitchAsync()
        {
            var loadingMsg = await Context.Channel.SendMessageAsync("⏳ **Lade Twitch Setup...**");

            try
            {
                var guildId = Context.Guild.Id.ToString();
                var userId = Context.User.Id.ToString();

                await loadingMsg.ModifyAsync(m => m.Content = "Write your correct Twitch username in the Channel to synchronize and connect with Discord. Format: example");

                // Wait for username (filter: no ! prefix)
                var username = await WaitForMessageAsync(msg => !msg.Content.StartsWith("!"), timeoutSeconds: 60, loadingMsg);
                if (username == null)
                {
                    await loadingMsg.ModifyAsync(m => m.Content = "⏰ **Timeout!** Du hast zu lange nicht geantwortet.");
                    return;
                }

                var twitchUsername = username.Trim();
                await loadingMsg.ModifyAsync(m => m.Content = $"⏳ **Lade Twitch-Daten für {twitchUsername}...**");

                var twitchLinks = LoadTwitchLinks();

                if (!twitchLinks.ContainsKey(guildId))
                    twitchLinks[guildId] = new Dictionary<string, TwitchUserData>();

                var existingData = twitchLinks[guildId].ContainsKey(userId) ? twitchLinks[guildId][userId] : null;

                if (existingData != null)
                {
                    // User already has Twitch - ask what to update
                    await loadingMsg.ModifyAsync(m => m.Content =
                        $"Du hast bereits **{existingData.TwitchUsername}** verlinkt.\n" +
                        $"Channel: <#{existingData.ClipChannelId}>\n\n" +
                        "Was möchtest du ändern?\n" +
                        "1️⃣ - Username ändern\n" +
                        "2️⃣ - Channel ändern\n" +
                        "3️⃣ - Abbrechen\n\n" +
                        "Schreib 1, 2 oder 3");

                    var choiceMsg = await WaitForMessageAsync(timeoutSeconds: 60, loadingMsg: loadingMsg);
                    if (choiceMsg == null)
                    {
                        await loadingMsg.ModifyAsync(m => m.Content = "⏰ **Timeout!** Vorgang abgebrochen.");
                        return;
                    }

                    switch (choiceMsg.Trim())
                    {
                        case "1":
                            await loadingMsg.ModifyAsync(m => m.Content = "Schreib deinen neuen Twitch Username:");
                            var newUsername = await WaitForMessageAsync(timeoutSeconds: 60, loadingMsg: loadingMsg);
                            if (newUsername != null)
                            {
                                existingData.TwitchUsername = newUsername.Trim();
                                SaveTwitchLinks(twitchLinks);
                                await loadingMsg.ModifyAsync(m => m.Content = $"✅ Username geändert zu **{newUsername.Trim()}**");
                            }
                            else
                            {
                                await loadingMsg.ModifyAsync(m => m.Content = "⏰ **Timeout!** Vorgang abgebrochen.");
                            }
                            return;

                        case "2":
                            await loadingMsg.ModifyAsync(m => m.Content = "Schreib die Channel ID wo Clips gepostet werden sollen:");
                            var newChanId = await WaitForMessageAsync(timeoutSeconds: 60, loadingMsg: loadingMsg);
                            if (newChanId != null && ulong.TryParse(newChanId.Trim(), out var chanId))
                            {
                                existingData.ClipChannelId = chanId.ToString();
                                SaveTwitchLinks(twitchLinks);
                                await loadingMsg.ModifyAsync(m => m.Content = $"✅ Channel geändert zu <#{chanId}>");
                            }
                            else
                            {
                                await loadingMsg.ModifyAsync(m => m.Content = "❌ Ungültige Channel ID oder Timeout!");
                            }
                            return;

                        case "3":
                            await loadingMsg.ModifyAsync(m => m.Content = "Abgebrochen.");
                            return;

                        default:
                            await loadingMsg.ModifyAsync(m => m.Content = "Ungültige Auswahl. Abgebrochen.");
                            return;
                    }
                }

                // NEW SETUP - Get channel
                await loadingMsg.ModifyAsync(m => m.Content =
                    "Schreib die Channel ID wo Clips gepostet werden sollen\n" +
                    "ODER schreib **!setchannel** um einen neuen Thread-Channel zu erstellen:");

                var channelInput = await WaitForMessageAsync(timeoutSeconds: 60, loadingMsg: loadingMsg);
                if (channelInput == null)
                {
                    await loadingMsg.ModifyAsync(m => m.Content = "⏰ **Timeout!** Vorgang abgebrochen.");
                    return;
                }

                ulong clipChannelId = 0;

                if (channelInput.Trim().ToLower() == "!setchannel")
                {
                    // Create thread-only channel
                    await loadingMsg.ModifyAsync(m => m.Content = "⏳ **Erstelle Channel...**");
                    try
                    {
                        var newChan = await Context.Guild.CreateTextChannelAsync($"clips-{twitchUsername.ToLower()}");
                        clipChannelId = newChan.Id;
                        await loadingMsg.ModifyAsync(m => m.Content = $"✅ Channel erstellt: <#{clipChannelId}>");
                    }
                    catch (Exception ex)
                    {
                        await loadingMsg.ModifyAsync(m => m.Content = $"❌ Fehler beim Erstellen: {ex.Message}");
                        return;
                    }
                }
                else if (ulong.TryParse(channelInput.Trim(), out clipChannelId))
                {
                    var chan = Context.Guild.GetTextChannel(clipChannelId);
                    if (chan == null)
                    {
                        await loadingMsg.ModifyAsync(m => m.Content = "❌ Channel nicht gefunden!");
                        return;
                    }
                }
                else
                {
                    await loadingMsg.ModifyAsync(m => m.Content = "❌ Ungültige Eingabe!");
                    return;
                }

                // Save
                await loadingMsg.ModifyAsync(m => m.Content = "⏳ **Speichere Daten...**");
                twitchLinks[guildId][userId] = new TwitchUserData
                {
                    TwitchUsername = twitchUsername,
                    ClipChannelId = clipChannelId.ToString()
                };
                SaveTwitchLinks(twitchLinks);

                await loadingMsg.ModifyAsync(m => m.Content =
                    $"✅ **Twitch Setup abgeschlossen!**\n\n" +
                    $"👤 Username: **{twitchUsername}**\n" +
                    $"📺 Clip Channel: <#{clipChannelId}>\n\n" +
                    $"Nutze `!testtwitch` um einen Test-Clip zu posten!");
            }
            catch (Exception ex)
            {
                await loadingMsg.ModifyAsync(m => m.Content = $"❌ **Fehler:** {ex.Message}");
            }
        }

        [Command("deletetwitch")]
        [Summary("Delete Twitch data")]
        public async Task DeleteTwitchAsync()
        {
            var guildId = Context.Guild.Id.ToString();
            var userId = Context.User.Id.ToString();

            var twitchLinks = LoadTwitchLinks();

            if (!twitchLinks.ContainsKey(guildId) || !twitchLinks[guildId].ContainsKey(userId))
            {
                await ReplyAsync("❌ Du hast keine Twitch-Daten in diesem Server.");
                return;
            }

            var userData = twitchLinks[guildId][userId];
            var username = userData.TwitchUsername;

            twitchLinks[guildId].Remove(userId);
            if (twitchLinks[guildId].Count == 0)
                twitchLinks.Remove(guildId);

            SaveTwitchLinks(twitchLinks);
            await ReplyAsync($"✅ Deine Twitch-Daten für **{username}** wurden erfolgreich gelöscht! 🗑️");
        }

        [Command("testtwitch")]
        [Alias("testingtwitch")]
        [Summary("Test clip posting")]
        public async Task TestTwitchAsync()
        {
            var guildId = Context.Guild.Id.ToString();
            var userId = Context.User.Id.ToString();

            var twitchLinks = LoadTwitchLinks();

            if (!twitchLinks.ContainsKey(guildId) || !twitchLinks[guildId].ContainsKey(userId))
            {
                await ReplyAsync("❌ Du musst zuerst `!settwitch` ausführen!");
                return;
            }

            var userData = twitchLinks[guildId][userId];
            var twitchUsername = userData.TwitchUsername;
            var clipChannelId = ulong.Parse(userData.ClipChannelId);

            var clipChannel = Context.Guild.GetTextChannel(clipChannelId);
            if (clipChannel == null)
            {
                await ReplyAsync("❌ Clip-Channel nicht gefunden!");
                return;
            }

            await ReplyAsync($"🔍 Hole neuesten Clip von **{twitchUsername}**...");

            try
            {
                // Get App Access Token
                Console.WriteLine($"[Twitch] Getting access token with ClientID: {TwitchClientId?.Substring(0, 5)}...");

                var tokenParams = new Dictionary<string, string>
                {
                    { "client_id", TwitchClientId },
                    { "client_secret", TwitchClientSecret },
                    { "grant_type", "client_credentials" }
                };

                var tokenResponse = await _httpClient.PostAsync("https://id.twitch.tv/oauth2/token",
                    new FormUrlEncodedContent(tokenParams));

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    var errorContent = await tokenResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"[Twitch] Token request failed: {tokenResponse.StatusCode} - {errorContent}");
                    await ReplyAsync($"Failed to authenticate with Twitch API: {tokenResponse.StatusCode}");
                    return;
                }

                var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"[Twitch] Token response: {tokenJson}");
                var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);
                var accessToken = tokenData.GetProperty("access_token").GetString();
                Console.WriteLine($"[Twitch] Got access token: {accessToken?.Substring(0, 10)}...");

                // Get Broadcaster ID
                Console.WriteLine($"[Twitch] Looking up user: {twitchUsername}");
                var userRequest = new HttpRequestMessage(HttpMethod.Get,
                    $"https://api.twitch.tv/helix/users?login={twitchUsername}");
                userRequest.Headers.Add("Client-ID", TwitchClientId);
                userRequest.Headers.Add("Authorization", $"Bearer {accessToken}");

                var userResponse = await _httpClient.SendAsync(userRequest);

                if (!userResponse.IsSuccessStatusCode)
                {
                    var errorContent = await userResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"[Twitch] User lookup failed: {userResponse.StatusCode} - {errorContent}");
                    await ReplyAsync($"Failed to lookup Twitch user: {userResponse.StatusCode}");
                    return;
                }

                var userJson = await userResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"[Twitch] User response: {userJson}");
                var userDataApi = JsonSerializer.Deserialize<JsonElement>(userJson);

                if (!userDataApi.GetProperty("data").EnumerateArray().Any())
                {
                    await ReplyAsync($"❌ Twitch User **{twitchUsername}** nicht gefunden!");
                    return;
                }

                var broadcasterId = userDataApi.GetProperty("data")[0].GetProperty("id").GetString();
                Console.WriteLine($"[Twitch] Broadcaster ID: {broadcasterId}");

                // Get Latest Clip
                Console.WriteLine($"[Twitch] Fetching clips for broadcaster: {broadcasterId}");
                var clipsRequest = new HttpRequestMessage(HttpMethod.Get,
                    $"https://api.twitch.tv/helix/clips?broadcaster_id={broadcasterId}&first=1");
                clipsRequest.Headers.Add("Client-ID", TwitchClientId);
                clipsRequest.Headers.Add("Authorization", $"Bearer {accessToken}");

                var clipsResponse = await _httpClient.SendAsync(clipsRequest);

                if (!clipsResponse.IsSuccessStatusCode)
                {
                    var errorContent = await clipsResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"[Twitch] Clips request failed: {clipsResponse.StatusCode} - {errorContent}");
                    await ReplyAsync($"Failed to fetch clips: {clipsResponse.StatusCode}");
                    return;
                }

                var clipsJson = await clipsResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"[Twitch] Clips response: {clipsJson}");
                var clipsData = JsonSerializer.Deserialize<JsonElement>(clipsJson);

                if (!clipsData.GetProperty("data").EnumerateArray().Any())
                {
                    await ReplyAsync($"❌ Keine Clips für **{twitchUsername}** gefunden!");
                    return;
                }

                var clip = clipsData.GetProperty("data")[0];
                var clipTitle = clip.GetProperty("title").GetString();
                var clipUrl = clip.GetProperty("url").GetString();
                var clipCreator = clip.GetProperty("creator_name").GetString();
                var clipViews = clip.GetProperty("view_count").GetInt32();
                var clipThumbnail = clip.GetProperty("thumbnail_url").GetString();

                // Post clip to channel
                var embed = new EmbedBuilder()
                    .WithColor(0x9147ff)
                    .WithAuthor(twitchUsername, "https://i.imgur.com/aw5WxpI.png")
                    .WithTitle($"🎬 {clipTitle}")
                    .WithUrl(clipUrl)
                    .WithDescription($"Geclippt von **{clipCreator}**\n👁️ {clipViews:N0} Views")
                    .WithImageUrl(clipThumbnail)
                    .WithFooter("Twitch Clip")
                    .WithCurrentTimestamp()
                    .Build();

                await clipChannel.SendMessageAsync(embed: embed);
                await ReplyAsync($"✅ Test erfolgreich! Clip wurde in <#{clipChannelId}> gepostet!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Twitch] Exception in TestTwitch: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"[Twitch] Stack trace: {ex.StackTrace}"); await ReplyAsync($"❌ Fehler beim Abrufen des Clips: {ex.Message}");
            }
        }

        #region Helper

        private async Task<string?> WaitForMessageAsync(Func<SocketMessage, bool>? filter = null, int timeoutSeconds = 60, IUserMessage? loadingMsg = null)
        {
            var tcs = new TaskCompletionSource<string?>();
            var startTime = DateTime.UtcNow;
            var updateInterval = 10; // Update countdown every 10 seconds
            var lastUpdate = DateTime.UtcNow;

            Task Handler(SocketMessage msg)
            {
                if (msg.Channel.Id == Context.Channel.Id &&
                    msg.Author.Id == Context.User.Id &&
                    !msg.Author.IsBot &&
                    !string.IsNullOrWhiteSpace(msg.Content))
                {
                    if (filter == null || filter(msg))
                    {
                        tcs.TrySetResult(msg.Content.Trim());
                    }
                }
                return Task.CompletedTask;
            }

            Context.Client.MessageReceived += Handler;

            // Countdown timer task
            var countdownTask = Task.Run(async () =>
            {
                while (!tcs.Task.IsCompleted)
                {
                    await Task.Delay(1000);
                    var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                    var remaining = timeoutSeconds - (int)elapsed;

                    if (remaining <= 0)
                    {
                        tcs.TrySetResult(null);
                        break;
                    }

                    // Update loading message every 10 seconds if provided
                    if (loadingMsg != null && (DateTime.UtcNow - lastUpdate).TotalSeconds >= updateInterval)
                    {
                        try
                        {
                            var currentContent = (await Context.Channel.GetMessageAsync(loadingMsg.Id) as IUserMessage)?.Content ?? "";
                            if (!currentContent.Contains("⏱️"))
                            {
                                await loadingMsg.ModifyAsync(m => m.Content = $"{currentContent}\n\n⏱️ **Timeout in {remaining} Sekunden...**");
                            }
                            else
                            {
                                await loadingMsg.ModifyAsync(m => m.Content = System.Text.RegularExpressions.Regex.Replace(currentContent, @"⏱️ \*\*Timeout in \d+ Sekunden\.\.\.\*\*", $"⏱️ **Timeout in {remaining} Sekunden...**"));
                            }
                            lastUpdate = DateTime.UtcNow;
                        }
                        catch { }
                    }
                }
            });

            await tcs.Task;
            Context.Client.MessageReceived -= Handler;

            return await tcs.Task;
        }

        #endregion
    }

    public class TwitchUserData
    {
        public string TwitchUsername { get; set; } = "";
        public string ClipChannelId { get; set; } = "";
    }
}
