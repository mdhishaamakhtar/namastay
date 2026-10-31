# Data format

This repository contains no travel data. The research and the `data/` folder are private, and visitors only reach them through the API. To run your own copy, do your own research, write it in this format, and import it.

The importer reads JSON only, and loads it into Postgres. Nothing is edited in the database by hand.

- `manifest.json`: `{ "complete": true }` marks the batch as the whole catalog. Records missing from a complete batch are retired, never deleted.
- `core/`: regions, clusters, colour families, interests, places, transport links, festivals, notices, exclusions and sources.
- `places/<slug>.json`: a full guide record. It replaces that place's short record from `core/places.json`.
- `journey/`: cards and illustrations.

Check a batch with `make import-check`, then load it with `make import`. A batch either passes every check and commits, or changes nothing.
