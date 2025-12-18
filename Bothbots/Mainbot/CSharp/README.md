# Mainbot (C# port)

Minimal scaffold of `Mainbot` using Discord.NET and .NET 7.

Prerequisites:
- .NET 7 SDK installed

How to run (PowerShell):

```powershell
cd Bothbots/Mainbot/CSharp
dotnet restore
$env:MAINBOT_TOKEN = "your-bot-token-here"
dotnet run
```

What I implemented:
- Project scaffold (`Mainbot.csproj`)
- `Bot` startup (`Program.cs`, `Bot.cs`)
- Basic command module with `!ping` and `!help` (`Modules/MainCommands.cs`)

Next steps:
- Translate the rest of `Bothbots/Mainbot/commands.js` command-by-command into C# modules.
- Add persistence (JSON files) and security moderation logic.
