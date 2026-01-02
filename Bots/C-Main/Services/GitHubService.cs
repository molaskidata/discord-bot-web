using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace MainbotCSharp.Services
{
    public class GitHubData
    {
        public string GitHubUsername { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
    }

    public static class GitHubService
    {
        private const string GITHUB_LINKS_FILE = "github_links.json";
        private const string GITHUB_CODER_ROLE_ID = "1440681068708630621";
        private static Dictionary<ulong, GitHubData> _githubLinks = LoadGitHubData();
        private static DiscordSocketClient _client;
        private static readonly HttpClient _httpClient = new HttpClient();

        private static Dictionary<ulong, GitHubData> LoadGitHubData()
        {
            try
            {
                if (!File.Exists(GITHUB_LINKS_FILE))
                    return new Dictionary<ulong, GitHubData>();

                var json = File.ReadAllText(GITHUB_LINKS_FILE);
                return JsonSerializer.Deserialize<Dictionary<ulong, GitHubData>>(json) ?? new Dictionary<ulong, GitHubData>();
            }
            catch (Exception ex)
            {
                Console.WriteLine("GitHub data load error: " + ex.Message);
                return new Dictionary<ulong, GitHubData>();
            }
        }

        private static void SaveGitHubData()
        {
            try
            {
                var json = JsonSerializer.Serialize(_githubLinks, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(GITHUB_LINKS_FILE, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine("GitHub save error: " + ex.Message);
            }
        }

        public static void Initialize(DiscordSocketClient client)
        {
            _client = client;
            Console.WriteLine("GitHubService initialized");
        }

        public static async Task<bool> AssignGitHubCoderRoleAsync(ulong discordId)
        {
            try
            {
                if (_client == null) return false;

                var guild = _client.Guilds.FirstOrDefault();
                if (guild == null) return false;

                var member = guild.GetUser(discordId);
                if (member == null) return false;

                var role = guild.GetRole(ulong.Parse(GITHUB_CODER_ROLE_ID));
                if (role == null) return false;

                await member.AddRoleAsync(role);
                Console.WriteLine($"✅ GitHub-Coder role assigned to {member.Username}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error assigning GitHub-Coder role: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> RemoveGitHubCoderRoleAsync(ulong discordId)
        {
            try
            {
                if (_client == null) return false;

                var guild = _client.Guilds.FirstOrDefault();
                if (guild == null) return false;

                var member = guild.GetUser(discordId);
                if (member == null) return false;

                var role = guild.GetRole(ulong.Parse(GITHUB_CODER_ROLE_ID));
                if (role == null) return false;

                await member.RemoveRoleAsync(role);
                Console.WriteLine($"✅ GitHub-Coder role removed from {member.Username}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing GitHub-Coder role: {ex.Message}");
                return false;
            }
        }

        public static string GenerateOAuthUrl(ulong discordId, string redirectUri)
        {
            var clientId = Environment.GetEnvironmentVariable("GITHUB_CLIENT_ID");
            if (string.IsNullOrEmpty(clientId))
            {
                Console.WriteLine("❌ GITHUB_CLIENT_ID not configured in environment variables");
                return string.Empty;
            }

            var state = Convert.ToBase64String(Encoding.UTF8.GetBytes(discordId.ToString()));
            return $"https://github.com/login/oauth/authorize?client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope=read:user%20repo&state={state}";
        }

        public static async Task<bool> ProcessOAuthCallbackAsync(string code, string state, string redirectUri)
        {
            try
            {
                // Get GitHub credentials from environment
                var clientId = Environment.GetEnvironmentVariable("GITHUB_CLIENT_ID");
                var clientSecret = Environment.GetEnvironmentVariable("GITHUB_CLIENT_SECRET");

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    Console.WriteLine("❌ GitHub OAuth credentials not configured in environment variables");
                    return false;
                }

                // Decode Discord ID from state
                var discordIdBytes = Convert.FromBase64String(state);
                var discordIdString = Encoding.UTF8.GetString(discordIdBytes);
                if (!ulong.TryParse(discordIdString, out var discordId))
                    return false;

                // Exchange code for access token
                var tokenRequest = new
                {
                    client_id = clientId,
                    client_secret = clientSecret,
                    code = code,
                    redirect_uri = redirectUri
                };

                var tokenJson = JsonSerializer.Serialize(tokenRequest);
                var tokenContent = new StringContent(tokenJson, Encoding.UTF8, "application/json");

                var tokenResponse = await _httpClient.PostAsync("https://github.com/login/oauth/access_token", tokenContent);
                var tokenResult = await tokenResponse.Content.ReadAsStringAsync();

                // Parse access token (GitHub returns form-encoded response)
                var tokenParams = System.Web.HttpUtility.ParseQueryString(tokenResult);
                var accessToken = tokenParams["access_token"];

                if (string.IsNullOrEmpty(accessToken))
                    return false;

                // Get user info from GitHub
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"token {accessToken}");
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Discord-Bot-GitHub-Integration");

                var userResponse = await _httpClient.GetAsync("https://api.github.com/user");
                var userJson = await userResponse.Content.ReadAsStringAsync();
                var userInfo = JsonSerializer.Deserialize<JsonElement>(userJson);

                var githubUsername = userInfo.GetProperty("login").GetString();
                if (string.IsNullOrEmpty(githubUsername))
                    return false;

                // Save to our data
                _githubLinks[discordId] = new GitHubData
                {
                    GitHubUsername = githubUsername,
                    AccessToken = accessToken
                };
                SaveGitHubData();

                // Assign role
                await AssignGitHubCoderRoleAsync(discordId);

                Console.WriteLine($"✅ GitHub account linked: Discord {discordId} -> GitHub {githubUsername}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OAuth callback error: {ex.Message}");
                return false;
            }
        }

        public static bool DisconnectGitHub(ulong discordId)
        {
            try
            {
                if (_githubLinks.Remove(discordId))
                {
                    SaveGitHubData();
                    _ = Task.Run(() => RemoveGitHubCoderRoleAsync(discordId)); // Fire and forget
                    Console.WriteLine($"✅ GitHub disconnected for Discord {discordId}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GitHub disconnect error: {ex.Message}");
                return false;
            }
        }

        public static GitHubData GetGitHubData(ulong discordId)
        {
            return _githubLinks.TryGetValue(discordId, out var data) ? data : null;
        }

        public static async Task<List<(string Username, int Commits)>> GetTopCommittersAsync()
        {
            var commitCounts = new Dictionary<string, int>();

            foreach (var kvp in _githubLinks)
            {
                try
                {
                    var data = kvp.Value;
                    _httpClient.DefaultRequestHeaders.Clear();
                    _httpClient.DefaultRequestHeaders.Add("Authorization", $"token {data.AccessToken}");
                    _httpClient.DefaultRequestHeaders.Add("User-Agent", "Discord-Bot-GitHub-Integration");

                    // Get user's repositories
                    var reposResponse = await _httpClient.GetAsync($"https://api.github.com/users/{data.GitHubUsername}/repos?per_page=100");
                    var reposJson = await reposResponse.Content.ReadAsStringAsync();
                    var repos = JsonSerializer.Deserialize<JsonElement>(reposJson);

                    int totalCommits = 0;
                    foreach (var repo in repos.EnumerateArray())
                    {
                        var repoName = repo.GetProperty("name").GetString();
                        var commitsResponse = await _httpClient.GetAsync($"https://api.github.com/repos/{data.GitHubUsername}/{repoName}/commits?author={data.GitHubUsername}&per_page=100");

                        if (commitsResponse.IsSuccessStatusCode)
                        {
                            var commitsJson = await commitsResponse.Content.ReadAsStringAsync();
                            var commits = JsonSerializer.Deserialize<JsonElement>(commitsJson);
                            totalCommits += commits.GetArrayLength();
                        }
                    }

                    commitCounts[data.GitHubUsername] = totalCommits;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error getting commits for {kvp.Value.GitHubUsername}: {ex.Message}");
                }
            }

            return commitCounts.OrderByDescending(kvp => kvp.Value)
                              .Take(10)
                              .Select(kvp => (kvp.Key, kvp.Value))
                              .ToList();
        }

        public static async Task<int> GetUserCommitCountAsync(ulong discordId)
        {
            try
            {
                var data = GetGitHubData(discordId);
                if (data == null) return -1;

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"token {data.AccessToken}");
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Discord-Bot-GitHub-Integration");

                var reposResponse = await _httpClient.GetAsync($"https://api.github.com/users/{data.GitHubUsername}/repos?per_page=100");
                var reposJson = await reposResponse.Content.ReadAsStringAsync();
                var repos = JsonSerializer.Deserialize<JsonElement>(reposJson);

                int totalCommits = 0;
                foreach (var repo in repos.EnumerateArray())
                {
                    var repoName = repo.GetProperty("name").GetString();
                    var commitsResponse = await _httpClient.GetAsync($"https://api.github.com/repos/{data.GitHubUsername}/{repoName}/commits?author={data.GitHubUsername}&per_page=100");

                    if (commitsResponse.IsSuccessStatusCode)
                    {
                        var commitsJson = await commitsResponse.Content.ReadAsStringAsync();
                        var commits = JsonSerializer.Deserialize<JsonElement>(commitsJson);
                        totalCommits += commits.GetArrayLength();
                    }
                }

                return totalCommits;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting user commit count: {ex.Message}");
                return -1;
            }
        }
    }
}