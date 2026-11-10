# 0002: The stack

Status: accepted (2026-11-10)

## Context

Seven people need to start building on the same day, in a codebase each of them can run alone. The stack had to be
agreed before the skeleton was built (guide 1, "Decisions already made").

## Decision

| Area | Choice |
|---|---|
| Runtime | .NET 10, C# latest, nullable reference types enabled |
| Orchestration | Aspire (AppHost and ServiceDefaults) |
| API | ASP.NET Core Minimal APIs, built-in OpenAPI |
| Background work | `Channel<Guid>` and a `BackgroundService` (`RunWorker`); no extra infrastructure |
| Streaming to the browser | Server-Sent Events (`text/event-stream`) |
| Database | PostgreSQL through Aspire (`AddAzurePostgresFlexibleServer().RunAsContainer()`: a container locally, Azure Database for PostgreSQL Flexible Server when deployed), EF Core with Npgsql |
| Frontend | React, Vite and TypeScript; Fluent UI React v9, React Router, TanStack Query, react-markdown |
| Strategies | Keyed services (`AddKeyedSingleton`, `GetRequiredKeyedService`); fakes registered for any key with `KeyedService.AnyKey` |
| Package versions | Central Package Management (`Directory.Packages.props`) |
| CI | GitHub Actions |
| Azure authentication | `DefaultAzureCredential`; no keys in code |

How it turned out when the skeleton was built:

- Versions: .NET SDK 10.0.4xx and Aspire 13.6. The web app came from create-vite 9, so Vite 8, React 19,
  TypeScript 7 and oxlint (instead of ESLint).
- Containers: Docker Desktop or Podman. With Podman, set `ASPIRE_CONTAINER_RUNTIME=podman`.
- CI uses Node 24 (Active LTS); Node 22 or newer works locally.

## Consequences

- One `dotnet run` starts everything (database, API and web app) with logs, traces and health in one dashboard, and
  `azd` can deploy the same AppHost to Azure.
- No message broker or job scheduler to run: one API instance processes runs one at a time, which is enough for a
  lab. Running several API instances later needs a shared queue first.
- Every contract has a fake, so each owner can build and demo their part before the others are done.
- Changing a stack choice needs a new ADR that supersedes this one.
