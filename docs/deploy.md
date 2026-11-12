# Deploying

Everything runs on Railway, as two services plus Postgres and a bucket. Both services deploy from GitHub.

| Service | Address | Dockerfile (`RAILWAY_DOCKERFILE_PATH`) |
|---|---|---|
| API | `api.namastay.hishaam.dev` | `Dockerfile.api` |
| Website | `namastay.hishaam.dev` | `Dockerfile.site` |

## API variables

- `ConnectionStrings__Namastay`
- `Auth__TokenKey`: a random secret of at least 32 characters
- `Site__Url`: `https://namastay.hishaam.dev`
- `Media__Endpoint`, `Media__Bucket`, `Media__AccessKey`, `Media__SecretKey`

The server refuses to start without these, and names what's missing.

## CDN

Railway's CDN is free but off by default, so turn it on for both services, under **Settings > Edge > Enable CDN Caching**, or:

```bash
railway cdn enable --service <service>
```

Keep the defaults: **HTML Caching** on Auto and **Purge Cache on Deploy** on HTML.

- **Website:** Caddy sends `s-maxage` on pages, because Auto mode only caches HTML with a cache header. Each deploy clears them.
- **API:** public travel data caches for 5 minutes, and photos for a year. Everything else sends `no-store`. The CDN ignores cookies, so a personal answer must never send a public cache header.

To check it, request a page or photo twice. The second answer should say `x-cache: HIT`, and `/.railway/cdn-trace` shows which edge location answered. Hits don't reach the service, so its logs will undercount visitors.

## Publishing travel data

```bash
make publish-check
make publish
```

`make publish` builds the local database from `data/`, then sends its travel content to production the way a dump and restore would: each content table goes through a private tunnel as one bulk stream into a temporary copy, and Postgres then writes only the rows that differ. The whole data set takes a few seconds, however much changed.

- **What it touches.** Only the tables listed in `src/Namastay.Data/Import/ContentTables.fs`. Accounts, shared itineraries and saved trips aren't listed, so they're never read or written.
- **One transaction.** If anything is rejected, production is left exactly as it was.
- **What it prints.** Each table's new, changed and removed rows, by name. `make publish-check` prints the same and writes nothing.
- **Migrations run first**, so a new column is there before its data arrives. A dry run can't run them, so it stops and says so when production is behind.
- **Removed rows.** A place, experience, route or card that's gone from the local database is marked retired, not deleted, because a saved trip may still show it. What belongs to a retired place stays with it. Regions, interests, sources and photos are never removed.
- **Afterwards.** It uploads new photos, clears the API's CDN cache and rebuilds the website. When nothing changed, it stops before that.

Production becomes a copy of the local database, so the local database has to be right. `make publish` runs `make import` first for that reason, and it refuses to publish from a database with no places.

A new content table or column must be added to `ContentTables.fs`. A test fails until it is.

Keep the database off the public internet. Don't add a TCP proxy.

### Starting over

A full `pg_dump` and restore replaces everything, including user accounts and saved trips, so it's only for before launch or when the owner chooses it:

1. Rebuild local: drop the `namastay` database, create it again, then `make import`.
2. Open a tunnel to Postgres: `railway ssh config --service Postgres --alias nst-pg --path <file>`, then `ssh -F <file> -N -L 15432:127.0.0.1:5432 nst-pg`.
3. Reset production: `psql -c "drop schema public cascade" -c "create schema public"`.
4. Stream it: `pg_dump --no-owner --no-privileges | psql --single-transaction`, pointing `psql` at the tunnel.

## Photos

`make media-sync` uploads what's new or changed, 16 at a time. Set `MEDIA_ENDPOINT`, `MEDIA_BUCKET`, `MEDIA_ACCESS_KEY` and `MEDIA_SECRET_KEY`, and `MEDIA_PATH_STYLE=false` for Railway. `make media-prune` removes bucket files gone from `data/media`, keeping copies in `MEDIA_BACKUP`.

If the API can't be reached while the website rebuilds, or any page fails to load, the build stops and the live site stays as it was.
