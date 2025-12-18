using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PiratBotCSharp.Services;

namespace PiratBotCSharp.Modules
{
    public class TicketCommands : ModuleBase<SocketCommandContext>
    {
        [Command("munga-supportticket")]
        public async Task PostSupportEmbedAsync()
        {
            if (Context.Guild == null) { await ReplyAsync("This command must be used in a guild."); return; }
            var embed = new EmbedBuilder()
                .WithColor(Color.DarkGrey)
                .WithAuthor("Support Ticket", "https://i.imgur.com/f29ONGJ.png")
                .WithTitle("Create a Support Ticket")
                .WithDescription("Need help? Choose the appropriate option from the menu below to create a support ticket. Provide as much detail as possible when prompted.")
                .WithFooter("Select a category to start a ticket — a staff member will respond.");

            var select = new SelectMenuBuilder()
                .WithPlaceholder("Choose a support category")
                .WithCustomId("support_select")
                .WithMinValues(1)
                .WithMaxValues(1)
                .AddOption("Technical Issue", "support_technical", "Bot or server technical problem", "🛠️")
                .AddOption("Spam / Scam", "support_spam", "Report spam, phishing or scams", "⚠️")
                .AddOption("Abuse / Harassment", "support_abuse", "Report abuse or threatening behavior", "🚨")
                .AddOption("Advertising / Recruitment", "support_ad", "Unwanted promotions or invites", "📣")
                .AddOption("Bug / Feature", "support_bug", "Report a bug or request a feature", "🐛")
                .AddOption("Other", "support_other", "Other support inquiries", "❓");

            var comp = new ComponentBuilder().WithSelectMenu(select);
            var sent = await Context.Channel.SendMessageAsync(embed: embed.Build(), components: comp.Build());
            TicketService.TicketMetas.TryAdd(sent.Id, new TicketService.TicketMeta { GuildId = Context.Guild.Id, UserId = Context.User.Id, Category = "embed_origin" });
            await ReplyAsync("✅ Support ticket embed posted in this channel!");
        }

        [Command("munga-ticketsystem")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task ConfigureTicketSystemAsync()
        {
            await ReplyAsync("Send the Log Channel ID where ticket transcripts should be posted, or type **!create** to let the bot create one. (60s)");
            var resp = await NextMessageAsync(m => m.Author.Id == Context.User.Id, TimeSpan.FromSeconds(60));
            if (resp == null) { await ReplyAsync("❌ Time expired."); return; }
            var val = resp.Content.Trim();
            ulong logChannelId = 0;
            if (string.Equals(val, "!create", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var created = await Context.Guild.CreateTextChannelAsync("tickets-log");
                    logChannelId = created.Id;
                }
                catch { await ReplyAsync("❌ Failed to create log channel."); return; }
            }
            else
            {
                var idStr = System.Text.RegularExpressions.Regex.Replace(val, "[^0-9]", "");
                if (!ulong.TryParse(idStr, out logChannelId)) { await ReplyAsync("❌ Invalid channel ID."); return; }
                var ch = Context.Guild.GetTextChannel(logChannelId);
                if (ch == null) { await ReplyAsync("❌ Channel not found."); return; }
            }
            TicketService.SetConfig(Context.Guild.Id, new TicketService.TicketConfigEntry { LogChannelId = logChannelId });
            await ReplyAsync($"✅ Ticket system configured. Log channel: <#{logChannelId}>");
        }

        private async Task<SocketMessage> NextMessageAsync(Func<SocketMessage, bool> filter, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<SocketMessage?>();
            Task Handler(SocketMessage msg)
            {
                try { if (filter(msg)) tcs.TrySetResult(msg); } catch { }
                return Task.CompletedTask;
            }
            void OnMsg(SocketMessage m) => Handler(m);
            Context.Client.MessageReceived += OnMsg;
            var task = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
            Context.Client.MessageReceived -= OnMsg;
            if (task == tcs.Task) return tcs.Task.Result!;
            return null;
        }
    }
}
