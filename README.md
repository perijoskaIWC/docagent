# DocAgent

DocAgent turns an export pack from a source system (CSV, OpenAPI, XML, Excel, PDF, ...) into an evidence-backed
solution design, and it is a lab for comparing agent strategies with numbers. The plan, the architecture and one guide
per team member are in [docs/guides/](docs/guides/). Start with
[00-team-overview.md](docs/guides/00-team-overview.md).

**New to the project?** Follow [docs/onboarding.md](docs/onboarding.md): from clone to your own fake flag turned off,
with fixes for the usual problems.

## Prerequisites

| Tool | Check |
|---|---|
| .NET SDK 10 | `dotnet --version` |
| Node.js 22 or newer | `node --version` |
| Docker Desktop **or** Podman, running | `docker info` / `podman info` |
| Azure CLI (for the real Azure services, later) | `az --version` |
| `dotnet-ef` (only to add migrations) | `dotnet tool install --global dotnet-ef` |

**Podman instead of Docker:** set the user environment variable `ASPIRE_CONTAINER_RUNTIME` to `podman` once, start
the Podman machine (`podman machine start`), and open a new terminal.

## Run it

```bash
git clone <repo URL> docagent
cd docagent
cd src/web && npm install && cd ../..        # once, and again after package.json changes

# Once: the Azure endpoints the AppHost passes to the API (placeholders work while every fake is on)
cd src/DocAgent.AppHost
dotnet user-secrets set "Parameters:foundry-endpoint" "https://placeholder.invalid/"
dotnet user-secrets set "Parameters:aiservices-endpoint" "https://placeholder.invalid/"
dotnet user-secrets set "Parameters:search-endpoint" "https://placeholder.invalid/"
cd ../..

dotnet run --project src/DocAgent.AppHost
```

The console prints a **dashboard** link (`https://localhost:17083/login?t=...`). The dashboard shows four resources:
`postgres`, `docagentdb`, `api` and `web`, with their logs, traces and health. Click the URL next to **web** to open
the app. The first start pulls the Postgres image and creates the database; migrations run on every start in
Development. Stop everything with **Ctrl+C**.

The API listens on `https://localhost:7133`; [DocAgent.Api.http](src/DocAgent.Api/DocAgent.Api.http) has a request for
every endpoint (VS Code: REST Client extension; Visual Studio: built in). The real values for the endpoints are in
[docs/environments.md](docs/environments.md) once Darko has filled it in.

## Fakes: build your part on your own

Every contract in `src/DocAgent.Core` has a fake, and the flags in
[src/DocAgent.Api/appsettings.Development.json](src/DocAgent.Api/appsettings.Development.json) choose between the
fake and the real implementation (`Features:Fakes:<Name>`). The table of flags, entry points and owners is in
[team overview §6.3](docs/guides/00-team-overview.md).

To try your real implementation, turn off **your** flag in your own user-secrets first, not in the shared file:

```bash
dotnet user-secrets set "Features:Fakes:Chat" "false" --project src/DocAgent.Api
```

When it works end to end, change the shared default in a pull request.

## Choosing a strategy

Each run picks one implementation per experiment slot: **detector** (E1), **author** (E2) and **retrieval** (E3).

- In the app: **New run → Lab settings**. Fake strategies are marked "(fake)".
- In the API: the `detector`, `author` and `retrieval` form fields of `POST /api/runs`.
  `GET /api/strategies` lists what is registered.
- Missing slots use `Strategy:Default` in [src/DocAgent.Api/appsettings.json](src/DocAgent.Api/appsettings.json).

A new strategy is a keyed registration plus a `StrategyOption` in your entry point; see [AGENTS.md](AGENTS.md).

## Before a pull request

```bash
dotnet build && dotnet test
cd src/web && npm run build && npm run lint
```

CI runs the same checks on every pull request ([.github/workflows/ci.yml](.github/workflows/ci.yml)).

## Where things are

| Path | What | Owner |
|---|---|---|
| `src/DocAgent.Core` | The contracts: records and interfaces, no dependencies | Darko |
| `src/DocAgent.Fakes` | A fake for every contract | Elena, then each owner |
| `src/DocAgent.Api` | Minimal API, `RunWorker`, Server-Sent Events | Elena |
| `src/DocAgent.Infrastructure` | EF Core, PostgreSQL, `IRunReader` | Elena |
| `src/DocAgent.Profiling` | Profiler, extractors, tools, validators, checks | Riste |
| `src/DocAgent.Agents` | Pipeline, authors, prompts, templates, renderer | Boban, Stevcho, Riste |
| `src/DocAgent.Knowledge` | Ingestion, detection, retrieval, Ask | Trajche, Todor |
| `src/DocAgent.Safety` | Content Safety screening | Elena |
| `src/DocAgent.AppHost`, `ServiceDefaults` | Aspire orchestration, telemetry, health | Elena |
| `src/web` | React app ([its README](src/web/README.md)) | Elena; each owner adds their tab |
| `tests/` | Unit tests; the eval harness later | |
| `samples/`, `knowledge/`, `evals/` | Synthetic packs, knowledge corpus, eval data | Darko, Trajche, Elena |
| `docs/adr/` | Architecture decision records | everyone |

## Rules

- **Synthetic or public data only.** Nothing from any customer, anywhere in the repo.
- **No secrets in the repo.** Use `dotnet user-secrets`; endpoints are configuration, keys are secrets.
- **`src/DocAgent.Core` changes go through Darko** ([team overview §6.4](docs/guides/00-team-overview.md)).
- AI coding agents follow [AGENTS.md](AGENTS.md).
