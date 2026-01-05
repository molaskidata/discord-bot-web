using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using MainbotCSharp.Services;

namespace MainbotCSharp.Modules
{
    public class PremiumCommands : ModuleBase<SocketCommandContext>
    {
        [Command("premium")]
        [Summary("Get information about Premium features")]
        public async Task PremiumAsync()
        {
            var guildId = Context.Guild.Id;
            var hasPremium = PremiumService.HasPremium(guildId);

            if (hasPremium)
            {
                // User already has premium
                var premiumInfo = PremiumService.GetPremiumGuild(guildId);
                var embed = new EmbedBuilder()
                    .WithTitle("✨ Premium Active!")
                    .WithDescription($"This server has **Code.Master() Premium**!")
                    .WithColor(0xFFD700) // Gold
                    .AddField("💎 Subscription Type",
                        premiumInfo!.SubscriptionType == "yearly" ? "Yearly (€60/year)" : "Monthly (€5.99/month)", true)
                    .AddField("📅 Activated",
                        premiumInfo.ActivatedAt.ToString("dd.MM.yyyy"), true)
                    .AddField("⏰ Expires",
                        premiumInfo.ExpiresAt.ToString("dd.MM.yyyy"), true)
                    .AddField("🎯 Premium Features",
                        "✅ Voice System - Full control\n" +
                        "✅ Security System - AI-powered moderation\n" +
                        "✅ Ticket System - Professional support\n" +
                        "✅ Priority Support\n" +
                        "✅ Early Access to new features", false)
                    .WithThumbnailUrl("https://i.imgur.com/7mkVUuO.png")
                    .WithFooter("Thank you for supporting Code.Master()! ❤️")
                    .WithCurrentTimestamp();

                await ReplyAsync(embed: embed.Build());
            }
            else
            {
                // User doesn't have premium - show upgrade message
                var embed = new EmbedBuilder()
                    .WithTitle("🔒 Unlock Premium Features")
                    .WithDescription(
                        "**Upgrade to Code.Master() Premium and unlock powerful features!**\n\n" +
                        "Get access to professional tools that take your Discord server to the next level.")
                    .WithColor(0x7289DA)
                    .AddField("💎 Premium Features",
                        "🎤 **Voice System** - Join-to-Create, templates, stats\n" +
                        "🛡️ **Security System** - AI-powered spam & NSFW detection\n" +
                        "🎫 **Ticket System** - Professional support channels\n" +
                        "⚡ **Priority Support** - Get help faster\n" +
                        "🚀 **Early Access** - Be first to try new features", false)
                    .AddField("💰 Pricing",
                        "**Monthly:** €5.99/month\n" +
                        "**Yearly:** €60/year (€5/month - Save 17%!)", false)
                    .AddField("🌐 Get Premium Now",
                        "Visit our website to upgrade:\n" +
                        "**[https://your-domain.com/premium](https://your-domain.com/premium)**\n\n" +
                        "💳 Secure payment via Stripe • Cancel anytime", false)
                    .WithImageUrl("https://imgur.com/aYh8OAq.png")
                    .WithFooter("Made by mungabee", "https://i.imgur.com/7mkVUuO.png")
                    .WithCurrentTimestamp();

                // Add button to website
                var component = new ComponentBuilder()
                    .WithButton("Upgrade to Premium", style: ButtonStyle.Link,
                        url: "https://your-domain.com/premium",
                        emote: new Emoji("💎"))
                    .Build();

                await ReplyAsync(embed: embed.Build(), components: component);
            }
        }

        [Command("premiumstatus")]
        [Alias("premstat")]
        [Summary("Check premium status of this server")]
        public async Task PremiumStatusAsync()
        {
            var guildId = Context.Guild.Id;
            var hasPremium = PremiumService.HasPremium(guildId);

            if (hasPremium)
            {
                var premiumInfo = PremiumService.GetPremiumGuild(guildId);
                var daysRemaining = (premiumInfo!.ExpiresAt - DateTime.UtcNow).Days;

                var embed = new EmbedBuilder()
                    .WithTitle("Premium Status")
                    .WithColor(0x00FF00) // Green
                    .AddField("Status", "✅ Active", true)
                    .AddField("Type", premiumInfo.SubscriptionType == "yearly" ? "Yearly" : "Monthly", true)
                    .AddField("Days Remaining", $"{daysRemaining} days", true)
                    .AddField("Expires", premiumInfo.ExpiresAt.ToString("dd.MM.yyyy HH:mm"), true)
                    .WithFooter("Premium features are enabled")
                    .WithCurrentTimestamp();

                await ReplyAsync(embed: embed.Build());
            }
            else
            {
                var embed = new EmbedBuilder()
                    .WithTitle("Premium Status")
                    .WithDescription("This server does not have Premium.")
                    .WithColor(0xFF0000) // Red
                    .AddField("Upgrade", "Use `!premium` to learn more about Premium features!")
                    .WithCurrentTimestamp();

                await ReplyAsync(embed: embed.Build());
            }
        }

        [Command("activatepremium")]
        [Summary("Activate premium for this server (Admin only - for testing)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task ActivatePremiumAsync(string type = "monthly")
        {
            if (type != "monthly" && type != "yearly")
            {
                await ReplyAsync("❌ Invalid type! Use `monthly` or `yearly`");
                return;
            }

            var guildId = Context.Guild.Id;
            var ownerId = Context.User.Id;

            PremiumService.ActivatePremium(guildId, ownerId, type);

            var duration = type == "yearly" ? "1 year" : "30 days";
            await ReplyAsync($"✅ Premium activated for {duration}! Use `!premiumstatus` to check.");
        }

        [Command("deactivatepremium")]
        [Summary("Deactivate premium for this server (Admin only)")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task DeactivatePremiumAsync()
        {
            var guildId = Context.Guild.Id;
            PremiumService.DeactivatePremium(guildId);
            await ReplyAsync("❌ Premium deactivated for this server.");
        }
    }
}
