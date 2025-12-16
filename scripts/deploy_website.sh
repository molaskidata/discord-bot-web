#!/usr/bin/env bash
set -euo pipefail

# deploy_website.sh
# Usage: sudo ./deploy_website.sh [REPO_PATH] [TARGET_ROOT]
# Example: sudo ./deploy_website.sh /home/deploy/discord-bot-web /var/www/html

REPO_DIR="${1:-$(pwd)}"
TARGET_ROOT="${2:-/var/www/html}"

echo "Deploying website from ${REPO_DIR}/Website -> ${TARGET_ROOT}"

if [ ! -d "${REPO_DIR}" ]; then
  echo "ERROR: repo path does not exist: ${REPO_DIR}" >&2
  exit 2
fi

cd "${REPO_DIR}"

if [ -d .git ]; then
  echo "Pulling latest changes in ${REPO_DIR}"
  git fetch --all --prune
  git pull --rebase
else
  echo "Warning: ${REPO_DIR} is not a git repo — skipping git pull"
fi

echo "Syncing Website/ to ${TARGET_ROOT}"
rsync -av --delete --chmod=Du=rwx,Dg=rx,Do=rx,Fu=rw,Fg=r,Fo=r Website/ "${TARGET_ROOT}/"

echo "Setting ownership to www-data:www-data"
chown -R www-data:www-data "${TARGET_ROOT}"

echo "Reloading nginx"
systemctl reload nginx

echo "Deployment complete. Served files in ${TARGET_ROOT}" 
