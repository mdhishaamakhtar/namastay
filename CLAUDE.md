# Namastay

A free trip planner for first-time foreign visitors to India. F# everywhere: ASP.NET Core minimal APIs, Fable, Elmish and Feliz, on Postgres. Production runs on Railway at `namastay.hishaam.dev` and `api.namastay.hishaam.dev`.

## Commands

- `make up` starts Postgres (port 5433) and RustFS. `make dev` runs the API on 5080. `make client` runs the site on 5173.
- `make test-all` runs every suite. `make check-all` adds format, lint and coverage.
- `make import`, `make media-sync` and `make media-prune` load `data/`.
- After a client change, `cd src/Namastay.Client && dotnet fable . -o build` shows compile errors.

## Layout

`Web -> Data -> Domain -> Shared`, and `Client -> Shared`. See `docs/architecture.md`. Server entry point: `src/Namastay.Web/Program.fs`. Trip logic: `src/Namastay.Domain/Trip/`. Every route is in `src/Namastay.Shared/Routes.fs`.

## Rules

- **Private data stays out of git.** `data/`, research and photos are never committed. Only the invented `samples/` are public, plus our own artwork.
- `data/` is the source of truth for content: edit its JSON directly, then import.
- Never recommend drugs. Kashmir, Manipur and the Pakistan border belt stay excluded.
- Every failure is an `ApiError`. Every route is typed in `Routes.fs`.
- Parameters only in SQL. Anything interpolated into a query is a fixed name from the code.
- The CDN ignores cookies. `Endpoint.mapData` is public and cached, so it's only for data that's the same for everyone. Personal routes use `mapGet`, which sends `no-store`.
- Show new pages and visual changes on the design canvas first. Alignment comes first, and loading states are skeletons of the page.
- Tests check behaviour, not real data, and stay fast, parallel and free of sleeps. Fix the cause of a flaky test.
- Small files, one job each. Match the surrounding code. Commits go through a hook that formats and lints.

## Production data

The site is in alpha, so a brief break around a deploy is fine. Saved and shared trips must keep opening, so add new trip fields to `TripSnapshot.fs`.

- **Small changes:** import locally, then send only the changed rows through a `railway ssh` tunnel in one transaction. Insert a committed row into `import_batches` so the API reloads its data, then run `railway cdn purge all --service api`.
- **`make publish`:** rewrites all content row by row, so it takes 20 minutes or more. It keeps accounts and trips. See issue #10.
- **`pg_dump` streaming:** fast, but it wipes accounts and saved trips. Never use it unless the owner chooses that. See `docs/deploy.md`.

Only the importer runs migrations, so a new column reaches production before the code that reads it merges. Importer changes need `data/core/places.json` updated as well as `data/places/*.json`, because both are parsed even though place files win. Never change production, wipe data or merge without saying what will happen first.

## Food

- A dish needs a `photoSlug` to appear on the Food page or have a dish page. Guides and itineraries show every dish.
- `vegan` means vegan as usually made, or with one simple request that the description spells out.
- Trips store their food when they're built, so older trips don't gain new dishes.
- Giraffe's `voidTag` leaves SVG shapes unclosed, so use `tag "rect" [ ... ] []` for shapes with siblings.

## Checking the site

Run `make test-e2e` for browser checks. The desktop app's Browser pane pauses animations while hidden, so swipes stall there. After a deploy, check the live site in a fresh browser, because an open tab keeps the old version.
