#!/usr/bin/env bash
# Runs the browser tests against their own database and servers, so the data
# loaded for development never changes the result. Needs `make up`.
set -euo pipefail

api_port=5081
client_port=5174
database=namastay_e2e
connection="Host=localhost;Port=5433;Database=$database;Username=namastay;Password=namastay"

stop_servers() {
    jobs -p | xargs -r kill 2>/dev/null || true
}
trap stop_servers EXIT

wait_for() {
    for _ in $(seq 1 120); do
        curl --silent --fail --output /dev/null "$1" && return 0
        sleep 0.25
    done
    echo "$1 didn't start." >&2
    return 1
}

psql_admin() {
    docker compose exec -T postgres psql --quiet -U namastay -d postgres -c "$1"
}

psql_admin "drop database if exists $database with (force)"
psql_admin "create database $database"

export NAMASTAY_DB="$connection"
dotnet run --project src/Namastay.Importer -- samples/data > /dev/null
dotnet run --project src/Namastay.Importer -- --media samples/media > /dev/null

dotnet build src/Namastay.Web --verbosity quiet
ASPNETCORE_ENVIRONMENT=Development ConnectionStrings__Namastay="$connection" \
    dotnet run --no-build --no-launch-profile --project src/Namastay.Web --urls "http://localhost:$api_port" > /dev/null &

(cd src/Namastay.Client && dotnet fable . -o build > /dev/null)
(cd src/Namastay.Client && NAMASTAY_API="http://localhost:$api_port" exec npx vite --port "$client_port" --strictPort > /dev/null) &

wait_for "http://localhost:$api_port/health"
wait_for "http://localhost:$client_port"

NAMASTAY_URL="http://localhost:$client_port" dotnet test tests/Namastay.E2ETests --verbosity quiet
