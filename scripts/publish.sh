#!/usr/bin/env bash
# Builds the local database from data/, then sends its travel content to production as one
# bulk stream and rebuilds the website. With --check it only says what would change.
# Needs `make up`, and the Railway CLI, logged in and linked to the project.
set -euo pipefail

check=${1:-}
tunnel_port=15432
config=$(mktemp)

stop_tunnel() {
    [ -n "${tunnel:-}" ] && kill "$tunnel" 2>/dev/null || true
    rm -f "$config"
}
trap stop_tunnel EXIT

# The local database is what gets published, so it's rebuilt from data/ first.
make import

rm -f "$config"
railway ssh config --service Postgres --alias namastay-postgres --path "$config" > /dev/null
ssh -F "$config" -o BatchMode=yes -o ExitOnForwardFailure=yes -N \
    -L "$tunnel_port:127.0.0.1:5432" namastay-postgres &
tunnel=$!
sleep 5

eval "$(RAILWAY_TUNNEL_PORT=$tunnel_port python3 scripts/publish_settings.py)"

if [ "$check" = "--check" ]; then
    dotnet run --project src/Namastay.Importer -- --publish-check
    exit 0
fi

published=$(dotnet run --project src/Namastay.Importer -- --publish | tee /dev/stderr)
uploaded=$(make --no-print-directory media-sync | tee /dev/stderr)

if grep -q "Nothing to change" <<< "$published" && grep -q "Uploaded 0 file" <<< "$uploaded"; then
    echo "Production already had all of it, so nothing was rebuilt."
    exit 0
fi

# The API remembers the data version for 5 seconds, and the CDN must not keep the old answer.
sleep 6
railway cdn purge all --service api
sleep 15
railway variables --service website --set "CONTENT_STAMP=$(date -u +%Y%m%d%H%M%S)"
echo "Published. The website is rebuilding on Railway."
