using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace MainbotCSharp.Modules
{
    // Verify Service Classes
    public class VerifyConfigEntry
    {
        public ulong ChannelId { get; set; }
        public ulong RoleId { get; set; }
        public ulong? MessageId { get; set; }
        public Dictionary<ulong, bool?> Snapshot { get; set; } = new Dictionary<ulong, bool?>();
    }

    public static class VerifyService
    {
        private const string VERIFY_FILE = "main_verify_config.json";
        private static Dictionary<ulong, VerifyConfigEntry> _cfg = LoadVerifyConfig();

        private static Dictionary<ulong, VerifyConfigEntry> LoadVerifyConfig()
        {
            try
            {
                if (!File.Exists(VERIFY_FILE)) return new Dictionary<ulong, VerifyConfigEntry>();
                var txt = File.ReadAllText(VERIFY_FILE);
                var d = JsonSerializer.Deserialize<Dictionary<ulong, VerifyConfigEntry>>(txt);
                return d ?? new Dictionary<ulong, VerifyConfigEntry>();
            }
            catch
            {
                return new Dictionary<ulong, VerifyConfigEntry>();
            }
        }

        private static void SaveVerifyConfig()
        {
            try
            {
                var txt = JsonSerializer.Serialize(_cfg, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(VERIFY_FILE, txt);
            }
            catch { }
        }

        public static VerifyConfigEntry GetConfig(ulong guildId)
        {
            if (_cfg.TryGetValue(guildId, out var e)) return e; return null;
        }

        public static void SetConfig(ulong guildId, VerifyConfigEntry config)
        {
            _cfg[guildId] = config; SaveVerifyConfig();
        }

        public static void RemoveConfig(ulong guildId)
        {
            if (_cfg.ContainsKey(guildId)) { _cfg.Remove(guildId); SaveVerifyConfig(); }
        }
    }

    public class VerifyCommands : ModuleBase<SocketCommandContext>
    {
        [Command("munga-verify")]
        [Summary("Setup verification system (Admin only)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetupVerifyAsync(IRole role)
        {
            try
            {
                var embed = new EmbedBuilder()
                    .WithTitle("✅ Server Verification")
                    .WithDescription("Click the button below to get verified and access the server!")
                    .WithColor(Color.Green)
                    .WithFooter("One click verification");

                var button = new ComponentBuilder()
                    .WithButton("Verify Me", "verify_button", ButtonStyle.Success, new Emoji("✅"));

                var message = await Context.Channel.SendMessageAsync(embed: embed.Build(), components: button.Build());

                var config = new VerifyConfigEntry
                {
                    ChannelId = Context.Channel.Id,
                    RoleId = role.Id,
                    MessageId = message.Id
                };

                VerifyService.SetConfig(Context.Guild.Id, config);
                await ReplyAsync($"✅ Verification system setup! Verified users will receive {role.Mention}");
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Setup failed: {ex.Message}");
            }
        }

        [Command("verify-status")]
        [Summary("Check verification system status (Admin only)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task VerifyStatusAsync()
        {
            try
            {
                var config = VerifyService.GetConfig(Context.Guild.Id);
                if (config == null)
                {
                    await ReplyAsync("❌ Verification system not configured.");
                    return;
                }

                var role = Context.Guild.GetRole(config.RoleId);
                var channel = Context.Guild.GetTextChannel(config.ChannelId);

                var embed = new EmbedBuilder()
                    .WithTitle("✅ Verification System Status")
                    .WithColor(Color.Green)
                    .AddField("Channel", channel?.Mention ?? "Not found", true)
                    .AddField("Role", role?.Mention ?? "Not found", true)
                    .AddField("Message ID", config.MessageId?.ToString() ?? "None", true)
                    .AddField("Verified Users", config.Snapshot.Count(s => s.Value == true).ToString(), true);

                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Failed to get status: {ex.Message}");
            }
        }

        [Command("verify-remove")]
        [Summary("Remove verification system (Admin only)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task RemoveVerifyAsync()
        {
            try
            {
                VerifyService.RemoveConfig(Context.Guild.Id);
                await ReplyAsync("✅ Verification system removed.");
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Failed to remove: {ex.Message}");
            }
        }
    }
}
