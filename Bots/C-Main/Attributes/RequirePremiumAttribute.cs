using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using MainbotCSharp.Services;

namespace MainbotCSharp.Attributes
{
    /// <summary>
    /// Attribute to check if a server has premium before executing a command
    /// </summary>
    public class RequirePremiumAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (context.Guild == null)
                return Task.FromResult(PreconditionResult.FromError("This command can only be used in a server."));

            var hasPremium = PremiumService.HasPremium(context.Guild.Id);

            if (!hasPremium)
            {
                var embed = new EmbedBuilder()
                    .WithTitle("🔒 Premium Feature")
                    .WithDescription(
                        $"**{command.Name}** is a Premium-only feature!\n\n" +
                        "Upgrade to **Code.Master() Premium** to unlock this and many more features.")
                    .WithColor(0xFF6B6B) // Red
                    .AddField("💎 Get Premium",
                        "Use `!premium` to see all benefits and upgrade!\n" +
                        "**Pricing:** €5.99/month or €60/year")
                    .WithThumbnailUrl("https://i.imgur.com/7mkVUuO.png")
                    .WithFooter("Premium unlocks Voice, Security & Ticket features")
                    .WithCurrentTimestamp();

                // Send the embed in the channel
                context.Channel.SendMessageAsync(embed: embed.Build());

                return Task.FromResult(PreconditionResult.FromError("This server does not have Premium."));
            }

            return Task.FromResult(PreconditionResult.FromSuccess());
        }
    }
}
