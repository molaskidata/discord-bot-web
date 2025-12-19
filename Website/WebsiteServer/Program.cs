using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

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

// Minimal GitHub OAuth callback placeholder (replaces github-oauth-server.js functionality)
app.MapGet("/github/callback", (HttpRequest req, ILogger<Program> log) =>
{
    var code = req.Query["code"].ToString();
    var state = req.Query["state"].ToString();
    log.LogInformation("Received github callback: code={Code}, state={State}", code, state);

    // Placeholder: respond with a small HTML confirmation. Real token exchange should be implemented securely.
    var html = "<html><body><h2>GitHub OAuth callback received</h2><p>Close this window.</p></body></html>";
    return Results.Content(html, "text/html");
});

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

app.Run();
