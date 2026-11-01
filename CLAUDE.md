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

Restore `data/` by importing locally, then streaming `pg_dump` through a `railway ssh` tunnel. Row-by-row imports over the tunnel are far too slow. See `docs/deploy.md`.
