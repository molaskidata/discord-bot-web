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

Replace placeholders like `your_domain_here`, `your_server_ip`, `your_token_here`, etc. with your own values when deploying. This README is safe for public repositories and does not expose any private data.
