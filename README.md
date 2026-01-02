# Discord Bot Website Setup Guide

## 1. Prerequisites

Before you start, you need:
- A Hetzner server (Ubuntu recommended)
- A registered domain (e.g. `your_domain_here`)
- A Discord account
- Your own Discord server

## 2. Deploying the Website

### Server Preparation
- Connect to your Hetzner server via SSH:
  ```sh
  ssh root@your_server_ip
  ```
- Update and install required packages:
  ```sh
  apt update
  apt install nginx certbot python3-certbot-nginx
  ```

### DNS & Domain Setup
- Set A-records for your domain to point to your Hetzner server IP.
- Example DNS record:
  ```
  Type: A
  Name: @
  Value: your_server_ip
  TTL: 600
  ```

### Upload Website Files
- Use SCP or SFTP to upload your website files:
  ```sh
  scp -r /local/path/to/website/* root@your_server_ip:/var/www/your_domain_here/
  ```

### Nginx Configuration
- Create a new Nginx site config:
  ```sh
  nano /etc/nginx/sites-available/your_domain_here
  ```
- Example config:
  ```nginx
  server {
      listen 80;
      server_name your_domain_here www.your_domain_here;
      root /var/www/your_domain_here;
      index index.html;
      location / {
          try_files $uri $uri/ =404;
      }
  }
  ```
- Enable the site and reload Nginx:
  ```sh
  ln -s /etc/nginx/sites-available/your_domain_here /etc/nginx/sites-enabled/
  nginx -t
  systemctl reload nginx
  ```

### HTTPS Setup
- Issue SSL certificate with Certbot:
  ```sh
  certbot --nginx -d your_domain_here -d www.your_domain_here
  ```
- Test your site at `https://your_domain_here`

## 3. Discord Bots Setup


### Node.js dependencies (`node_modules`) for the bots

To make the Discord bots work, you need to install all required Node.js packages. These packages are defined in each bot's `package.json` (e.g., in `Bothbots/Mainbot` and `Bothbots/Pingbot`). Install them with:

```powershell
npm install
```

**Important notes:**
- Run the command in the respective bot folder, e.g.:
  ```powershell
  cd Bothbots\Mainbot
  npm install
  ```
- This creates the `node_modules` folder, where all dependencies are stored.
- The `package.json` file lists all required packages like `discord.js`, `dotenv`, `axios`, etc.
- Without `node_modules`, the bot cannot start because the packages are missing.

**Starting the bot:**
- After installation, you can start the bot with:
  ```powershell
  node infobot.js
  ```
  or, if a start script is defined:
  ```powershell
  npm start
  ```

**Tip:**  
If you have multiple bots, repeat the installation in every bot folder that has its own `package.json`.

**Troubleshooting:**  
If you get an error like `Cannot find module 'discord.js'` when starting the bot, you probably forgot to run `npm install` or you are in the wrong folder.

### Discord Developer Portal
- Create a new application and bot at [Discord Developer Portal](https://discord.com/developers/applications)
- Copy your bot token: `your_token_here`
- Add your bot to your server using OAuth2 URL Generator.

### Bot Code Example (Ping-Pong)
- Example command handler in Node.js:
  ```js
  client.on('messageCreate', (message) => {
      if (message.content === '!pingme') {
          message.channel.send('!ponggg');
      }
  });
  ```
- Set your bot token in `.env`:
  ```
  DISCORD_TOKEN=your_token_here
  ```
### Running Bots on Hetzner Server
- Upload your bot files to the server.
- Install Node.js and dependencies:
  ```sh
  apt install nodejs npm
  npm install
  ```
- Use pm2 to keep bots running:
  ```sh
  npm install -g pm2
  pm2 start your_bot.js --name mainbot
  pm2 save
  pm2 startup
  ```

### Bot Code Example (Ping-Pong)
- Example command handler in Node.js:
  ```js
  client.on('messageCreate', (message) => {
      if (message.content === '!pingme') {
          message.channel.send('!ponggg');
      }
  });
  ```
- Set your bot token in `.env`:
  ```
  DISCORD_TOKEN=your_token_here
  ```

### Running Bots on Hetzner Server
- Upload your bot files to the server.
- Install Node.js and dependencies:
  ```sh
  apt install nodejs npm
  npm install
  ```
- Use pm2 to keep bots running:
  ```sh
  npm install -g pm2
  pm2 start your_bot.js --name mainbot
  pm2 save
  pm2 startup
  ```

### Custom Commands & Prefixes
- Example for custom prefix:
  ```js
  const PREFIX = '!';
  client.on('messageCreate', (message) => {
      if (!message.content.startsWith(PREFIX)) return;
      // handle commands
  });
  ```

### Privacy Policy & Terms of Service
- Create `privacy-policy.html` and `terms-of-service.html` in your website directory.
- Link them in your website footer.

### Bot-to-Bot Communication
- To let one bot observe another:
  ```js
  client.on('messageCreate', (message) => {
      if (message.author.id === other_bot_id) {
          // Forward message to website or process it
      }
  });
  ```

---



## 4. Updating Bots and Website (Hetzner Server)

### How to update and restart bots after code changes

**If you changed bot code (e.g. Mainbot or Pingbot):**

1. **Commit and push your changes locally:**
  ```bash
  git add Bothbots/Mainbot/* Bothbots/Pingbot/*
  git commit -m "Update bot code"
  git push
  ```

2. **On your Hetzner server (via SSH):**
  ```bash
  cd /root/discord-bot-web
  git pull
  ```

3. **Restart the bot (if using pm2):**
  ```bash
  pm2 restart mainbot
  pm2 restart pingbot
  ```
  Or, if you run bots directly:
  ```bash
  pkill -f infobot.js
  nohup node Bothbots/Mainbot/infobot.js > mainbot.log 2>&1 &

  pkill -f pingbot.js
  nohup node Bothbots/Pingbot/pingbot.js > pingbot.log 2>&1 &
  ```

---

### How to update the website after changes

**If you changed website files (e.g. `Website/index.html`, `Website/styles.css`):**

1. **Commit and push your changes locally:**
  ```bash
  git add Website/index.html Website/styles.css
  git commit -m "Update website files"
  git push
  ```

2. **On your Hetzner server (via SSH):**
  ```bash
  cd /root/discord-bot-web
  git pull
  ```

3. **No need to restart nginx or the webserver unless you changed the config.**
  - If you changed `nginx-config.conf`, restart nginx:
    ```bash
    systemctl restart nginx
    ```

---

### Summary of update workflow

- Edit code or website files locally
- Commit and push changes to your git repository
- SSH into your Hetzner server and run `git pull`
- Restart bots if needed (with pm2 or node)
- For website: changes are live after `git pull` (unless config changed)

---

**Tip:**
You can use `tail -f mainbot.log` or `tail -f pingbot.log` to monitor bot output after restart.
