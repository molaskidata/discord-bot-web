using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using MainbotCSharp.Services;

namespace MainbotCSharp.Modules
{
    public class VerifyCommands : ModuleBase<SocketCommandContext>
    {
        [Command("setverify")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetVerifyAsync()
        {
            var guild = Context.Guild;
            await ReplyAsync("Please provide the CHANNEL ID where users must verify (or type `cancel`).");
            var response = await NextMessageAsync(m => m.Author.Id == Context.User.Id, TimeSpan.FromSeconds(60));
            if (response == null) { await ReplyAsync("Timeout waiting for channel ID."); return; }
            if (response.Content.ToLowerInvariant() == "cancel") { await ReplyAsync("Cancelled."); return; }
            var chanIdStr = System.Text.RegularExpressions.Regex.Replace(response.Content, "[^0-9]", "");
            if (!ulong.TryParse(chanIdStr, out var chanId)) { await ReplyAsync("Invalid channel ID. Aborting."); return; }
            var chan = guild.GetTextChannel(chanId);
            if (chan == null) { await ReplyAsync("❌ Channel not found or inaccessible. Aborting."); return; }

            await ReplyAsync("Now provide the ROLE ID users should receive on verification (or `cancel`).");
            var rres = await NextMessageAsync(m => m.Author.Id == Context.User.Id, TimeSpan.FromSeconds(60));
            if (rres == null) { await ReplyAsync("Timeout waiting for role ID."); return; }
            if (rres.Content.ToLowerInvariant() == "cancel") { await ReplyAsync("Cancelled."); return; }
            var roleIdStr = System.Text.RegularExpressions.Regex.Replace(rres.Content, "[^0-9]", "");
            if (!ulong.TryParse(roleIdStr, out var roleId)) { await ReplyAsync("❌ Role not found. Aborting."); return; }
            var role = guild.GetRole(roleId);
            if (role == null) { await ReplyAsync("❌ Role not found. Aborting."); return; }

            // Check bot permissions and role hierarchy
            var botMember = guild.CurrentUser;
            var missing = new List<string>();
            if (!botMember.GuildPermissions.ManageChannels) missing.Add("ManageChannels");
            if (!botMember.GuildPermissions.ManageRoles) missing.Add("ManageRoles");
            if (!botMember.GuildPermissions.SendMessages) missing.Add("SendMessages");
            if (role.Position >= botMember.Hierarchy)
            {
                await ReplyAsync("❌ The verification role is equal or higher than my highest role. I cannot assign it. Aborting."); return;
            }

            // take snapshot of @everyone ViewChannel
            var snapshot = new Dictionary<ulong, bool?>();
            var errors = new List<string>();
            foreach (var c in guild.Channels)
            {
                try
                {
                    if (!(c is SocketGuildChannel sgc)) continue;
                    // Only attempt edits if bot can manage channels
                    if (!sgc.Guild.CurrentUser.GuildPermissions.ManageChannels) continue;
                    var ow = sgc.GetPermissionOverwrite(guild.EveryoneRole);
                    bool? prev = null;
                    if (ow.HasValue)
                    {
                        var p = ow.Value;
                        // ViewChannel is a PermValue on OverwritePermissions
                        if (p.ViewChannel == PermValue.Allow) prev = true;
                        else if (p.ViewChannel == PermValue.Deny) prev = false;
                        else prev = null;
                    }
                    snapshot[sgc.Id] = prev;
                    // set overwrites: verify channel visible, others hidden
                    if (sgc.Id == chan.Id)
                    {
                        await sgc.AddPermissionOverwriteAsync(guild.EveryoneRole, new OverwritePermissions(viewChannel: PermValue.Allow));
                    }
                    else
                    {
                        await sgc.AddPermissionOverwriteAsync(guild.EveryoneRole, new OverwritePermissions(viewChannel: PermValue.Deny));
                    }
                }
                catch (Exception)
                {
                    errors.Add(c.Id.ToString());
                }
            }

            var conf = new VerifyConfigEntry { ChannelId = chan.Id, RoleId = role.Id, Snapshot = snapshot };
            // post message in verify channel
            try
            {
                var eb = new EmbedBuilder()
                    .WithTitle("Verify to access the server")
                    .WithDescription("To verify and get access to the server, type `!verify` in this channel. The staff will be notified if there are problems.")
                    .WithColor(Color.Blue)
                    .WithFooter("Verification — stay safe");
                var sent = await chan.SendMessageAsync(embed: eb.Build());
                conf.MessageId = sent.Id;
            }
            catch
            {
                // ignore
            }

            VerifyService.SetConfig(guild.Id, conf);
            var missingMsg = missing.Count > 0 ? $"\nMissing bot permissions: {string.Join(',', missing)}. Some updates were skipped." : string.Empty;
            await ReplyAsync($"✅ Verify configured in {chan}.{(errors.Count > 0 ? " Some channels could not be updated due to permissions." : "")}{missingMsg}");
        }

        [Command("verify")]
        public async Task VerifyAsync()
        {
            var cfg = VerifyService.GetConfig(Context.Guild.Id);
            if (cfg == null || cfg.ChannelId == 0) { await ReplyAsync("Verification is not configured on this server."); return; }
            if (Context.Channel.Id != cfg.ChannelId) { await ReplyAsync("Please verify in the verification channel."); return; }
            var member = Context.User as SocketGuildUser;
            var role = Context.Guild.GetRole(cfg.RoleId);
            if (role == null) { await ReplyAsync("Verification role no longer exists. Contact an admin."); return; }
            try
            {
                await member.AddRoleAsync(role);
                await ReplyAsync("✅ You have been verified and the role was assigned. Welcome!");
            }
            catch
            {
                await ReplyAsync("❌ Failed to assign role. I may lack Manage Roles permission or the role is higher than my role.");
            }
        }

        [Command("delverifysett")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task DelVerifySettAsync()
        {
            var cfg = VerifyService.GetConfig(Context.Guild.Id);
            if (cfg == null) { await ReplyAsync("No verify setup found."); return; }
            await ReplyAsync("Are you sure? Type `Y` to confirm deletion, `N` to cancel (60s).");
            var res = await NextMessageAsync(m => m.Author.Id == Context.User.Id, TimeSpan.FromSeconds(60));
            if (res == null) { await ReplyAsync("Timeout. No changes made."); return; }
            var v = res.Content.Trim().ToLowerInvariant();
            if (v == "y" || v == "yes")
            {
                // delete verify message if present
                try
                {
                    var ch = Context.Guild.GetTextChannel(cfg.ChannelId);
                    if (ch != null && cfg.MessageId.HasValue)
                    {
                        var msg = await ch.GetMessageAsync(cfg.MessageId.Value) as IUserMessage;
                        if (msg != null) await msg.DeleteAsync();
                    }
                }
                catch { }

                // rollback snapshot
                var snapshot = cfg.Snapshot ?? new Dictionary<ulong, bool?>();
                foreach (var kv in snapshot)
                {
                    try
                    {
                        var ch = Context.Guild.GetChannel(kv.Key) as SocketGuildChannel;
                        if (ch == null) continue;
                        var prev = kv.Value;
                        if (prev == null)
                        {
                            try { await ch.RemovePermissionOverwriteAsync(Context.Guild.EveryoneRole); } catch { }
                        }
                        else if (prev == true)
                        {
                            try { await ch.AddPermissionOverwriteAsync(Context.Guild.EveryoneRole, new OverwritePermissions(viewChannel: PermValue.Allow)); } catch { }
                        }
                        else // false
                        {
                            try { await ch.AddPermissionOverwriteAsync(Context.Guild.EveryoneRole, new OverwritePermissions(viewChannel: PermValue.Deny)); } catch { }
                        }
                    }
                    catch { }
                }

                VerifyService.RemoveConfig(Context.Guild.Id);
                await ReplyAsync("✅ Verify setup removed and previous channel view permissions attempted to be restored.");
            }
            else
            {
                await ReplyAsync("Aborted. No changes made.");
            }
        }

        // Helper to wait for the next message from the invoking user
        private async Task<SocketMessage> NextMessageAsync(Func<SocketMessage, bool> filter, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<SocketMessage?>();
            Func<SocketMessage, Task> handler = (SocketMessage msg) =>
            {
                try { if (filter(msg)) tcs.TrySetResult(msg); } catch { }
                return Task.CompletedTask;
            };
            Context.Client.MessageReceived += handler;
            var task = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
            Context.Client.MessageReceived -= handler;
            if (task == tcs.Task) return tcs.Task.Result!;
            return null;
        }
    }
}
