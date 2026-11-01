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

## First deploy

The website saves its pages from the live API while building, so the API and its data come first.

1. Deploy the API.
2. Load the data from a machine that has `data/`:
   `NAMASTAY_DB=<production connection string> make import`, then `make media-sync` with the `MEDIA_*` variables set.
3. Deploy the website.

## After changing travel data

1. Import as above.
2. Wait 5 minutes, so cached API data expires. If you replaced a photo at the same path, run `railway cdn purge all` on the API, because photos cache for a year.
3. Change the website's `CONTENT_STAMP` variable to anything new. That rebuilds its pages, and the deploy clears the cached ones.

If the API can't be reached, or any page fails to load, the website build stops and the live site stays as it was.
