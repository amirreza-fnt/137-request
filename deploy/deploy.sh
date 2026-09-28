#!/bin/bash

# ============================================
# Deploy Script - Request Service (ثبت درخواست)
# apiweb-137request.sabzevar.ir :5006
# Runs in Development so Swagger UI is available at /swagger.
# ============================================
set -e

APP_NAME="requestservice"
API_DIR="/opt/$APP_NAME"
SERVICE_FILE="/etc/systemd/system/$APP_NAME.service"
NGINX_CONF="/etc/nginx/sites-available/apiweb-137request"
DOTNET_ENV="Development"

echo "============================================"
echo "   Deploying $APP_NAME"
echo "============================================"

# Ensure service account exists before publish (ownership fix below).
sudo useradd -r -s /usr/sbin/nologin requestservice 2>/dev/null || true
sudo mkdir -p /var/log/requestservice
sudo chown requestservice:requestservice /var/log/requestservice

# Stop before publish so DLLs are not locked; wipe output to avoid mixed builds
# (mixed DLLs cause Swagger BadImageFormatException on /swagger/v1/swagger.json).
echo "[0/4] Stopping service and cleaning $API_DIR ..."
sudo systemctl stop "$APP_NAME" 2>/dev/null || true
sudo mkdir -p "$API_DIR"
sudo rm -rf "${API_DIR:?}"/*

# 1. Build & Publish
echo "[1/4] Publishing application..."
dotnet clean src/RequestService.Api/RequestService.Api.csproj -c Release >/dev/null 2>&1 || true
dotnet publish src/RequestService.Api/RequestService.Api.csproj \
    -c Release \
    -o "$API_DIR" \
    --self-contained false

sudo chown -R requestservice:requestservice "$API_DIR"

# 2. Copy nginx config
echo "[2/4] Setting up nginx..."
if [ -f "$NGINX_CONF" ]; then
    echo "  nginx config already exists, skipping..."
else
    sudo cp deploy/nginx.conf "$NGINX_CONF"
    sudo ln -sf "$NGINX_CONF" /etc/nginx/sites-enabled/
fi

# 3. Setup systemd service
echo "[3/4] Setting up systemd service..."
if [ -f "$SERVICE_FILE" ]; then
    echo "  systemd unit already exists, skipping... (edit $SERVICE_FILE directly)"
else
    sudo cp deploy/requestservice.service "$SERVICE_FILE"
fi

# 4. Enable & restart service
echo "[4/4] Starting service..."
sudo systemctl daemon-reload
sudo systemctl enable "$APP_NAME"
sudo systemctl restart "$APP_NAME"
sudo systemctl status "$APP_NAME" --no-pager

echo ""
echo "============================================"
echo "   Deploy complete!"
echo "   Service: $APP_NAME"
echo "   API: https://apiweb-137request.sabzevar.ir"
echo "   Health: https://apiweb-137request.sabzevar.ir/health"
echo "   Swagger: https://apiweb-137request.sabzevar.ir/swagger"
echo "============================================"
echo ""
echo "Check logs: sudo journalctl -u $APP_NAME -f"
