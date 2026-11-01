#!/usr/bin/env bash
# Sends data/ to production and rebuilds the website from it, all in one go.
# Needs the Railway CLI, logged in and linked to the project.
set -euo pipefail

tunnel_port=15432
config=$(mktemp)

railway_variables() {
    railway variables --service "$1" --json
}

stop_tunnel() {
    [ -n "${tunnel:-}" ] && kill "$tunnel" 2>/dev/null || true
    rm -f "$config"
}
trap stop_tunnel EXIT

rm -f "$config"
railway ssh config --service Postgres --alias namastay-postgres --path "$config" > /dev/null
ssh -F "$config" -o BatchMode=yes -o ExitOnForwardFailure=yes -N \
    -L "$tunnel_port:127.0.0.1:5432" namastay-postgres &
tunnel=$!
sleep 5

eval "$(RAILWAY_TUNNEL_PORT=$tunnel_port python3 scripts/publish_settings.py)"

make import
make media-sync
railway cdn purge all --service api
sleep 15
railway variables --service website --set "CONTENT_STAMP=$(date -u +%Y%m%d%H%M%S)"
echo "Data sent. The website is rebuilding on Railway."
