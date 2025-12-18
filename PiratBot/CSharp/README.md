# PiratBot (C# port)

Scaffold of `PiratBot` using Discord.NET and .NET 7.

Prerequisites:
- .NET 7 SDK

How to run (PowerShell):

```powershell
cd Bothbots/PiratBot/CSharp
dotnet restore
$env:PIRATBOT_TOKEN = "your-bot-token-here"
dotnet run
```

Implemented:
- Project scaffold (`PiratBot.csproj`)
- `Bot` startup (`Program.cs`, `Bot.cs`)
- Command module with `!bs`, `!mine`, `!gold`, `!raid` (`Modules/PirateCommands.cs`)

Notes:
- This is an in-memory implementation (no persistence). For persistence we can add JSON file save/load.
- Interaction-based ticket menus are not ported here yet.

Next steps:
- Translate remaining command logic (security moderation, verify, tickets) from JS to C# modules.
- Add persistence and configuration files.
