#!/bin/bash

################################################################################
# Discord Bot Website - Complete Build & Deploy Script
# This script builds and deploys the entire project on Ubuntu VPS
# Usage: ./deploy.sh [production|development]
################################################################################

set -e  # Exit on error

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
ENVIRONMENT="${1:-production}"
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOG_DIR="/var/log/discord-bot"
APP_USER="discordbot"
DOMAIN="${DOMAIN:-localhost}"
NODE_VERSION="18"

# Configuration variables (customize these)
REPO_URL="${REPO_URL:-}"
DISCORD_TOKEN="${DISCORD_TOKEN:-}"
ACTIVITY_SERVER_PORT="3002"
VITE_PORT="5174"
WWW_PATH="/var/www/discord-bot"

################################################################################
# Utility Functions
################################################################################

log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

check_root() {
    if [[ $EUID -ne 0 ]]; then
        log_error "This script must be run as root"
        exit 1
    fi
}

check_command() {
    if ! command -v $1 &> /dev/null; then
        log_error "$1 is not installed"
        return 1
    fi
    return 0
}

################################################################################
# Pre-flight Checks
################################################################################

run_preflight_checks() {
    log_info "Running pre-flight checks..."

    if ! check_command node; then
        log_error "Node.js is not installed"
        exit 1
    fi

    if ! check_command npm; then
        log_error "npm is not installed"
        exit 1
    fi

    log_success "Pre-flight checks passed"
}

################################################################################
# Installation Functions
################################################################################

install_system_dependencies() {
    log_info "Installing system dependencies..."

    apt-get update -qq
    apt-get install -y \
        nodejs \
        npm \
        nginx \
        certbot \
        python3-certbot-nginx \
        git \
        curl \
        wget \
        htop \
        supervisor \
        ufw \
        2>&1 | grep -v "^Get:\|^Hit:\|^Reading" || true

    log_success "System dependencies installed"
}

create_app_user() {
    if ! id "$APP_USER" &>/dev/null; then
        log_info "Creating app user: $APP_USER"
        useradd -m -s /bin/bash "$APP_USER"
        log_success "App user created"
    else
        log_info "App user $APP_USER already exists"
    fi
}

install_pm2() {
    log_info "Installing PM2 globally..."
    npm install -g pm2 --quiet
    pm2 update
    log_success "PM2 installed"
}

################################################################################
# Build Functions
################################################################################

build_vite_frontend() {
    log_info "Building Vite frontend (activitys/client)..."

    cd "$PROJECT_ROOT/activitys/client"

    if [ ! -d "node_modules" ]; then
        npm install --quiet
    fi

    npm run build 2>&1 | tail -5

    if [ ! -d "dist" ]; then
        log_error "Vite build failed - no dist folder created"
        exit 1
    fi

    log_success "Vite frontend built successfully"
}

install_activity_server() {
    log_info "Installing Activity Server (activitys/server)..."

    cd "$PROJECT_ROOT/activitys/server"

    if [ ! -d "node_modules" ]; then
        npm install 
    else
        npm update 
    fi

    log_success "Activity Server dependencies installed"
}

install_mainbot() {
    log_info "Installing Main Bot (Bothbots/Mainbot)..."

    cd "$PROJECT_ROOT/Bothbots/Mainbot"

    if [ ! -d "node_modules" ]; then
        npm install 
    else
        npm update 
    fi

    log_success "Main Bot dependencies installed"
}

install_pingbot() {
    log_info "Installing Ping Bot (Bothbots/Pingbot)..."

    cd "$PROJECT_ROOT/Bothbots/Pingbot"

    if [ ! -d "node_modules" ]; then
        npm install 
    else
        npm update 
    fi

    log_success "Ping Bot dependencies installed"
}

################################################################################
# Directory & File Setup
################################################################################

setup_directories() {
    log_info "Setting up directories..."

    # Create necessary directories
    mkdir -p "$LOG_DIR"
    mkdir -p "$WWW_PATH"
    mkdir -p "/etc/discord-bot"

    # Set permissions
    chown -R "$APP_USER:$APP_USER" "$LOG_DIR"
    chown -R "$APP_USER:$APP_USER" "$PROJECT_ROOT"
    chown -R www-data:www-data "$WWW_PATH"

    log_success "Directories created and permissions set"
}

copy_website_files() {
    log_info "Copying website files to web root..."

    # Copy static website files
    cp -r "$PROJECT_ROOT/Website"/* "$WWW_PATH/"

    # Copy Vite dist if available
    if [ -d "$PROJECT_ROOT/activitys/client/dist" ]; then
        cp -r "$PROJECT_ROOT/activitys/client/dist"/* "$WWW_PATH/"
    fi

    # Copy privacy and terms pages
    mkdir -p "$WWW_PATH/legal"
    cp -r "$PROJECT_ROOT/terms&privacy"/* "$WWW_PATH/legal/" 2>/dev/null || true

    chown -R www-data:www-data "$WWW_PATH"
    chmod -R 755 "$WWW_PATH"

    log_success "Website files copied"
}

create_env_files() {
    log_info "Creating .env files..."

    # Activity Server .env
    if [ ! -f "$PROJECT_ROOT/activitys/.env" ]; then
        cat > "$PROJECT_ROOT/activitys/.env" << EOF
NODE_ENV=$ENVIRONMENT
ACTIVITY_SERVER_PORT=$ACTIVITY_SERVER_PORT
EOF
        log_success "Created activitys/.env"
    fi

    # Mainbot .env
    if [ ! -f "$PROJECT_ROOT/Bothbots/Mainbot/.env" ]; then
        cat > "$PROJECT_ROOT/Bothbots/Mainbot/.env" << EOF
NODE_ENV=$ENVIRONMENT
DISCORD_TOKEN=${DISCORD_TOKEN}
EOF
        log_warning "Created Bothbots/Mainbot/.env - UPDATE WITH REAL TOKEN!"
    fi

    # Pingbot .env
    if [ ! -f "$PROJECT_ROOT/Bothbots/Pingbot/.env" ]; then
        cat > "$PROJECT_ROOT/Bothbots/Pingbot/.env" << EOF
NODE_ENV=$ENVIRONMENT
DISCORD_TOKEN=${DISCORD_TOKEN}
EOF
        log_warning "Created Bothbots/Pingbot/.env - UPDATE WITH REAL TOKEN!"
    fi

    chmod 600 "$PROJECT_ROOT/activitys/.env"
    chmod 600 "$PROJECT_ROOT/Bothbots/Mainbot/.env"
    chmod 600 "$PROJECT_ROOT/Bothbots/Pingbot/.env"
}

################################################################################
# Nginx Configuration
################################################################################

setup_nginx() {
    log_info "Setting up Nginx..."

    # Backup existing config if it exists
    if [ -f "/etc/nginx/sites-available/$DOMAIN" ]; then
        cp "/etc/nginx/sites-available/$DOMAIN" "/etc/nginx/sites-available/$DOMAIN.bak"
    fi

    # Create Nginx config
    cat > "/etc/nginx/sites-available/$DOMAIN" << 'NGINX_CONFIG'
server {
    listen 80;
    listen [::]:80;
    server_name DOMAIN_PLACEHOLDER www.DOMAIN_PLACEHOLDER;

    root /var/www/discord-bot;
    index index.html;

    access_log /var/log/nginx/discord-bot_access.log;
    error_log /var/log/nginx/discord-bot_error.log;

    # Static files
    location / {
        try_files $uri $uri/ /index.html;
    }

    # API proxy to activity server
    location /api/ {
        proxy_pass http://localhost:3002/api/;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # Cache static assets
    location ~* \.(css|js|jpg|jpeg|png|gif|ico|svg|woff|woff2|ttf|eot)$ {
        expires 30d;
        add_header Cache-Control "public, immutable";
    }

    # Security headers
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-XSS-Protection "1; mode=block" always;
    add_header Referrer-Policy "strict-origin-when-cross-origin" always;
}
NGINX_CONFIG

    # Replace domain placeholder
    sed -i "s/DOMAIN_PLACEHOLDER/$DOMAIN/g" "/etc/nginx/sites-available/$DOMAIN"

    # Enable site
    ln -sf "/etc/nginx/sites-available/$DOMAIN" "/etc/nginx/sites-enabled/$DOMAIN"

    # Disable default site
    rm -f "/etc/nginx/sites-enabled/default"

    # Test and reload
    if nginx -t; then
        systemctl reload nginx
        log_success "Nginx configured and reloaded"
    else
        log_error "Nginx configuration test failed"
        exit 1
    fi
}

setup_ssl() {
    log_info "Setting up SSL certificate..."

    if [ -d "/etc/letsencrypt/live/$DOMAIN" ]; then
        log_info "SSL certificate already exists for $DOMAIN"
        return
    fi

    certbot certonly --nginx -d "$DOMAIN" -d "www.$DOMAIN" --agree-tos --no-eff-email --email admin@$DOMAIN -n || {
        log_warning "SSL setup failed - continuing without HTTPS"
        return
    }

    # Update Nginx config to use SSL
    cat >> "/etc/nginx/sites-available/$DOMAIN" << 'NGINX_SSL'

# HTTPS redirect (auto-added by certbot)
server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name DOMAIN_PLACEHOLDER www.DOMAIN_PLACEHOLDER;

    ssl_certificate /etc/letsencrypt/live/DOMAIN_PLACEHOLDER/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/DOMAIN_PLACEHOLDER/privkey.pem;

    root /var/www/discord-bot;
    index index.html;

    # ... rest of server block from above ...
}
NGINX_SSL

    nginx -t && systemctl reload nginx
    log_success "SSL certificate installed"
}

################################################################################
# Service Management
################################################################################

setup_pm2_services() {
    log_info "Setting up PM2 services..."

    cd "$PROJECT_ROOT"

    # Kill existing processes
    pm2 delete all 2>/dev/null || true

    # Start Activity Server
    log_info "Starting Activity Server..."
    pm2 start "activitys/server/server.js" \
        --name "activity-server" \
        --log "$LOG_DIR/activity-server.log" \
        --error "$LOG_DIR/activity-server-error.log"

    # Start Main Bot
    log_info "Starting Main Bot..."
    pm2 start "Bothbots/Mainbot/infobot.js" \
        --name "mainbot" \
        --log "$LOG_DIR/mainbot.log" \
        --error "$LOG_DIR/mainbot-error.log"

    # Start Ping Bot
    log_info "Starting Ping Bot..."
    pm2 start "Bothbots/Pingbot/pingbot.js" \
        --name "pingbot" \
        --log "$LOG_DIR/pingbot.log" \
        --error "$LOG_DIR/pingbot-error.log"

    # Save PM2 processes
    pm2 save
    pm2 startup systemd -u root --hp /root

    log_success "PM2 services started and configured to auto-start"
}

################################################################################
# Firewall Setup
################################################################################

setup_firewall() {
    log_info "Setting up firewall rules..."

    # Check if UFW is installed
    if ! command -v ufw &> /dev/null; then
        log_warning "UFW not installed, skipping firewall setup"
        return
    fi

    # Enable UFW
    ufw --force enable > /dev/null 2>&1 || true

    # Allow SSH
    ufw allow 22/tcp > /dev/null 2>&1 || true

    # Allow HTTP/HTTPS
    ufw allow 80/tcp > /dev/null 2>&1 || true
    ufw allow 443/tcp > /dev/null 2>&1 || true

    log_success "Firewall configured"
}

################################################################################
# Health Checks
################################################################################

health_check() {
    log_info "Running health checks..."

    local all_healthy=true

    # Check Node processes
    log_info "Checking Node.js processes..."
    pm2 status || {
        log_warning "Could not get PM2 status"
        all_healthy=false
    }

    # Check Nginx
    log_info "Checking Nginx..."
    if systemctl is-active --quiet nginx; then
        log_success "Nginx is running"
    else
        log_warning "Nginx is not running"
        all_healthy=false
    fi

    # Check ports
    log_info "Checking ports..."
    if netstat -tuln | grep -q ":80\|:443"; then
        log_success "Web ports (80/443) are listening"
    else
        log_warning "Web ports not listening"
    fi

    if netstat -tuln | grep -q ":3002"; then
        log_success "Activity server port (3002) is listening"
    else
        log_warning "Activity server not listening"
    fi

    if [ "$all_healthy" = true ]; then
        log_success "All health checks passed!"
        return 0
    else
        log_warning "Some health checks failed - review logs"
        return 1
    fi
}

################################################################################
# Main Deployment Flow
################################################################################

main() {
    log_info "=========================================="
    log_info "Discord Bot - Build & Deploy Script"
    log_info "Environment: $ENVIRONMENT"
    log_info "Domain: $DOMAIN"
    log_info "=========================================="

    check_root
    run_preflight_checks

    log_info "Starting deployment..."

    # Phase 1: System Setup
    log_info "PHASE 1: System Setup"
    install_system_dependencies
    create_app_user
    install_pm2
    setup_directories
    create_env_files

    # Phase 2: Build
    log_info "PHASE 2: Building Applications"
    build_vite_frontend
    install_activity_server
    install_mainbot
    install_pingbot

    # Phase 3: Deployment
    log_info "PHASE 3: Deploying to Web Server"
    copy_website_files
    setup_nginx
    setup_ssl

    # Phase 4: Services
    log_info "PHASE 4: Starting Services"
    setup_pm2_services
    setup_firewall

    # Phase 5: Verification
    log_info "PHASE 5: Health Checks"
    sleep 3
    health_check

    # Summary
    log_success "=========================================="
    log_success "Deployment completed successfully!"
    log_success "=========================================="
    log_info "Website: https://$DOMAIN"
    log_info "Activity Server: http://localhost:3002"
    log_info ""
    log_info "View logs:"
    log_info "  Main Bot:     pm2 logs mainbot"
    log_info "  Ping Bot:     pm2 logs pingbot"
    log_info "  Activity:     pm2 logs activity-server"
    log_info ""
    log_info "Manage services:"
    log_info "  pm2 start/stop/restart/reload all"
    log_info "  pm2 save"
    log_info ""
    log_warning "IMPORTANT: Update .env files with your Discord tokens:"
    log_warning "  - $PROJECT_ROOT/Bothbots/Mainbot/.env"
    log_warning "  - $PROJECT_ROOT/Bothbots/Pingbot/.env"
}

# Handle errors
trap 'log_error "Deployment failed on line $LINENO"; exit 1' ERR

# Run main function
main "$@"
