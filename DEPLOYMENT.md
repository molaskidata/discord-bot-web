# Discord Bot Web - Deployment Guide

Complete build and deployment scripts for your Discord Bot website and services.

## 📋 Prerequisites

- Ubuntu 20.04 LTS or newer
- Root or sudo access
- Domain name (optional, but recommended)
- Discord Bot Tokens (from Discord Developer Portal)
- Git repository access

## 🚀 Quick Start

### 1. Initial Setup on VPS

```bash
# Clone the repository
git clone https://github.com/molaskidata/discord-bot-website.git
cd discord-bot-website

# Make scripts executable
chmod +x deploy.sh update.sh setup-config.sh

# Configure your deployment
./setup-config.sh

# Run the full deployment
sudo bash deploy.sh production
```

### 2. First Time Configuration

The `setup-config.sh` script will prompt you for:
- **Domain**: Your website domain (e.g., `discord-bot.com`)
- **MainBot Token**: Discord token for the main bot
- **PingBot Token**: Discord token for the ping bot
- **Environment**: `production` or `development`
- **SSL Email**: Email for Let's Encrypt certificate

## 📁 Deployment Scripts

### `deploy.sh` - Full Deployment
Complete build and deployment of all services.

**Usage:**
```bash
sudo bash deploy.sh [production|development]
```

**What it does:**
- ✅ Installs system dependencies (Node.js, npm, Nginx, Certbot)
- ✅ Creates app user and directories
- ✅ Installs PM2 for process management
- ✅ Builds Vite frontend
- ✅ Installs dependencies for all bots and services
- ✅ Configures Nginx with SSL
- ✅ Sets up PM2 services with auto-restart
- ✅ Configures firewall (UFW)
- ✅ Runs health checks

**Output:**
```
Website: https://your-domain.com
Activity Server: http://localhost:3002
Bots: Running via PM2
```

### `update.sh` - Quick Updates
Fast redeployment after code changes.

**Usage:**
```bash
./update.sh [mainbot|pingbot|activity|website|all]
```

**Examples:**
```bash
# Update just the main bot
./update.sh mainbot

# Update all services
./update.sh all

# Update website files only
./update.sh website
```

### `setup-config.sh` - Configuration Wizard
Interactive configuration setup.

**Usage:**
```bash
./setup-config.sh
```

**Creates:**
- `.deploy-config` - Deployment configuration
- `.env` files for each service with your tokens

## 📊 Project Structure

```
discord-bot-website/
├── Website/                    # Static website files
├── activitys/
│   ├── client/                # Vite React app
│   │   ├── main.js
│   │   ├── vite.config.js
│   │   └── package.json
│   └── server/                # Activity server (Express)
│       ├── server.js
│       └── package.json
├── Bothbots/
│   ├── Mainbot/              # Main Discord bot
│   │   ├── infobot.js
│   │   ├── utils.js
│   │   └── package.json
│   └── Pingbot/              # Ping Discord bot
│       ├── pingbot.js
│       └── package.json
├── deploy.sh                  # Full deployment script
├── update.sh                  # Quick update script
├── setup-config.sh           # Configuration wizard
└── README.md                 # This file
```

## 🔧 Manual Configuration

If you need to configure manually:

### Environment Variables

Create `.env` files in each directory:

**activitys/.env:**
```
NODE_ENV=production
ACTIVITY_SERVER_PORT=3002
```

**Bothbots/Mainbot/.env:**
```
NODE_ENV=production
DISCORD_TOKEN=your_discord_token_here
```

**Bothbots/Pingbot/.env:**
```
NODE_ENV=production
DISCORD_TOKEN=your_discord_token_here
```

### Nginx Configuration

The deployment script auto-configures Nginx. To manually configure:

```bash
# Edit or create Nginx config
sudo nano /etc/nginx/sites-available/your-domain.com

# Enable the site
sudo ln -s /etc/nginx/sites-available/your-domain.com /etc/nginx/sites-enabled/

# Test and reload
sudo nginx -t
sudo systemctl reload nginx
```

## 📝 Managing Services

### View Service Status
```bash
pm2 status
```

### View Logs
```bash
# Main Bot logs
pm2 logs mainbot

# Ping Bot logs
pm2 logs pingbot

# Activity Server logs
pm2 logs activity-server

# All logs
pm2 logs
```

### Start/Stop/Restart
```bash
# Restart all services
pm2 restart all

# Stop all services
pm2 stop all

# Start all services
pm2 start all

# Restart specific service
pm2 restart mainbot
```

### Monitor Services
```bash
# Real-time monitoring
pm2 monit

# Show detailed info
pm2 info mainbot
```

## 🔄 Update Workflow

After making code changes:

```bash
# Commit and push to git
git add .
git commit -m "Update bot code"
git push

# On the VPS
cd discord-bot-website
git pull
./update.sh all
```

Or for specific services:
```bash
git pull
./update.sh mainbot        # Just update main bot
./update.sh activity       # Just update activity server
./update.sh website        # Just update website files
```

## 🧪 Testing Deployment

### Check if services are running
```bash
# Check all processes
pm2 status

# Check ports
sudo ss -tlnp | grep -E ':80|:443|:3002'

# Test website
curl https://your-domain.com

# Test activity server
curl http://localhost:3002
```

### Monitor system resources
```bash
# Real-time monitoring
pm2 monit

# Check disk space
df -h

# Check memory usage
free -h
```

## 🔐 Security Best Practices

1. **Keep tokens secure:**
   - Never commit `.env` files to git
   - Use git credentials for cloning
   - Rotate tokens regularly

2. **Firewall:**
   - The deployment script sets up UFW automatically
   - Only ports 22 (SSH), 80 (HTTP), 443 (HTTPS) are open

3. **SSL/TLS:**
   - Automatic SSL via Let's Encrypt
   - Auto-renewal with Certbot

4. **Process Monitoring:**
   - Services auto-restart on failure
   - PM2 watches for crashes and restarts

## 🐛 Troubleshooting

### Services not starting
```bash
# Check PM2 status
pm2 status

# View detailed logs
pm2 logs --lines 50

# Restart services
pm2 restart all
```

### Port conflicts
```bash
# Check what's using port 3002
sudo lsof -i :3002

# Kill process if needed
sudo kill -9 <PID>
```

### Website not loading
```bash
# Check Nginx
sudo systemctl status nginx
sudo nginx -t

# Check logs
sudo tail -f /var/log/nginx/discord-bot_error.log
```

### Discord bot not responding
```bash
# Check bot logs
pm2 logs mainbot

# Verify token is correct in .env
cat Bothbots/Mainbot/.env

# Ensure bot has permissions in Discord server
```

### SSL certificate issues
```bash
# Check certificate status
sudo certbot status

# Renew certificate
sudo certbot renew

# Manual renewal (if needed)
sudo certbot certonly --nginx -d your-domain.com
```

## 📞 Support

For issues:
1. Check logs with `pm2 logs`
2. Review this guide's troubleshooting section
3. Check Discord Developer Portal for bot configuration
4. Verify firewall settings: `sudo ufw status`

## 🔄 Backup & Recovery

### Backup configurations
```bash
# Backup .env files (store securely!)
tar -czf backup-env.tar.gz .env Bothbots/*/.env activitys/.env

# Backup website files
tar -czf backup-website.tar.gz Website/

# Backup PM2 configuration
cp ~/.pm2/dump.pm2 ~/pm2-backup-$(date +%Y%m%d).pm2
```

### Restore from backup
```bash
# Restore .env files
tar -xzf backup-env.tar.gz

# Restore website
tar -xzf backup-website.tar.gz

# Restore PM2
pm2 resurrect  # or provide saved dump
```

## 📜 License

See LICENSE file in the repository.

## 👨‍💼 Contributing

For issues or suggestions, please open a GitHub issue or contact the maintainers.

---

**Last Updated:** December 1, 2025
**Maintained by:** molaskidata
