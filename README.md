# Coffee & Codes Discord Bot Project

## Overview

This project contains a Discord bot and a simple website for the Coffee & Codes community. The main focus is the Discord bot, which provides interactive commands, community engagement, and a status bar feature. The website serves as an informational landing page and includes links to the Discord server, privacy policy, and terms of service.

## Features

- Discord bot with multiple custom commands
- Status bar showing bot activity and uptime
- Website with community information and Discord invite
- Privacy policy and terms of service included
- Example deployment scripts and Nginx configuration

## Use Cases

- Use the Discord bot to enhance your community server with fun and useful commands
- Display bot status and uptime for transparency
- Provide users with easy access to legal information and community guidelines
- Deploy the website as a landing page for your Discord community

## Getting Started

### 1. Clone the Repository

```
git clone https://github.com/molaskidata/discord-bot-webproject.git
cd discord-bot-webproject/Discord Bot
```

### 2. Install Dependencies

```
npm install
```

### 3. Configure Environment Variables

Create a `.env` file in the `Discord Bot` directory and add your Discord bot token:

```
DISCORD_TOKEN=your-bot-token-here
```

### 4. Start the Bot

```
npm start
```

The bot will connect to Discord and become active in your server. The status bar will show the bot's activity and uptime.

### 5. Deploy the Website (Optional)

- Use the provided PowerShell script (`deploy-website.ps1`) to upload the website files to your server
- Configure your web server using the sample `nginx-config.conf`

### 6. Access Legal Documents

- Privacy policy and terms of service are located in the `terms&privacy` folder
- Update the content as needed for your community

## Project Structure

- `Discord Bot/` - Main bot code and configuration
- `Website/` - Static website files
- `terms&privacy/` - Legal documents
- `deploy-website.ps1` - Deployment script for the website
- `nginx-config.conf` - Example Nginx configuration

## Notes

- The main focus is the Discord bot and its features
- The status bar provides real-time information about the bot's activity
- The project is designed for easy customization and deployment

For questions or support, please use the Discord link provided on the website.
