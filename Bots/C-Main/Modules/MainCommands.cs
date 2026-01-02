using System.Threading.Tasks;
using Discord.Commands;
using Discord;
using Discord.WebSocket;
using MainbotCSharp.Services;
using System.Linq;
using System.Text.RegularExpressions;

namespace MainbotCSharp.Modules
{
    public class MainCommands : ModuleBase<SocketCommandContext>
    {
        [Command("ping")]
        [Summary("Check bot latency")]
        public async Task PingAsync()
        {
            var latency = Context.Client is Discord.WebSocket.DiscordSocketClient c ? c.Latency : 0;
            await ReplyAsync($"Pong! Latency: {latency}ms");
        }

        [Command("help")]
        [Summary("Show help information")]
        public async Task HelpAsync()
        {
            var eb = new EmbedBuilder()
                .WithTitle("🤖 Mainbot Help")
                .WithDescription("Available commands:")
                .AddField("Basic", "`!ping` - Check bot latency\n`!help` - Show this help", false)
                .AddField("Birthday System", "`!birthdaychannel` - Set birthday notification channel (Admin)\n`!birthdayset` - Set your birthday (dd/mm/yyyy)\n`!birthdayremove` - Remove your birthday\n`!birthdaylist` - Show all birthdays", false)
                .AddField("Voice System", "`!voicename` - Rename your voice channel\n`!voicelimit` - Set user limit\n`!voiceprivate/public` - Change privacy\n`!voicehelp` - Voice commands help", false)
                .WithColor(Color.DarkBlue)
                .WithFooter("More commands available via other modules (Security, Tickets, Verify)");
            await ReplyAsync(embed: eb.Build());
        }

        // Placeholder for security moderation trigger - this mimics the JS handler invocation
        [Command("debugsecurity")]
        [Summary("Run a quick security check on the message content (admin only)")]
        public async Task DebugSecurityAsync([Remainder] string text = null)
        {
            var guser = Context.User as SocketGuildUser;
            if (!Context.User.IsBot && Context.Guild != null && guser != null && !guser.Roles.Any(r => !r.IsEveryone))
            {
                // implement security checks when porting
            }
            await ReplyAsync("Security check placeholder (not implemented yet)");
        }

        // Birthday Commands
        [Command("birthdaychannel")]
        [Summary("Set the birthday notification channel (Admin only)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetBirthdayChannelAsync()
        {
            try
            {
                BirthdayService.SetBirthdayChannel(Context.Guild.Id, Context.Channel.Id);
                await ReplyAsync($"✅ Birthday notifications will now be sent to {Context.Channel.Mention}");
            }
            catch (Exception ex)
            {
                await ReplyAsync("❌ Error setting birthday channel: " + ex.Message);
            }
        }

        [Command("birthdayset")]
        [Summary("Set your birthday (format: dd/mm/yyyy)")]
        public async Task SetBirthdayAsync([Remainder] string birthday = null)
        {
            try
            {
                // Delete user message for privacy
                try { await Context.Message.DeleteAsync(); } catch { }

                if (string.IsNullOrWhiteSpace(birthday))
                {
                    var msg = await ReplyAsync("Please provide your birthday in format: dd/mm/yyyy (e.g., 25/12/1995)");

                    // Wait for user response using message collector pattern
                    var filter = Discord.Addons.Interactive.Criteria.EnsureSourceUserCriteria();
                    var response = await Context.Channel.GetMessagesAsync(1).FlattenAsync();

                    // Simple timeout approach - wait for next message from user
                    var userMessages = Context.Channel.GetMessagesAsync(50).FlattenAsync();
                    await foreach (var userMsg in userMessages)
                    {
                        if (userMsg.Author.Id == Context.User.Id && userMsg.Id != Context.Message.Id)
                        {
                            birthday = userMsg.Content?.Trim();
                            try { await userMsg.DeleteAsync(); } catch { }
                            break;
                        }
                    }

                    try { await msg.DeleteAsync(); } catch { }
                }

                if (string.IsNullOrWhiteSpace(birthday))
                {
                    var errorMsg = await ReplyAsync("❌ No birthday provided. Use: `!birthdayset dd/mm/yyyy`");
                    _ = Task.Run(async () => { await Task.Delay(5000); try { await errorMsg.DeleteAsync(); } catch { } });
                    return;
                }

                if (BirthdayService.SetUserBirthday(Context.Guild.Id, Context.User.Id, birthday))
                {
                    var successMsg = await ReplyAsync($"🎉 Birthday set for <@{Context.User.Id}>!");
                    _ = Task.Run(async () => { await Task.Delay(3000); try { await successMsg.DeleteAsync(); } catch { } });
                }
                else
                {
                    var errorMsg = await ReplyAsync("❌ Invalid date format. Please use: dd/mm/yyyy (e.g., 25/12/1995)");
                    _ = Task.Run(async () => { await Task.Delay(5000); try { await errorMsg.DeleteAsync(); } catch { } });
                }
            }
            catch (Exception ex)
            {
                await ReplyAsync("❌ Error setting birthday: " + ex.Message);
            }
        }

        [Command("birthdayremove")]
        [Summary("Remove your birthday from the system")]
        public async Task RemoveBirthdayAsync()
        {
            try
            {
                // Delete user message for privacy
                try { await Context.Message.DeleteAsync(); } catch { }

                if (BirthdayService.RemoveUserBirthday(Context.Guild.Id, Context.User.Id))
                {
                    var msg = await ReplyAsync($"🗑️ Birthday removed for <@{Context.User.Id}>");
                    _ = Task.Run(async () => { await Task.Delay(3000); try { await msg.DeleteAsync(); } catch { } });
                }
                else
                {
                    var msg = await ReplyAsync("❌ No birthday found to remove.");
                    _ = Task.Run(async () => { await Task.Delay(3000); try { await msg.DeleteAsync(); } catch { } });
                }
            }
            catch (Exception ex)
            {
                await ReplyAsync("❌ Error removing birthday: " + ex.Message);
            }
        }

        [Command("birthdaylist")]
        [Summary("Show all birthdays in this server")]
        public async Task ListBirthdaysAsync()
        {
            try
            {
                var data = BirthdayService.GetGuildBirthdayData(Context.Guild.Id);
                if (data == null || !data.Users.Any())
                {
                    await ReplyAsync("📅 No birthdays set in this server yet.");
                    return;
                }

                var eb = new EmbedBuilder()
                    .WithTitle("🎂 Server Birthdays")
                    .WithColor(Color.Gold)
                    .WithFooter($"Notifications will be sent to: {(data.ChannelId != 0 ? $"<#{data.ChannelId}>" : "Not set")}");

                var birthdayList = data.Users
                    .OrderBy(kvp =>
                    {
                        var parts = kvp.Value.Split('/');
                        return new DateTime(2000, int.Parse(parts[1]), int.Parse(parts[0]));
                    })
                    .Select(kvp => $"<@{kvp.Key}> - {kvp.Value.Substring(0, 5)}") // Only show dd/mm, not year
                    .ToList();

                // Split into chunks if too many birthdays
                const int maxPerField = 15;
                for (int i = 0; i < birthdayList.Count; i += maxPerField)
                {
                    var chunk = birthdayList.Skip(i).Take(maxPerField);
                    var fieldName = birthdayList.Count > maxPerField ? $"Birthdays ({i + 1}-{Math.Min(i + maxPerField, birthdayList.Count)})" : "Birthdays";
                    eb.AddField(fieldName, string.Join("\n", chunk), false);
                }

                await ReplyAsync(embed: eb.Build());
            }
            catch (Exception ex)
            {
                await ReplyAsync("❌ Error listing birthdays: " + ex.Message);
            }
        }
    }
}
