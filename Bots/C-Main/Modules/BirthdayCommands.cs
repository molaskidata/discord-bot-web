using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MainbotCSharp.Modules
{
    public class BirthdayCommands : ModuleBase<SocketCommandContext>
    {
        [Command("birthdaychannel")]
        [Summary("Set or create birthday notification channel (Admin only)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task BirthdayChannelAsync([Remainder] string? arg = null)
        {
            if (string.IsNullOrWhiteSpace(arg))
            {
                await ReplyAsync("❌ Please provide either a channel ID or use `!new-birthchan` to create a new channel.\n\n**Usage:**\n`!birthdaychannel CHANNEL_ID` - Use existing channel\n`!birthdaychannel !new-birthchan` - Create new channel");
                return;
            }

            arg = arg.Trim();

            if (arg == "!new-birthchan")
            {
                try
                {
                    var newChannel = await Context.Guild.CreateTextChannelAsync("★-birthday-wishes");

                    BirthdayService.SetBirthdayChannel(Context.Guild.Id, newChannel.Id);

                    var embed = new EmbedBuilder()
                        .WithTitle("🎂 Birthday Channel Created!")
                        .WithColor(0xFFD700)
                        .AddField("Channel", newChannel.Mention, true)
                        .AddField("Status", "✅ Active", true)
                        .WithDescription("Birthday wishes will be sent here automatically!")
                        .WithTimestamp(DateTimeOffset.UtcNow);

                    await ReplyAsync(embed: embed.Build());
                    await newChannel.SendMessageAsync("🎉 This channel is now set up for birthday wishes!");
                }
                catch (Exception ex)
                {
                    await ReplyAsync($"❌ Failed to create birthday channel: {ex.Message}");
                }
            }
            else
            {
                // Try to parse as channel ID
                if (ulong.TryParse(arg, out ulong channelId))
                {
                    var channel = Context.Guild.GetTextChannel(channelId);
                    if (channel == null)
                    {
                        await ReplyAsync("❌ Channel not found! Please provide a valid channel ID from this server.");
                        return;
                    }

                    BirthdayService.SetBirthdayChannel(Context.Guild.Id, channelId);

                    var embed = new EmbedBuilder()
                        .WithTitle("🎂 Birthday Channel Set!")
                        .WithColor(0xFFD700)
                        .AddField("Channel", channel.Mention, true)
                        .AddField("Status", "✅ Active", true)
                        .WithDescription("Birthday wishes will be sent here automatically!")
                        .WithTimestamp(DateTimeOffset.UtcNow);

                    await ReplyAsync(embed: embed.Build());
                }
                else
                {
                    await ReplyAsync("❌ Invalid channel ID! Please provide a valid numeric channel ID or use `!new-birthchan`.");
                }
            }
        }

        [Command("birthdayset")]
        [Summary("Set your birthday (format: dd.mm.yyyy)")]
        public async Task BirthdaySetAsync([Remainder] string? birthday = null)
        {
            // Delete user's command message for privacy
            try { await Context.Message.DeleteAsync(); } catch { }

            if (string.IsNullOrWhiteSpace(birthday))
            {
                var msg = await ReplyAsync("❌ Please provide your birthday in the format: `dd.mm.yyyy`\n**Example:** `!birthdayset 25.12.1995`");
                _ = Task.Delay(10000).ContinueWith(async _ => { try { await msg.DeleteAsync(); } catch { } });
                return;
            }

            var data = BirthdayService.GetGuildBirthdayData(Context.Guild.Id);
            if (data == null || data.ChannelId == 0)
            {
                var msg = await ReplyAsync("❌ Birthday channel not set! An administrator needs to run `!birthdaychannel` first.");
                _ = Task.Delay(10000).ContinueWith(async _ => { try { await msg.DeleteAsync(); } catch { } });
                return;
            }

            // Convert dd.mm.yyyy to dd/mm/yyyy for internal storage
            birthday = birthday.Replace('.', '/');

            if (BirthdayService.SetUserBirthday(Context.Guild.Id, Context.User.Id, birthday))
            {
                var embed = new EmbedBuilder()
                    .WithTitle("🎂 Birthday Saved!")
                    .WithColor(0xFFD700)
                    .AddField("User", Context.User.Mention, true)
                    .AddField("Birthday", birthday.Substring(0, 5), true) // Only show dd/MM for privacy
                    .WithDescription("Your birthday has been saved! You'll receive wishes on your special day! 🎉")
                    .WithTimestamp(DateTimeOffset.UtcNow);

                var reply = await ReplyAsync(embed: embed.Build());

                // Auto-delete confirmation after 15 seconds
                _ = Task.Delay(15000).ContinueWith(async _ => { try { await reply.DeleteAsync(); } catch { } });
            }
            else
            {
                var msg = await ReplyAsync("❌ Invalid date format! Please use: `dd.mm.yyyy`\n**Example:** `25.12.1995`");
                _ = Task.Delay(10000).ContinueWith(async _ => { try { await msg.DeleteAsync(); } catch { } });
            }
        }

        [Command("birthdaylist")]
        [Summary("Show all birthdays in the server sorted by date")]
        public async Task BirthdayListAsync()
        {
            var data = BirthdayService.GetGuildBirthdayData(Context.Guild.Id);
            if (data == null || !data.Users.Any())
            {
                await ReplyAsync("❌ No birthdays have been set yet! Use `!birthdayset dd.mm.yyyy` to add yours.");
                return;
            }

            try
            {
                // Sort birthdays by month and day
                var sortedBirthdays = data.Users
                    .Select(kvp => new
                    {
                        UserId = kvp.Key,
                        Birthday = kvp.Value,
                        Parts = kvp.Value.Split('/'),
                    })
                    .Select(x => new
                    {
                        x.UserId,
                        x.Birthday,
                        Day = int.Parse(x.Parts[0]),
                        Month = int.Parse(x.Parts[1]),
                        Year = x.Parts.Length > 2 ? x.Parts[2] : "????"
                    })
                    .OrderBy(x => x.Month)
                    .ThenBy(x => x.Day)
                    .ToList();

                var embed = new EmbedBuilder()
                    .WithTitle($"🎂 {Context.Guild.Name} - Birthday List")
                    .WithColor(0xFFD700)
                    .WithDescription($"**{sortedBirthdays.Count}** members with birthdays registered")
                    .WithTimestamp(DateTimeOffset.UtcNow);

                // Group by month for better readability
                var monthNames = new[] { "", "January", "February", "March", "April", "May", "June",
                                        "July", "August", "September", "October", "November", "December" };

                var grouped = sortedBirthdays.GroupBy(x => x.Month);

                foreach (var monthGroup in grouped)
                {
                    var monthName = monthNames[monthGroup.Key];
                    var birthdayList = string.Join("\n", monthGroup.Select(b =>
                    {
                        var user = Context.Guild.GetUser(b.UserId);
                        var userName = user != null ? user.Username : $"Unknown User ({b.UserId})";
                        return $"🎈 **{b.Day:D2}/{b.Month:D2}** - {userName}";
                    }));

                    embed.AddField($"📅 {monthName}", birthdayList, false);
                }

                embed.WithFooter($"Use !birthdayset dd.mm.yyyy to add your birthday | Channel: #{Context.Guild.GetTextChannel(data.ChannelId)?.Name ?? "Not set"}");

                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Error displaying birthday list: {ex.Message}");
            }
        }

        [Command("birthdayremove")]
        [Alias("mybirthdayremove")]
        [Summary("Remove your birthday from the system")]
        public async Task BirthdayRemoveAsync()
        {
            if (BirthdayService.RemoveUserBirthday(Context.Guild.Id, Context.User.Id))
            {
                await ReplyAsync($"✅ {Context.User.Mention}, your birthday has been removed from the system.");
            }
            else
            {
                await ReplyAsync("❌ You don't have a birthday set in this server.");
            }
        }

        [Command("mybirthdayinfo")]
        [Summary("Show your saved birthday info")]
        public async Task MyBirthdayInfoAsync()
        {
            var data = BirthdayService.GetGuildBirthdayData(Context.Guild.Id);
            if (data == null || !data.Users.TryGetValue(Context.User.Id, out var birthday))
            {
                await ReplyAsync("❌ You haven't set your birthday yet. Use `!birthdayset dd.mm.yyyy`");
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle("🎂 Your Birthday Info")
                .WithColor(0xFFD700)
                .AddField("User", Context.User.Mention, true)
                .AddField("Birthday", birthday.Substring(0, 5), true) // Only show dd/MM
                .AddField("Channel", $"<#{data.ChannelId}>", true)
                .WithDescription("Your birthday is saved and you'll receive wishes on your special day!")
                .WithTimestamp(DateTimeOffset.UtcNow);

            await ReplyAsync(embed: embed.Build());
        }
    }
}
