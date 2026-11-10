# AGENTS.md

Instructions for AI coding agents (Claude Code, GitHub Copilot and others) working in this repository. People start
with [README.md](README.md).

## The project

DocAgent turns an export pack from a source system (CSV, OpenAPI, XML, Excel, PDF, ...) into an evidence-backed
solution design: code profiles the files, a detector recognises the system, and agents draft and review the design.
It is also a lab: every run picks one strategy per experiment slot, so the team can compare approaches with numbers.

Read [docs/guides/00-team-overview.md](docs/guides/00-team-overview.md) first. Every team member has a guide in
[docs/guides/](docs/guides/); the one for the area you are changing says who owns it and what is planned.

## Rules

- **Never modify `src/DocAgent.Core` without telling the user first.** It holds the contracts the whole team builds
  on; a change breaks other people's work. Contract changes are agreed with Darko (team overview §6.4).
- **Never commit secrets.** Keys go in `dotnet user-secrets`. Endpoints are configuration; keys are secrets.
- **Synthetic or public data only.** No customer data, exports, documents, names or credentials anywhere: code,
  tests, `samples/`, `knowledge/`, prompts or traces. When unsure, ask the user.
- Don't commit or push unless the user asks.

## Commands

```bash
dotnet build                                    # the whole solution
dotnet test                                     # unit tests; no database or containers needed
dotnet run --project src/DocAgent.AppHost       # Postgres + API + web app, with the Aspire dashboard
cd src/web && npm run build && npm run lint     # web app: type check and build, then lint
dotnet ef migrations add <Name> -p src/DocAgent.Infrastructure -s src/DocAgent.Api
```

- Aspire needs Docker or Podman running (Podman: user environment variable `ASPIRE_CONTAINER_RUNTIME=podman`).
- While the AppHost runs, the API locks its `bin/` folder. Build or test with `--artifacts-path <a temp folder>`.

## Conventions

- **Contracts first.** Components talk only through the interfaces in `DocAgent.Core`. Each has a fake in
  `DocAgent.Fakes`. The flag `Features:Fakes:<Name>` switches between the fake and the owner's entry point
  (`AddDocAgent<Area>` in their project): one flag, one entry point, one owner (team overview §6.3).
- **A new strategy is a keyed implementation plus a `StrategyOption`.** In the owner's entry point:
  `services.AddKeyedSingleton<IDesignAuthor, MyAuthor>("my-key")` and
  `services.AddSingleton(new StrategyOption("author", "my-key", "What it does", IsFake: false))`.
  It then appears in `GET /api/strategies` and the UI. This is not a contract change.
- **Ids are stable and content-derived**: `entity:work_orders`, `field:work_orders.technician_id` (team overview §6.2).
- **Package versions live only in `Directory.Packages.props`** (Central Package Management); `.csproj` files have no
  versions. `dotnet add package` sorts new entries to the top of that file: move them under the right heading.
- **Prompts are versioned Markdown files** in `src/DocAgent.Agents/Prompts/` or `src/DocAgent.Knowledge/Prompts/`,
  never string literals in code.
- **API** (`src/DocAgent.Api`): Minimal APIs. Errors are ProblemDetails; a 400 lists its problems per field. Every
  endpoint has a request in `src/DocAgent.Api/DocAgent.Api.http`. Long work runs in the `RunWorker`, never inside a
  request.
- **Database**: EF Core with Npgsql; a model change needs a migration, or the API refuses to start in Development.
- **Web app** (`src/web`): server data only through the TanStack Query hooks in `api/hooks.ts`, never `fetch` in a
  component; one typed function per endpoint in `api/client.ts`; Fluent UI v9 and `makeStyles`, no other CSS
  framework; each run-page tab is its own file in `pages/run/`. Write `useEffect` bodies with braces, so an effect
  never returns a value by accident (React would call it as the cleanup).
- **Tests**: xUnit in `tests/DocAgent.UnitTests`, with synthetic inputs from `TestData.cs`.
- **Git**: branches `feat/<area>/<short-desc>`; small pull requests; CI must pass.

## Keep in sync

`.github/copilot-instructions.md` is a copy of this file for GitHub Copilot, and CI fails when the two differ.
After changing this file: `cp AGENTS.md .github/copilot-instructions.md`.
