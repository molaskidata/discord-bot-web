#!/bin/bash

################################################################################
# Discord Bot - Update & Restart Script
# Quick update script for redeploying after code changes
# Usage: ./update.sh [mainbot|pingbot|activity|all]
################################################################################

set -e

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
UPDATE_TYPE="${1:-all}"

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

update_mainbot() {
    log_info "Updating Main Bot..."
    cd "$PROJECT_ROOT/Bothbots/Mainbot"
    npm update --quiet
    pm2 restart mainbot --update-env
    sleep 2
    pm2 logs mainbot --lines 10
    log_success "Main Bot restarted"
}

update_pingbot() {
    log_info "Updating Ping Bot..."
    cd "$PROJECT_ROOT/Bothbots/Pingbot"
    npm update --quiet
    pm2 restart pingbot --update-env
    sleep 2
    pm2 logs pingbot --lines 10
    log_success "Ping Bot restarted"
}

update_activity() {
    log_info "Updating Activity Server..."
    cd "$PROJECT_ROOT/activitys/server"
    npm update --quiet
    pm2 restart activity-server --update-env
    sleep 2
    pm2 logs activity-server --lines 10
    log_success "Activity Server restarted"
}

update_website() {
    log_info "Updating website files..."
    cd "$PROJECT_ROOT"
    
    if [ -d "activitys/client/dist" ]; then
        cp -r activitys/client/dist/* /var/www/discord-bot/
    fi
    
    cp -r Website/* /var/www/discord-bot/
    chown -R www-data:www-data /var/www/discord-bot
    
    log_success "Website files updated"
}

pull_latest() {
    log_info "Pulling latest changes from git..."
    cd "$PROJECT_ROOT"
    git pull
    log_success "Repository updated"
}

case "$UPDATE_TYPE" in
    mainbot)
        pull_latest
        update_mainbot
        ;;
    pingbot)
        pull_latest
        update_pingbot
        ;;
    activity)
        pull_latest
        update_activity
        ;;
    website)
        pull_latest
        update_website
        ;;
    all)
        pull_latest
        update_mainbot
        update_pingbot
        update_activity
        update_website
        ;;
    *)
        log_error "Unknown update type: $UPDATE_TYPE"
        echo "Usage: $0 [mainbot|pingbot|activity|website|all]"
        exit 1
        ;;
esac

log_success "Update completed!"
