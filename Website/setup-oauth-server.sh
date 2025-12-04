#!/bin/bash
# Setup script for GitHub OAuth Server on Hetzner

echo "🔧 Setting up GitHub OAuth Server..."

# 1. Install Let's Encrypt SSL (if not already installed)
echo "📦 Installing Certbot for SSL..."
sudo apt-get update
sudo apt-get install -y certbot python3-certbot-nginx

# 2. Get SSL certificate (if not already obtained)
echo "🔐 Getting SSL certificate..."
sudo certbot certonly --nginx -d thecoffeylounge.com -d www.thecoffeylounge.com

# 3. Copy nginx config
echo "⚙️ Setting up nginx configuration..."
sudo cp nginx-github-oauth.conf /etc/nginx/sites-available/thecoffeylounge.com
sudo ln -sf /etc/nginx/sites-available/thecoffeylounge.com /etc/nginx/sites-enabled/thecoffeylounge.com

# 4. Remove default nginx config if exists
sudo rm -f /etc/nginx/sites-enabled/default

# 5. Test nginx configuration
echo "✅ Testing nginx configuration..."
sudo nginx -t

# 6. Restart nginx
echo "🚀 Restarting nginx..."
sudo systemctl restart nginx

# 7. Install Node.js if not already installed
echo "📦 Installing Node.js..."
curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -
sudo apt-get install -y nodejs

# 8. Create directory for OAuth server
echo "📁 Creating OAuth server directory..."
sudo mkdir -p /opt/github-oauth-server
sudo chown $(whoami):$(whoami) /opt/github-oauth-server

# 9. Copy OAuth server files
echo "📋 Copying OAuth server files..."
cp github-oauth-server.js /opt/github-oauth-server/
cp .env /opt/github-oauth-server/
cp package.json /opt/github-oauth-server/ 2>/dev/null || echo "Note: Create package.json if needed"

# 10. Install dependencies in OAuth server directory
echo "📦 Installing OAuth server dependencies..."
cd /opt/github-oauth-server
npm install express dotenv node-fetch cors

# 11. Create systemd service
echo "⚙️ Creating systemd service..."
sudo tee /etc/systemd/system/github-oauth.service > /dev/null <<EOF
[Unit]
Description=GitHub-Discord OAuth Server
After=network.target

[Service]
Type=simple
User=$(whoami)
WorkingDirectory=/opt/github-oauth-server
ExecStart=/usr/bin/node github-oauth-server.js
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
EOF

# 12. Enable and start the service
echo "🚀 Starting GitHub OAuth service..."
sudo systemctl daemon-reload
sudo systemctl enable github-oauth.service
sudo systemctl start github-oauth.service
sudo systemctl status github-oauth.service

echo "✅ Setup complete!"
echo "📍 Your GitHub OAuth server is now running at: https://thecoffeylounge.com/github/auth"
echo "🔗 Callback URL: https://thecoffeylounge.com/api/github/callback"
