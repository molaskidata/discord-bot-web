using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Discord;

namespace MainbotCSharp.Modules
{
    public class VoiceCommands : ModuleBase<SocketCommandContext>
    {
        [Command("voicename")]
        public async Task VoiceNameAsync([Remainder] string name)
        {
            var svc = MainbotCSharp.Services.VoiceService.GetConfig();
            var ownerInfo = MainbotCSharp.Services.VoiceService.GetActiveChannelByOwner(Context.User.Id);
            if (ownerInfo == null) { await ReplyAsync("You don't own an active voice channel."); return; }
            // fallback: try find by owner mapping
            ulong channelId = 0;
            foreach (var kv in svc.ActiveChannels) if (kv.Value.OwnerId == Context.User.Id) { channelId = kv.Key; break; }
            if (channelId == 0) { await ReplyAsync("Cannot find your channel."); return; }
            var channel = Context.Guild.GetVoiceChannel(channelId);
            if (channel == null) { await ReplyAsync("Channel not found."); return; }
            try { await channel.ModifyAsync(p => p.Name = name); await ReplyAsync($"✅ Channel renamed to {name}"); }
            catch { await ReplyAsync("Failed to rename channel."); }
        }

        // (other voice commands left unchanged)
    }
}
