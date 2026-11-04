# Elena — Guide 1: Project Setup

**You build the foundation everyone else builds on.** Until the skeleton runs, nobody can integrate, so for the next eight working days you are the critical path. After that you move to your second area, production readiness (`elena-2-observability-evals-safety.md`), and keep reviewing changes to the API and the web app.

Read `00-team-overview.md` first. Its contracts (§6.1), flags (§6.3) and repo layout (§7) are inputs to this guide. Darko creates the GitHub repo and the Azure resources in parallel (`darko-azure-and-lead.md`); you don't wait for Azure, because everything in this guide runs on fakes.

---

## Your mission and what you'll learn

| You deliver | You learn |
|---|---|
| A .NET 10 solution with Aspire orchestration: API, PostgreSQL in a container, React web app, one command to run it all, a dashboard with logs and traces | **.NET Aspire**: AppHost, ServiceDefaults, resource references, parameters, `azd` deployment |
| The shared contracts in `DocAgent.Core` and a fake behind every one | **Contract-first design** with keyed services and feature flags, so seven people can work in parallel |
| A Minimal API with a background worker, file uploads, Server-Sent Events and OpenAPI | **ASP.NET Core Minimal APIs**, `Channel<T>` + `BackgroundService`, SSE streaming |
| PostgreSQL persistence with EF Core, migrations and `jsonb` columns | **EF Core + Npgsql** |
| A React + Vite + TypeScript app with Fluent UI, TanStack Query and a strategy picker | **React with Fluent UI v9**, data fetching and polling patterns |
| GitHub Actions CI, repo hygiene, instructions for AI coding agents | **Engineering workflow**: CPM, CODEOWNERS, PR templates, `AGENTS.md`, Copilot-generated tests |

This guide is written so you can **hand it to your AI coding agent one phase at a time** (Claude Code, GitHub Copilot agent mode, or Codex). The agent does the typing; you review every diff and run every gate.

---

## How to run this with your coding agent

Create an empty folder, put this guide and `00-team-overview.md` into `docs/guides/`, then prompt:

> Read `docs/guides/00-team-overview.md` and `docs/guides/elena-1-project-setup.md`. Execute **Phase 1 only**. Follow the commands exactly unless a command fails. If it fails, explain why and propose the fix before applying it. Stop at the Phase 1 gate, run the verification commands and show me the output. Do not modify anything in `src/DocAgent.Core` except to copy the contracts from the overview verbatim.

Repeat for each phase. **Don't let the agent run all phases in one go.** You want to review each layer before the next one is built on top of it.

If a library API in this guide doesn't match the version you installed, the agent must check the current package docs, not guess. Aspire, Microsoft Agent Framework and Vite all move fast. When the agent proposes something that changes a contract, stop and ask Darko.

**Copilot in your IDE**: enable it, and use it for the unit tests in Phase 2.5 ("generate tests for this fake that assert the contract shape"). Read what it generates; fix what's wrong. That is part of the Track 1 brief.

---

## Your deliverables and dates

| When | What |
|---|---|
| Wed 28 Oct | Tooling installed (below). Repo cloned once Darko creates it (Tue/Wed) |
| Fri 30 Oct | **Phases 1–2**: solution builds, contracts and fakes in, unit tests green |
| Tue 3 Nov | **Phase 3**: Aspire runs API + Postgres, migrations applied |
| Wed 4 Nov | **Phases 4–5**: upload a pack → fake run → design visible in the web app, strategy picker working, Ask panel streaming a fake answer |
| Thu 5 Nov | **Phase 6**: CI green, `AGENTS.md`, README, ADRs 0001–0002 |
| **Fri 6 Nov** | **Checkpoint "It runs"**: you demo the skeleton; **Phase 7** walkthrough; each owner runs it on their own machine and flips their own flag |
| W3 onwards | Production readiness (guide 2). You keep ownership of the skeleton: review every API/UI PR; each owner adds their own tab and endpoints |
| W7 | Deploy to Azure with Darko via `azd` |

If a phase slips, tell Darko the same day. The skeleton is what unblocks six people on 9 Nov.

---

## Prerequisites — install before Phase 1

| Tool | Version | Check |
|---|---|---|
| .NET SDK | 10.x | `dotnet --version` |
| Node.js | 22 LTS or newer | `node --version` |
| Docker Desktop (or Podman) | current | `docker info`; Aspire runs Postgres in a container |
| Aspire templates | current | `dotnet new install Aspire.ProjectTemplates` |
| Azure CLI | current | `az --version`, then `az login` |
| Git + GitHub access | — | access to the IWConnect org |
| IDE | VS 2022 17.14+ / VS 2026 / VS Code + C# Dev Kit / Rider | — |
| `dotnet-ef` | current | `dotnet tool install --global dotnet-ef` |

---

## Decisions already made

| Area | Choice |
|---|---|
| Runtime | .NET 10, C# latest, nullable enabled |
| Orchestration | Aspire (AppHost + ServiceDefaults) |
| API | ASP.NET Core Minimal APIs, built-in OpenAPI |
| Background work | `Channel<Guid>` + `BackgroundService`; no extra infrastructure |
| Streaming to the browser | Server-Sent Events (`text/event-stream`) |
| Database | PostgreSQL via Aspire (`AddAzurePostgresFlexibleServer().RunAsContainer()`: container locally, Azure Flexible Server when deployed), EF Core with Npgsql |
| Frontend | React + Vite + TypeScript, Fluent UI React v9, React Router, TanStack Query, react-markdown |
| Strategies | Keyed services (`AddKeyedSingleton`, `GetRequiredKeyedService`), fakes registered with `KeyedService.AnyKey` |
| Package versions | Central Package Management (`Directory.Packages.props`) |
| CI | GitHub Actions |
| Azure auth | `DefaultAzureCredential`; no keys in code |

---

## Phase 1 — Repository and solution skeleton

```bash
mkdir docagent && cd docagent
git init -b main
mkdir -p docs/guides docs/adr docs/design docs/experiments samples knowledge evals
# copy 00-team-overview.md and all role guides into docs/guides/

dotnet new gitignore
dotnet new editorconfig
dotnet new sln -n DocAgent            # .slnx output on .NET 10 is fine

dotnet new aspire-apphost         -n DocAgent.AppHost         -o src/DocAgent.AppHost
dotnet new aspire-servicedefaults -n DocAgent.ServiceDefaults -o src/DocAgent.ServiceDefaults
dotnet new web      -n DocAgent.Api            -o src/DocAgent.Api
dotnet new classlib -n DocAgent.Core           -o src/DocAgent.Core
dotnet new classlib -n DocAgent.Fakes          -o src/DocAgent.Fakes
dotnet new classlib -n DocAgent.Profiling      -o src/DocAgent.Profiling
dotnet new classlib -n DocAgent.Agents         -o src/DocAgent.Agents
dotnet new classlib -n DocAgent.Knowledge      -o src/DocAgent.Knowledge
dotnet new classlib -n DocAgent.Safety         -o src/DocAgent.Safety
dotnet new classlib -n DocAgent.Infrastructure -o src/DocAgent.Infrastructure
dotnet new xunit    -n DocAgent.UnitTests      -o tests/DocAgent.UnitTests
dotnet new console  -n DocAgent.Evals          -o tests/DocAgent.Evals

dotnet sln add src/*/*.csproj tests/*/*.csproj
```

If `aspire-apphost` / `aspire-servicedefaults` aren't found, run `dotnet new list aspire` and use the names shown.

**Project references.** Core references nothing; everything else points inward. Owner projects don't reference each other; they meet through Core.

```bash
for p in Fakes Profiling Agents Knowledge Safety Infrastructure; do
  dotnet add src/DocAgent.$p reference src/DocAgent.Core
done

dotnet add src/DocAgent.Api reference \
  src/DocAgent.Core src/DocAgent.Fakes src/DocAgent.Profiling src/DocAgent.Agents \
  src/DocAgent.Knowledge src/DocAgent.Safety src/DocAgent.Infrastructure src/DocAgent.ServiceDefaults

dotnet add src/DocAgent.AppHost reference src/DocAgent.Api

for t in UnitTests Evals; do
  dotnet add tests/DocAgent.$t reference \
    src/DocAgent.Core src/DocAgent.Fakes src/DocAgent.Profiling src/DocAgent.Agents \
    src/DocAgent.Knowledge src/DocAgent.Safety
done
```

**`Directory.Build.props`** at the repo root:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

Remove the `TargetFramework` line from each generated `.csproj` so the root value applies (keep the AppHost's `Sdk="Aspire.AppHost.Sdk/..."` attribute exactly as generated).

**`Directory.Packages.props`**: enable Central Package Management and move every `Version="..."` from the generated `.csproj` files into it:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- populated as packages are added; csproj files reference packages without Version -->
  </ItemGroup>
</Project>
```

Delete the template `Class1.cs` files.

### ✅ Gate 1
```bash
dotnet build
git add -A && git commit -m "chore: solution skeleton"
```
Build succeeds with zero errors.

---

## Phase 2 — Contracts and fakes

### 2.1 Contracts
Create the seven files in `src/DocAgent.Core/` (`Exports.cs`, `SystemModel.cs`, `Detection.cs`, `Designs.cs`, `Runs.cs`, `Knowledge.cs`, `Chat.cs`) with the code from `00-team-overview.md` §6.1, **verbatim**. Don't add anything else to Core. Core has no package references.

### 2.2 Fakes — `src/DocAgent.Fakes/`

Add `Microsoft.Extensions.DependencyInjection.Abstractions` to `DocAgent.Fakes` (needed for `KeyedService.AnyKey`).

| Fake | Behaviour |
|---|---|
| `FakeContentScreen` | `Allowed = true`, no flags. |
| `FakeExportProfiler` | `ProfileAsync`: one `FileSummary` per file. For each `.csv`, one `EntityInfo` from the header row (entity name = file name without extension; fields typed `"string"`; first column `IsKey = true`; `RowCount` = lines − 1; source `L1-L1`). One `Signal("file-ext", ext)` per file. Ids follow 00 §6.2. `EnrichAsync` returns the model unchanged. |
| `FakeSystemDetector` | Constructed with the key. Returns `SystemId = "unknown"`, `DisplayName = "Unknown system (fake)"`, `Confidence = 0.5`, one evidence line per file name, no alternatives, `Strategy = key`. |
| `FakeTemplateProvider` | Returns the three templates from 00 §2 with their section ids. `Guidance` = the section title, `Required = true` except `open-questions`, no tables, `KnowledgeKinds = [Standard]`. `RequiresTarget`: false, false, true. |
| `FakeDesignAuthor` | Constructed with the key. One `DesignPart` per template section: an `Assumption` statement "Placeholder for {section title} ({key})", plus an `Evidenced` statement citing the first entity id if the model has one, plus one open question. One `ReviewResult` (`Approved = true`, `Score = 0.8`, no issues, no checks). Zero usage. |
| `FakeRunPipeline` | Uses the real DI registrations of `IContentScreen`, `IExportProfiler`, keyed `ISystemDetector`, `ITemplateProvider`, keyed `IDesignAuthor` (keys from `request.Strategy`) and `IDesignRenderer`. Reports `Screening → Profiling → Detecting → Drafting → Reviewing → Rendering → Completed` with `await Task.Delay(400, ct)` between steps. Honours `SystemOverride` by replacing the detection with `Strategy = "override"`. |
| `FakeDesignPartValidator` | Returns no issues. |
| `FakeKnowledgeRetriever` | Six canned `KnowledgeChunk`s (2 `SystemProfile`, 2 `Pattern`, 1 `Standard`, 1 `Lesson`). Returns chunks whose `Content` contains any word of the query (case-insensitive), filtered by `Kinds`, ordered by match count, `Top` applied. Ignores `Mode`. |
| `FakeKnowledgeIngestor` | Stores documents in a `ConcurrentDictionary`; `DeleteAsync` removes. |
| `FakeSectionContextProvider` | Constructed with the key. `"prestep"` → two canned chunks, `OfferSearchTool = false`. `"tool"` → no chunks, `OfferSearchTool = true`. Any other key → nothing. |
| `FakeRunChat` | Streams a canned answer word by word (50 ms apart), then a final `Done` delta with one citation. |
| `FakeDocumentTextExtractor` | Returns `"# {fileName}\n\n(fake text, {n} bytes)"`. |

**Fake strategy options.** A helper `AddFakeStrategyOptions(slot, params string[] keys)` registers one `StrategyOption(slot, key, "Fake", IsFake: true)` per key. Use it for: detector `rules, rag, hybrid`; author `single-agent, specialists, concurrent`; retrieval `none, prestep, tool`. The UI then shows every planned option from day one.

### 2.3 Renderer v0 (always real)
`src/DocAgent.Agents/Rendering/MarkdownDesignRenderer.cs` implementing `IDesignRenderer`:
- `# {Title}`, then a line with the detected system, confidence and target.
- Per section (in template order): `## {Section title}`; statements as paragraphs, with `*(assumption)*` after assumptions and `**[TBD]**` before unknowns; tables as Markdown tables with the `TableSpec` columns; open questions as a list.
- A compact "Evidence" line per section listing the referenced ids.

Riste takes ownership of it in week 3.

### 2.4 Entry-point stubs — the owners' starting points

One static class per project, in namespace `Microsoft.Extensions.DependencyInjection`. Each method returns `services` unchanged for now; owners fill them in.

| Project | Methods |
|---|---|
| `DocAgent.Safety` | `AddDocAgentSafety` |
| `DocAgent.Profiling` | `AddDocAgentProfiling`, `AddDocAgentTools` |
| `DocAgent.Agents` | `AddDocAgentTemplates`, `AddDocAgentAuthors`, `AddDocAgentPipeline` |
| `DocAgent.Knowledge` | `AddDocAgentDetection`, `AddDocAgentDocumentIntelligence`, `AddDocAgentKnowledge`, `AddDocAgentChat` |

```csharp
// src/DocAgent.Agents/ServiceCollectionExtensions.cs (pattern for all of them)
namespace Microsoft.Extensions.DependencyInjection;

public static class AgentsServiceCollectionExtensions
{
    // Boban and Stevcho implement: keyed IDesignAuthor registrations + their StrategyOptions.
    public static IServiceCollection AddDocAgentAuthors(this IServiceCollection services, IConfiguration configuration)
        => services;
    // ... AddDocAgentPipeline (Boban), AddDocAgentTemplates (Riste)
}
```

Add `Microsoft.Extensions.Configuration.Abstractions` and `Microsoft.Extensions.DependencyInjection.Abstractions` where needed.

### 2.5 Unit tests
In `DocAgent.UnitTests`, one test per fake asserting the contract shape. For example: the author returns a part for every template section; the retriever honours `Top` and `Kinds`; resolving `IDesignAuthor` with any key works; the pipeline reports every status in order. Generate them with GitHub Copilot, then read and fix them. That is part of the Track 1 brief.

### ✅ Gate 2
```bash
dotnet build && dotnet test
git commit -am "feat(core): contracts and fakes"
```

---

## Phase 3 — Aspire + PostgreSQL + EF Core

### 3.1 AppHost packages
```bash
dotnet add src/DocAgent.AppHost package Aspire.Hosting.Azure.PostgreSQL
dotnet add src/DocAgent.AppHost package Aspire.Hosting.JavaScript
```

### 3.2 `src/DocAgent.AppHost/AppHost.cs` (the file may be named `Program.cs`)

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Postgres: container locally, Azure Database for PostgreSQL Flexible Server when deployed with azd.
var postgres = builder.AddAzurePostgresFlexibleServer("postgres")
                      .RunAsContainer(c => c.WithDataVolume());
var db = postgres.AddDatabase("docagentdb");

// Azure endpoints are parameters (values in AppHost user-secrets, see 3.5). Not secrets, but not committed.
var foundryEndpoint    = builder.AddParameter("foundry-endpoint");
var aiServicesEndpoint = builder.AddParameter("aiservices-endpoint");
var searchEndpoint     = builder.AddParameter("search-endpoint");

var api = builder.AddProject<Projects.DocAgent_Api>("api")
                 .WithReference(db).WaitFor(db)
                 .WithEnvironment("Foundry__Endpoint", foundryEndpoint)
                 .WithEnvironment("AiServices__Endpoint", aiServicesEndpoint)
                 .WithEnvironment("Search__Endpoint", searchEndpoint)
                 .WithExternalHttpEndpoints();

builder.AddViteApp("web", "../web")
       .WithReference(api).WaitFor(api)
       .WithExternalHttpEndpoints();

builder.Build().Run();
```

The `web` folder is created in Phase 5. Until then, comment out the `AddViteApp` block.

### 3.3 Persistence — `src/DocAgent.Infrastructure/`

```bash
dotnet add src/DocAgent.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src/DocAgent.Infrastructure package Microsoft.EntityFrameworkCore.Design
dotnet add src/DocAgent.Api package Aspire.Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src/DocAgent.Api package Microsoft.EntityFrameworkCore.Design
```

**Entities** (`Entities/`):

| Entity | Columns |
|---|---|
| `Run` | `Id (Guid PK)`, `CreatedAt`, `CompletedAt?`, `Status`, `PackName`, `TemplateId`, `Target?`, `Brief?`, `SystemOverride?`, `Detector`, `Author`, `Retrieval`, `Lessons`, `MaxReviewLoops`, `DetectedSystemId?`, `DetectionConfidence?`, `Iteration`, `ProgressMessage`, `ErrorMessage?`, `TraceId?`, `InputTokens`, `OutputTokens`, `ModelCalls`, `ToolCalls`, `EstimatedCostUsd (numeric(10,4))`, `ExperimentId?`, `SourceRunId?` |
| `RunFile` | `Id`, `RunId (FK)`, `Path`, `Kind`, `SizeBytes`, `Content (bytea)` |
| `RunResult` | `RunId (PK, FK)`, `ScreeningJson`, `ModelJson`, `DetectionJson`, `DesignJson` (all `jsonb`), `Markdown (text)` |
| `RunReview` | `Id`, `RunId (FK)`, `Iteration`, `Approved`, `Score`, `Summary`, `IssuesJson (jsonb)`, `ChecksJson (jsonb)` |
| `Feedback` | `Id`, `RunId (FK)`, `CreatedAt`, `Rating (int, -1/+1)`, `SectionId?`, `Comment?` |
| `ChatMessage` | `Id`, `RunId? (FK)`, `ConversationId`, `CreatedAt`, `Role`, `Text`, `CitationsJson (jsonb)` |

The strategy is stored as separate columns (not only JSON) so runs can be filtered and compared by strategy. `ExperimentId` is set when the eval harness creates the run; `SourceRunId` when a run is a rerun of another.

Indexes: `Run.CreatedAt` descending; `Run (TemplateId, Author, Retrieval, Detector)`. Serialize the `jsonb` columns with `System.Text.Json`, stored as strings with `HasColumnType("jsonb")`.

**`DocAgentDbContext`** with the six `DbSet`s, and a **design-time factory** so migrations can be generated without Aspire running:

```csharp
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<DocAgentDbContext>
{
    public DocAgentDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<DocAgentDbContext>()
            .UseNpgsql("Host=localhost;Database=docagentdb;Username=postgres;Password=design-time-only")
            .Options);
}
```

**`EfRunReader : IRunReader`** reads `Run` + `RunResult` + `RunReview`s and deserializes them into a `DesignResult`; `GetPackAsync` rebuilds the `ExportPack` from `RunFile`s. Register it in `AddDocAgentInfrastructure()`.

```bash
dotnet ef migrations add Initial -p src/DocAgent.Infrastructure -s src/DocAgent.Api
```

### 3.4 API wiring for the database
In `src/DocAgent.Api/Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<DocAgentDbContext>("docagentdb");
builder.Services.AddDocAgentInfrastructure();
// ...
var app = builder.Build();
app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DocAgentDbContext>().Database.MigrateAsync();
}
```

Migrating on startup is fine for this internal dev environment.

### 3.5 AppHost user-secrets (endpoints)
```bash
cd src/DocAgent.AppHost
dotnet user-secrets set "Parameters:foundry-endpoint"    "<from docs/environments.md>"
dotnet user-secrets set "Parameters:aiservices-endpoint" "<from docs/environments.md>"
dotnet user-secrets set "Parameters:search-endpoint"     "<from docs/environments.md>"
```
Until Darko publishes the values, set all three to `https://placeholder.invalid/`. Fakes don't call Azure.

### ✅ Gate 3
```bash
dotnet run --project src/DocAgent.AppHost
```
The Aspire dashboard opens. `postgres`, `docagentdb` and `api` are **Running/Healthy**. Connect to the database (the dashboard shows the connection string) and confirm the six tables exist. Commit.

---

## Phase 4 — The API

### 4.1 Service registration (`Program.cs`)

```csharp
var fakes = builder.Configuration.GetSection("Features:Fakes");
bool Fake(string name) => fakes.GetValue(name, true);
var s = builder.Services;
var cfg = builder.Configuration;

if (Fake("Safety"))   s.AddSingleton<IContentScreen, FakeContentScreen>();
else                  s.AddDocAgentSafety(cfg);

if (Fake("Profiler")) { s.AddSingleton<IExportProfiler, FakeExportProfiler>();
                        s.AddSingleton<IDesignPartValidator, FakeDesignPartValidator>(); }
else                  s.AddDocAgentProfiling(cfg);

if (Fake("Templates")) s.AddSingleton<ITemplateProvider, FakeTemplateProvider>();
else                   s.AddDocAgentTemplates(cfg);

if (Fake("Detection")) { s.AddKeyedSingleton<ISystemDetector>(KeyedService.AnyKey, (_, key) => new FakeSystemDetector((string)key!));
                         s.AddFakeStrategyOptions("detector", "rules", "rag", "hybrid"); }
else                   s.AddDocAgentDetection(cfg);

if (Fake("DocumentIntelligence")) s.AddSingleton<IDocumentTextExtractor, FakeDocumentTextExtractor>();
else                              s.AddDocAgentDocumentIntelligence(cfg);

if (Fake("Knowledge")) { s.AddSingleton<IKnowledgeRetriever, FakeKnowledgeRetriever>();
                         s.AddSingleton<IKnowledgeIngestor, FakeKnowledgeIngestor>();
                         s.AddKeyedSingleton<ISectionContextProvider>(KeyedService.AnyKey, (_, key) => new FakeSectionContextProvider((string)key!));
                         s.AddFakeStrategyOptions("retrieval", "none", "prestep", "tool"); }
else                   s.AddDocAgentKnowledge(cfg);

if (Fake("Authoring")) { s.AddKeyedSingleton<IDesignAuthor>(KeyedService.AnyKey, (_, key) => new FakeDesignAuthor((string)key!));
                         s.AddFakeStrategyOptions("author", "single-agent", "specialists", "concurrent"); }
else                   s.AddDocAgentAuthors(cfg);

if (Fake("Pipeline")) s.AddSingleton<IRunPipeline, FakeRunPipeline>();
else                  s.AddDocAgentPipeline(cfg);

if (Fake("Chat"))     s.AddSingleton<IRunChat, FakeRunChat>();
else                  s.AddDocAgentChat(cfg);

s.AddDocAgentTools(cfg);                                    // Riste: drill-down tools, always real
s.AddSingleton<IDesignRenderer, MarkdownDesignRenderer>();   // Riste may move this into AddDocAgentTemplates
s.AddSingleton(Channel.CreateUnbounded<Guid>());
s.AddHostedService<RunWorker>();
s.AddProblemDetails();
s.AddOpenApi();
```

Put the `Features:Fakes` block from 00 §6.3 into `appsettings.Development.json` with every flag `true`, and the `Strategy:Default` keys from 00 §8 into `appsettings.json`.

Keyed services must be resolved with `GetRequiredKeyedService<T>(key)`. **Check that `KeyedService.AnyKey` factory registrations resolve for arbitrary keys in the .NET version you use** (a unit test in Phase 2 covers this).

### 4.2 `RunWorker : BackgroundService`
Reads run ids from the channel. For each one:
1. Load the `Run` and its `RunFile`s; build the `ExportPack` and `DesignRequest` (strategy from the run's columns).
2. Start an `Activity` named `docagent.run` with tags `run.id`, `strategy.detector`, `strategy.author`, `strategy.retrieval`, `template.id`. Store `TraceId`. (You refine the span model yourself in your second guide.)
3. Call `IRunPipeline.RunAsync` with an `IProgress<RunProgress>` that updates `Status`, `Iteration` and `ProgressMessage` in the database.
4. On success: save `RunResult` (JSON of screening, model, detection, design, plus Markdown from `IDesignRenderer`), each `RunReview`, usage, detected system and confidence; set `Completed`.
5. On exception: `Failed` + `ErrorMessage`, and log the exception.

One run at a time is fine for now. On startup, re-enqueue runs left in `Queued`.

### 4.3 Endpoints

| Method | Route | Behaviour |
|---|---|---|
| `POST` | `/api/runs` | `multipart/form-data`: `files[]` (1–30 files, ≤10 MB each) **or** one `.zip` (≤25 MB), plus `templateId`, `target?`, `brief?`, `detector?`, `author?`, `retrieval?`, `lessons?`, `maxReviewLoops?`, `systemOverride?`. Missing strategy fields come from `Strategy:Default`. Set `FileKind` from the extension and content sniffing (`.yaml/.yml/.json` with an `openapi` key → `OpenApi`). Save `Run` (`Queued`) + `RunFile`s, write the id to the channel, return `202 { runId }` with `Location`. Reject unknown template, unknown strategy key (not in `/api/strategies`), `RequiresTarget` without a target, or oversized input with `400 ProblemDetails`. |
| `GET` | `/api/runs` | Latest 50 runs: id, createdAt, status, packName, templateId, strategy, detected system, confidence, tokens, cost. Filters: `templateId`, `author`, `retrieval`, `detector`, `experimentId`. |
| `GET` | `/api/runs/{id}` | Full run: status, progress, files (names, kinds, sizes), detection, design (parts) + Markdown, reviews, usage, traceId, error |
| `GET` | `/api/runs/{id}/model` | The `SystemModel` JSON (for the "What we found" tab and debugging) |
| `GET` | `/api/runs/{id}/design.md` | `text/markdown` download, file name from the design title |
| `GET` | `/api/runs/{id}/events` | SSE stream of `RunProgress` while the run is active (should; polling is fine until then) |
| `POST` | `/api/runs/{id}/rerun` | Body: any of the strategy fields, `templateId`, `target`, `systemOverride`. Creates a new run on the same files with `SourceRunId = id`. `202 { runId }` |
| `POST` | `/api/runs/{id}/feedback` | `{ rating, sectionId?, comment? }` → `Feedback` row. `204` |
| `POST` | `/api/ask` | `{ runId?, conversationId?, question }` → **SSE stream** of `ChatDelta` from `IRunChat`; history from `ChatMessage`s of the conversation; question and final answer saved |
| `GET` | `/api/templates` | From `ITemplateProvider` |
| `GET` | `/api/strategies` | All registered `StrategyOption`s, grouped by slot |
| `POST` | `/api/knowledge` | `multipart`: `.md`/`.txt`/`.pdf` files + `kind` + `license` + `sourceUri?` → `KnowledgeDocument`s (PDFs through `IDocumentTextExtractor`) → `IKnowledgeIngestor.IngestAsync`. `202` (Trajche owns the internals) |
| `GET` | `/api/knowledge/search?q=&kind=&mode=&top=` | Debug endpoint over `IKnowledgeRetriever` |

**Zip safety.** Unpack in memory with limits: max 200 entries, max 50 MB uncompressed in total, reject entries with `..` or absolute paths, ignore directories and `__MACOSX/`. This is untrusted input.

**SSE.** Set `Content-Type: text/event-stream`, write `data: {json}\n\n` per delta and flush. Pass `HttpContext.RequestAborted` as the cancellation token so a closed browser tab stops the model call.

Map OpenAPI in development (`app.MapOpenApi()`).

### 4.4 HTTP test file
`src/DocAgent.Api/DocAgent.Api.http` with a request for every endpoint, including a multipart upload of a pack from `samples/`.

### ✅ Gate 4
Run the AppHost. Using the `.http` file: create a run with explicit strategy keys, poll it until `Completed`, fetch the Markdown and the model, rerun it with another author, ask a question and see the streamed deltas, post feedback. All succeed. Commit.

---

## Phase 5 — The web app

### 5.1 Scaffold
```bash
cd src
npm create vite@latest web -- --template react-ts
cd web
npm install
npm install react-router-dom @tanstack/react-query @fluentui/react-components @fluentui/react-icons react-markdown remark-gfm
```
If `create-vite` prompts, accept the defaults and **don't** start the dev server. Then uncomment the `AddViteApp` block in the AppHost.

### 5.2 `vite.config.ts` — proxy `/api` to the API that Aspire injects
```ts
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Aspire injects the API address for WithReference(api). The name depends on the Aspire version.
const apiTarget =
  process.env.API_HTTPS ?? process.env.API_HTTP ??
  process.env.services__api__https__0 ?? process.env.services__api__http__0 ??
  'http://localhost:5080'

export default defineConfig({
  plugins: [react()],
  server: {
    port: process.env.PORT ? Number(process.env.PORT) : 5173,
    proxy: { '/api': { target: apiTarget, changeOrigin: true, secure: false } },
  },
})
```
Check the `web` resource's environment variables in the Aspire dashboard to confirm which name your version injects. SSE goes through the Vite proxy fine; if responses arrive in one burst, make sure the API flushes after each event.

### 5.3 Structure
```
src/web/src/
├── api/client.ts          typed fetch functions + TS types mirroring the API responses
├── api/hooks.ts           TanStack Query hooks (useRuns, useRun(id) polling while not terminal, useCreateRun,
│                          useRerun, useTemplates, useStrategies, useFeedback)
├── api/sse.ts             small helper reading a fetch() response body as SSE (POST /api/ask can't use EventSource)
├── pages/NewRunPage.tsx   drop zone (files or zip) · template cards (description; target field when RequiresTarget)
│                          · brief textarea · "Lab settings" accordion: detector / author / retrieval dropdowns from
│                          /api/strategies (fake options marked), lessons toggle, max review loops · submit → run page
├── pages/RunsPage.tsx     table: created, pack, template, author, retrieval, detector, detected system + confidence,
│                          status badge, tokens, cost; filters by template and strategy
├── pages/RunDetailPage.tsx tabs:
│     Progress     ProgressBar + message + iteration
│     Detection    system, confidence bar, evidence list, alternatives; "Not right? Pick the system" → rerun with override
│     Design       rendered Markdown; statements styled by certainty (assumption = italic, unknown = warning badge);
│                  download .md; 👍/👎 + comment per section
│     What we found  entities and fields, relationships, interfaces, schedules, findings (from /model)
│     Reviews      per iteration: score, approved, issues table (severity), checks table (passed/score)
│     Ask          chat panel streaming from POST /api/ask with this runId; citations as chips
│     Footer       usage (tokens, model calls, tool calls, cost), trace id (+ link, see backlog), "Rerun with…" button
├── pages/AskPage.tsx      the same chat panel without a run: questions over the knowledge base
├── pages/KnowledgePage.tsx upload (.md/.txt/.pdf) with kind + licence; search box with mode selector (keyword,
│                          vector, hybrid, hybrid-semantic) hitting /api/knowledge/search
├── components/ChatPanel.tsx, StrategyPicker.tsx, CertaintyBadge.tsx, Layout.tsx (nav: New run, Runs, Ask, Knowledge)
├── App.tsx                routes: / → RunsPage, /runs/new, /runs/:id, /ask, /knowledge
└── main.tsx               QueryClientProvider + BrowserRouter + FluentProvider (webLightTheme)
```
Polling: `refetchInterval: status === 'Completed' || status === 'Failed' ? false : 1500`.

### ✅ Gate 5
Run the AppHost. Open `web` from the dashboard. Create a run from a `samples/` pack with non-default lab settings, watch progress, see the fake design, open every tab, ask a question and watch it stream, rerun with another author, find both runs in the list. Commit.

---

## Phase 6 — CI, repo hygiene, agent instructions

### 6.1 `.github/workflows/ci.yml`
On `pull_request` and push to `main`:
- `actions/setup-dotnet` (10.x) → `dotnet restore` → `dotnet build --no-restore` → `dotnet test --no-build`
- `actions/setup-node` (22) in `src/web` → `npm ci` → `npm run build` → `npm run lint` (if configured)

Evals do **not** run in CI on every PR (cost and time). You add a manual `workflow_dispatch` workflow for the evals later (second guide).

### 6.2 Repo files
- **`AGENTS.md`**, for AI coding agents: what the project is; where the guides are; "never modify `src/DocAgent.Core` without telling the user"; "never commit secrets, customer data or anything that isn't synthetic or public"; build, test and run commands; code conventions; "a new strategy = a keyed implementation + a `StrategyOption`". Add **`CLAUDE.md`** containing only `See AGENTS.md.` and **`.github/copilot-instructions.md`** with the same content as `AGENTS.md`.
- **`CODEOWNERS`**: `src/DocAgent.Core/** @<darko-github-handle>`
- **`.github/pull_request_template.md`**: what/why, how tested, contract change? (y/n), new strategy? (y/n), screenshots.
- **`README.md`**: prerequisites, `dotnet run --project src/DocAgent.AppHost`, where the dashboard is, how to flip fakes, how to set user-secrets, how to pick a strategy.
- **`docs/adr/0001-record-architecture-decisions.md`** and **`0002-stack.md`** (the "Decisions already made" table above).
- **`docs/environments.md`**: placeholder; Darko fills in endpoints and deployment names.

### 6.3 Ask Darko to enable
Branch protection on `main`: PR required, 1 approval, CI must pass.

### ✅ Gate 6
Open a PR with any small change. CI runs green. Merge.

---

## Phase 7 — Hand-over to the team (before the 6 Nov checkpoint)

Run a 20-minute walkthrough:
1. Clone, set user-secrets, `dotnet run --project src/DocAgent.AppHost`
2. Where each owner's entry point is (00 §6.3) and how to flip their flag
3. How keyed strategies work: register, add a `StrategyOption`, see it appear in the picker
4. The dashboard: logs, traces, resources
5. The contract change rule and CODEOWNERS

Every owner must be able to run the solution on their own machine before the checkpoint. That is the real gate.

---

## From week 3 — what you keep, what others add

You move to `elena-2-observability-evals-safety.md`. The skeleton stays yours: you are the reviewer for `src/DocAgent.Api`, `src/DocAgent.Infrastructure` and `src/web`. Each owner adds their own slice, following the patterns you set in Phases 4–5:

| Slice | Owner | What they add |
|---|---|---|
| Detection tab: evidence, alternatives, override → rerun | Trajche | Reads `/api/runs/{id}` detection; posts `/rerun` with `systemOverride` |
| Reviews tab: issues by severity, checks table, per iteration | Stevcho (issues), Riste (checks) | Reads reviews from `/api/runs/{id}` |
| Ask panel on real streaming, citations that open the referenced entity or chunk | Todor | Uses your `sse.ts` helper and `ChatPanel.tsx` |
| Knowledge page on real ingestion and search modes | Trajche, Todor | `/api/knowledge` internals |
| "What we found" tab | Riste | Reads `/api/runs/{id}/model` |
| Usage, cost, trace link on run detail and list | You (guide 2) | `TraceId` link to the Aspire dashboard locally, App Insights in the cloud |
| Compare view (should): two runs on the same pack side by side | You, W6 | Design sections, checks, usage |
| SSE `/api/runs/{id}/events` replacing polling (should) | You, when time allows | — |
| .docx download (should) | Riste | — |
| Lessons list with a disable toggle (should) | Todor | — |
| Confirm-detection step when confidence is low (should) | Boban | The pipeline pauses; the UI shows a confirm dialog |
| Deploy with `azd` | You + Darko, W7 | `azd init` detects the AppHost → `azd up` |
| Entra ID sign-in (later) | Darko | MSAL React + JWT bearer on the API |

**Review checklist for UI/API PRs:** follows the folder structure; typed client functions in `api/client.ts`; TanStack Query hooks, no ad-hoc `fetch` in components; Fluent components, no custom CSS frameworks; no new endpoint without an entry in the `.http` file; no contract change without Darko.

---

## If you have time left

- A **dark theme** toggle (Fluent `webDarkTheme`) and a nicer run list with status chips.
- **Run events over SSE** instead of polling (above).
- **Pack preview** on the New run page: file list with detected kinds and sizes before submitting.
- A `scripts/dev-reset.ps1` that drops the database volume and re-runs migrations.

---

## Pitfalls

- **Docker not running** → Aspire fails to start Postgres. Start Docker first.
- **Package versions in `.csproj` with CPM on** → restore errors. All versions live in `Directory.Packages.props`.
- **Port clashes** → let Aspire assign ports; don't hardcode.
- **HTTPS dev cert** → run `dotnet dev-certs https --trust` once per machine.
- **Proxy env var name** → check the `web` resource env in the dashboard; adjust `vite.config.ts`.
- **SSE buffered** → flush after every event; don't enable response compression on the SSE routes.
- **Zip bombs and path tricks** → enforce the limits in 4.3.
- **Agents "improving" Core** → reject any diff touching `src/DocAgent.Core` unless it's an agreed contract change.
- **Committing data** → only synthetic packs from `samples/` and public or own-written knowledge. Nothing from any customer.
