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

## First deploy

The website saves its pages from the live API while building, so the API and its data come first.

1. Deploy the API.
2. Load the data from a machine that has `data/`:
   `NAMASTAY_DB=<production connection string> make import`, then `make media-sync` with the `MEDIA_*` variables set.
3. Deploy the website.

## After changing travel data

Import as above, then change the website's `CONTENT_STAMP` variable to anything new. That rebuilds its pages.

If the API can't be reached, or any page fails to load, the website build stops and the live site stays as it was.
