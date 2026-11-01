<!-- markdownlint-disable MD033 -->

<p align="center">
  <img src="./.github/assets/namastay-preview.png" alt="Namastay: swipe through India, get your trip">
</p>

## Namastay

Namastay is a free trip planner for first-time visitors to India. Swipe through places, food and experiences, and it turns what you like into a day-by-day itinerary.

**Live at [namastay.hishaam.dev](https://namastay.hishaam.dev)**

## What It Does

- Learns your taste from a swipe journey, then builds a realistic route that lands and flies home from a major city
- Writes honest guides to 68 places, with what to see, when to go, how to get there and where to stay
- Shows what to eat in each region, and where people go for it
- Saves and edits trips for signed-in visitors, with a share link for each one
- Gives every public page real HTML, structured data and a share image, so search engines and link previews read it properly

## Tech Stack

[![F#](https://img.shields.io/badge/F%23-10-378BBA?style=for-the-badge&logo=fsharp&logoColor=white)](https://fsharp.org/)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-Minimal_APIs-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis)
[![Fable](https://img.shields.io/badge/Fable-5-378BBA?style=for-the-badge)](https://fable.io/)
[![React](https://img.shields.io/badge/React-19-149ECA?style=for-the-badge&logo=react&logoColor=white)](https://react.dev/)
[![Vite](https://img.shields.io/badge/Vite-7-646CFF?style=for-the-badge&logo=vite&logoColor=white)](https://vite.dev/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17-336791?style=for-the-badge&logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![Playwright](https://img.shields.io/badge/Playwright-Tests-2EAD33?style=for-the-badge&logo=playwright&logoColor=white)](https://playwright.dev/)
[![Railway](https://img.shields.io/badge/Railway-Hosting-0B0D0E?style=for-the-badge&logo=railway&logoColor=white)](https://railway.com/)

## Quick Start

You need the .NET 10 SDK, Node and Docker.

```bash
make setup
make up
make sample
```

Then run these in two terminals:

```bash
make dev
```

```bash
make client
```

Open [http://localhost:5173](http://localhost:5173). `make sample` loads a small invented catalogue, because the real travel data is private and reaches visitors only through the API.

## How It's Built

| Project | What it holds |
| --- | --- |
| `src/Namastay.Shared` | Contracts and typed routes, compiled for both the server and the browser |
| `src/Namastay.Domain` | Pure rules: scoring, routing and building trips |
| `src/Namastay.Data` | SQL, migrations and the import |
| `src/Namastay.Web` | The API and guide pages. Start at `Program.fs` |
| `src/Namastay.Client` | The website, F# compiled to JavaScript. Start at `App.fs` |
| `src/Namastay.Importer` | Loads data into Postgres and photos into the bucket |

The API stands alone at its own address, so the website and any future native app use it the same way. More in [docs/architecture.md](docs/architecture.md).

## Common Commands

```bash
make test-all    # unit, integration and browser tests
make check-all   # the above, plus format, lint and coverage
make import      # load data/ into Postgres
make media-sync  # upload data/media to the bucket
make down        # stop Postgres and the photo store
```

## Docs

- [Architecture](docs/architecture.md)
- [Data format](docs/data-format.md)
- [Deploying](docs/deploy.md)

## License

[GPL-3.0](LICENSE). This covers the code. The travel data and photos aren't part of this repository.
