# Data format

This repository contains no travel data. The research and the `data/` folder are private, and visitors only reach them through the API. To run your own copy, do your own research, write it in this format, and import it. `samples/data` is a small invented example you can load straight away.

The importer reads JSON only, and loads it into Postgres. Nothing is edited in the database by hand.

- `manifest.json`: `{ "complete": true }` marks the batch as the whole catalog. Records missing from a complete batch are retired, never deleted.
- `core/`: regions, clusters, colour families, interests, places, transport links, festivals, notices, exclusions and sources.
- `places/<slug>.json`: a full guide record. It replaces that place's short record from `core/places.json`.
- `journey/`: cards and illustrations.

Check a batch with `make import-check`, then load it with `make import`. A batch either passes every check and commits, or changes nothing.

## Photos

Photos live in an S3-compatible object store, never in git. Locally that's RustFS, which `make up` starts, with a console at http://localhost:9001. Files in `data/media` keep the same paths in the store, so `photos/kasol.webp` is served at `/media/photos/kasol.webp`. Run `make media-sync` to upload new or changed files. It reads `MEDIA_ENDPOINT`, `MEDIA_BUCKET`, `MEDIA_ACCESS_KEY` and `MEDIA_SECRET_KEY`, and defaults to the local store.
