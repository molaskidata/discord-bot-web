using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using Stripe;
using Stripe.Checkout;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddHttpClient();

var app = builder.Build();

// Stripe API Key (aus Environment Variable oder appsettings.json)
StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "sk_test_YOUR_KEY_HERE";
var stripeWebhookSecret = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET") ?? "whsec_YOUR_SECRET_HERE";

// Serve static files from the repo Website directory so existing HTML/CSS remain usable
var websiteRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", ".."));
if (Directory.Exists(websiteRoot))
{
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = new PhysicalFileProvider(websiteRoot)
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(websiteRoot)
    });
}

app.MapGet("/", () => Results.Redirect("/index.html"));


// Activity server endpoint placeholder (replaces Website/activitys/server/server.js)
app.MapPost("/api/activity", async (HttpRequest req, ILogger<Program> log) =>
{
    try
    {
        using var doc = await JsonDocument.ParseAsync(req.Body);
        log.LogInformation("Received activity payload: {Payload}", doc.RootElement.ToString());
        return Results.Ok(new { ok = true });
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Failed to parse activity payload");
        return Results.BadRequest(new { ok = false, error = ex.Message });
    }
});

app.MapGet("/healthz", () => Results.Text("ok"));

// Discord OAuth - Login
app.MapGet("/auth/discord", (HttpContext ctx) =>
{
    var clientId = Environment.GetEnvironmentVariable("DISCORD_CLIENT_ID") ?? "YOUR_CLIENT_ID";
    var redirectUri = "https://thecoffeylounge.com/auth/callback";
    var scope = "identify guilds";
    var state = Guid.NewGuid().ToString();

    var url = $"https://discord.com/api/oauth2/authorize?client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope={scope}&state={state}";
    return Results.Redirect(url);
});

// Discord OAuth - Callback
app.MapGet("/auth/callback", async (HttpRequest req, IHttpClientFactory httpFactory, ILogger<Program> log) =>
{
    var code = req.Query["code"].ToString();
    if (string.IsNullOrEmpty(code))
    {
        return Results.Redirect("/premium?error=no_code");
    }

    var clientId = Environment.GetEnvironmentVariable("DISCORD_CLIENT_ID") ?? "";
    var clientSecret = Environment.GetEnvironmentVariable("DISCORD_CLIENT_SECRET") ?? "";
    var redirectUri = "https://thecoffeylounge.com/auth/callback";

    try
    {
        var httpClient = httpFactory.CreateClient();

        // Exchange code for access token
        var tokenResponse = await httpClient.PostAsync("https://discord.com/api/oauth2/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "grant_type", "authorization_code" },
                { "code", code },
                { "redirect_uri", redirectUri }
            }));

        var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
        var tokenData = JsonDocument.Parse(tokenJson);
        var accessToken = tokenData.RootElement.GetProperty("access_token").GetString();

        // Get user info
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var userResponse = await httpClient.GetAsync("https://discord.com/api/users/@me");
        var userJson = await userResponse.Content.ReadAsStringAsync();
        var userData = JsonDocument.Parse(userJson);

        var userId = userData.RootElement.GetProperty("id").GetString();
        var username = userData.RootElement.GetProperty("username").GetString();

        log.LogInformation("Discord user logged in: {Username} ({UserId})", username, userId);

        // Get user's guilds
        var guildsResponse = await httpClient.GetAsync("https://discord.com/api/users/@me/guilds");
        var guildsJson = await guildsResponse.Content.ReadAsStringAsync();

        // Redirect to dashboard with user data
        return Results.Redirect($"/premium?user={userId}&username={username}");
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Discord OAuth failed");
        return Results.Redirect("/premium?error=auth_failed");
    }
});

// Stripe - Create Checkout Session
app.MapPost("/api/create-checkout-session", async (HttpRequest req, ILogger<Program> log) =>
{
    try
    {
        using var doc = await JsonDocument.ParseAsync(req.Body);
        var planType = doc.RootElement.GetProperty("planType").GetString(); // "monthly" or "yearly"
        var userId = doc.RootElement.GetProperty("userId").GetString();
        var guildId = doc.RootElement.GetProperty("guildId").GetString();

        var priceId = planType == "yearly"
            ? "price_1SmC1uKD4iDXK2wZfi3r2YIe"  // €60/year
            : "price_1SmC1uKD4iDXK2wZBky4cZ6c"; // €5.99/month

        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card", "paypal", "sepa_debit" },
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    Price = priceId,
                    Quantity = 1,
                }
            },
            Mode = "subscription",
            SuccessUrl = $"https://thecoffeylounge.com/premium?success=true&session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = "https://thecoffeylounge.com/premium?canceled=true",
            ClientReferenceId = $"{userId}:{guildId}", // Store user+guild for webhook
            Metadata = new Dictionary<string, string>
            {
                { "discord_user_id", userId },
                { "discord_guild_id", guildId },
                { "plan_type", planType }
            }
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options);

        log.LogInformation("Stripe checkout created for user {UserId}, guild {GuildId}, plan {Plan}", userId, guildId, planType);

        return Results.Ok(new { url = session.Url });
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Failed to create Stripe checkout");
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Stripe Webhook - Automatic Premium Activation
app.MapPost("/api/stripe-webhook", async (HttpRequest req, ILogger<Program> log) =>
{
    var json = await new StreamReader(req.Body).ReadToEndAsync();

    try
    {
        var stripeSignature = req.Headers["Stripe-Signature"].ToString();
        var stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, stripeWebhookSecret);

        log.LogInformation("Stripe webhook received: {EventType}", stripeEvent.Type);

        if (stripeEvent.Type == "checkout.session.completed")
        {
            var session = stripeEvent.Data.Object as Session;
            var metadata = session?.Metadata;

            if (metadata != null && metadata.ContainsKey("discord_guild_id"))
            {
                var guildId = metadata["discord_guild_id"];
                var userId = metadata["discord_user_id"];
                var planType = metadata["plan_type"];

                log.LogInformation("Payment successful! Activating Premium for guild {GuildId}, user {UserId}, plan {Plan}",
                    guildId, userId, planType);

                // Activate Premium in PremiumService
                // TODO: Call bot API or directly update premium_guilds.json
                // For now, write to a file that the bot can read
                var premiumDataPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Bots", "C-Main", "premium_guilds.json");

                // Load existing data
                var existingData = new List<object>();
                if (System.IO.File.Exists(premiumDataPath))
                {
                    var existingJson = await System.IO.File.ReadAllTextAsync(premiumDataPath);
                    existingData = JsonSerializer.Deserialize<List<object>>(existingJson) ?? new List<object>();
                }

                // Add new premium guild
                var newGuild = new
                {
                    GuildId = ulong.Parse(guildId),
                    OwnerId = ulong.Parse(userId),
                    SubscriptionType = planType,
                    ActivatedAt = DateTime.UtcNow,
                    ExpiresAt = planType == "yearly" ? DateTime.UtcNow.AddYears(1) : DateTime.UtcNow.AddMonths(1),
                    IsActive = true,
                    StripeCustomerId = session?.CustomerId ?? "",
                    StripeSubscriptionId = session?.SubscriptionId ?? ""
                };

                existingData.Add(newGuild);

                // Save to file
                var updatedJson = JsonSerializer.Serialize(existingData, new JsonSerializerOptions { WriteIndented = true });
                await System.IO.File.WriteAllTextAsync(premiumDataPath, updatedJson);

                log.LogInformation("Premium activated successfully for guild {GuildId}!", guildId);
            }
        }
        else if (stripeEvent.Type == "customer.subscription.deleted")
        {
            // Handle subscription cancellation
            var subscription = stripeEvent.Data.Object as Subscription;
            log.LogInformation("Subscription canceled: {SubscriptionId}", subscription?.Id);
            // TODO: Deactivate Premium
        }

        return Results.Ok();
    }
    catch (StripeException ex)
    {
        log.LogError(ex, "Stripe webhook error");
        return Results.BadRequest();
    }
});

app.Run();
