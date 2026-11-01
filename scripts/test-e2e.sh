#!/usr/bin/env bash
# Runs the browser tests against their own database and the production bundle, so
# the data loaded for development and the dev server's speed never change the result.
# Needs `make up`.
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

until docker compose exec -T postgres pg_isready --quiet -U namastay; do
    sleep 1
done

psql_admin "drop database if exists $database with (force)"
psql_admin "create database $database"

export NAMASTAY_DB="$connection"
dotnet run ${TEST_FLAGS:-} --project src/Namastay.Importer -- samples/data > /dev/null

[ -n "${TEST_FLAGS:-}" ] || dotnet build src/Namastay.Web --verbosity quiet
ASPNETCORE_ENVIRONMENT=Development ConnectionStrings__Namastay="$connection" Site__Url="http://localhost:$client_port" \
    dotnet run --no-build --no-launch-profile --project src/Namastay.Web --urls "http://localhost:$api_port" > /dev/null &

(cd src/Namastay.Client && dotnet fable . -o build-e2e > /dev/null)
(cd src/Namastay.Client && VITE_API_URL= NAMASTAY_BUILD=build-e2e npx vite build --outDir dist-e2e --emptyOutDir > /dev/null)
(cd src/Namastay.Client && NAMASTAY_API="http://localhost:$api_port" exec npx vite preview --outDir dist-e2e --port "$client_port" --strictPort > /dev/null) &

wait_for "http://localhost:$api_port/health"
wait_for "http://localhost:$client_port"

NAMASTAY_URL="http://localhost:$client_port" dotnet test tests/Namastay.E2ETests ${TEST_FLAGS:-} --verbosity minimal -- xUnit.StopOnFail=true
