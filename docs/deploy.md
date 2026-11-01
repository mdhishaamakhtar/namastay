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

## Loading data

Importing row by row over a tunnel takes far too long, so production gets a copy of a clean local database.

1. Rebuild local: drop the `namastay` database, create it again, then `make import`.
2. Open a tunnel to Postgres: `railway ssh config --service Postgres --alias nst-pg --path <file>`, then `ssh -F <file> -N -L 15432:127.0.0.1:5432 nst-pg`.
3. Reset production, which also deletes user accounts and saved trips, so only do this before launch: `psql -c "drop schema public cascade" -c "create schema public"`.
4. Stream it: `pg_dump --no-owner --no-privileges | psql --single-transaction`, pointing `psql` at the tunnel.

Keep the database off the public internet. Don't add a TCP proxy.

Once there are real users, a full reset would delete them, so run `make import` through the tunnel instead. It keeps users and trips, but it's slow until the importer batches its writes.

## Photos

`make media-sync` uploads what's new or changed, 16 at a time. Set `MEDIA_ENDPOINT`, `MEDIA_BUCKET`, `MEDIA_ACCESS_KEY` and `MEDIA_SECRET_KEY`, and `MEDIA_PATH_STYLE=false` for Railway. `make media-prune` removes bucket files gone from `data/media`, keeping copies in `MEDIA_BACKUP`.

## After changing travel data

```bash
make publish
```

That opens a private tunnel to Postgres, imports `data/`, uploads photos, clears the API's CDN cache and rebuilds the website. Keep your laptop awake: the import runs row by row over the tunnel, so it can take 20 minutes or more.

If the API can't be reached, or any page fails to load, the website build stops and the live site stays as it was.
