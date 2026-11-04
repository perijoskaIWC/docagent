# Track 1 — DocAgent: Team Overview

**Read this first, then your own guide** (`plan/<your name>-….md`). Everything shared lives here: what we're building and why, the architecture, the experiments, who owns what, the contracts that let us work in parallel, the timeline and the rules. Updated **Wed 28 Oct 2026**, after the team meeting that assigned the areas.

> **DocAgent** is a working name. If we want a better one, rename it **before the first code commit** (Phase 1 of the setup guide). Renaming later is painful.

---

## 1. What we're building, and why

**The product.** A web app where you upload an **export pack** from a business system: data extracts, schemas, API specs, configuration files, documentation. You choose the kind of design you want. The AI works out **which system the export came from**, then writes a **solution design** in which every statement is backed by evidence from the export or the knowledge base, and every unknown is flagged as an open question. Afterwards you can **ask questions** about the run in a streamed chat.

**The real goal is learning.** This is a lab, not a product launch. One realistic use case is our test bench for building, comparing and understanding the main ways to build AI solutions on the Microsoft stack:

- **Agent architectures** on Microsoft Agent Framework: one agent with tools, specialists with a reviewer, parallel specialists, MAF's built-in orchestrations
- **RAG approaches**: no retrieval, retrieval before writing, retrieval as a tool, agentic retrieval; keyword vs vector vs hybrid search; different chunking
- **Detection**: rules, retrieval + LLM, or both
- **Tools over MCP, evaluation, content safety and telemetry** as first-class parts, not afterthoughts

Every major step has **two or more interchangeable implementations** behind one interface. The evaluation harness runs them on the same inputs and tells us, with numbers, which works better and at what cost. What we learn goes into experiment notes and a reusable pattern catalog. That knowledge is the lasting output, more than the app itself.

| By 11 Dec we have | |
|---|---|
| **A working app** | Upload → detect → design → review → ask; deployed on Azure; traced end to end |
| **Measured comparisons** | At least five experiments (§4) with reports: detection, agent architecture, retrieval mode, search configuration, tools over MCP |
| **Reusable knowledge** | Experiment notes, an architecture document and a pattern catalog the practice can reuse on real engagements |

**What it is not:** production-hardened, multi-tenant, or tied to any customer, product or engagement. Everything in it is **synthetic or public** (§9).

---

## 2. The product in one picture

```
 1. Upload            2. Detect                   3. Choose               4. Generate                     5. Use
 ─────────────        ─────────────────           ─────────────           ─────────────────────           ─────────────────
 export pack     →    "Helio Service, 0.92,  →    design type        →    evidence-backed design     →    read, download,
 (zip or files)       because: OpenAPI title,     + target + brief        drafted, validated and          ask questions,
                      webhook config, ..."        + strategy (lab)        reviewed in a bounded loop      rerun with another
                      (user can override)                                                                 strategy, compare
```

### Design types — the user picks one per run

| Template id | Purpose | Target | Sections |
|---|---|---|---|
| `system-assessment` | Describe the system as it is: data, interfaces, rules, data quality, risks | None | summary · system · data-model · interfaces · rules · data-quality · risks · open-questions |
| `integration-design` | Design how to integrate the system with others on Azure | Optional (default: Azure Integration Services) | context · source · patterns · flows · mapping · security · operations · open-questions |
| `migration-design` | Design how to move the system's data and processes to a target platform | Required (e.g. Dataverse, Azure SQL) | context · inventory · mapping · transformations · approach · reconciliation · risks · open-questions |

Riste owns the template files (Darko reviews the guidance text): each section's guidance, table columns and knowledge kinds. Order of work: `system-assessment` first (no target, easiest to check), then `integration-design`, then `migration-design` (should).

### Test systems — what the detector must recognise

| System id | System | Type | Its export packs contain | Why it's in the set |
|---|---|---|---|---|
| `quarry-wms` | **Quarry WMS** (fictional) | Warehouse and inventory | CSV extracts, an XSD and sample XML for outbound shipments, a job-schedule JSON, a short PDF admin guide | File-based; relationships must be inferred; the PDF needs Document Intelligence |
| `helio-service` | **Helio Service** (fictional) | Field service and ticketing SaaS | OpenAPI 3 spec (YAML), NDJSON data dumps, webhook configuration, an Excel KPI report | API-first; interfaces come from the spec |
| `dataverse` | **Microsoft Dataverse / Dynamics 365** (real, publicly documented) | Business apps platform | Solution export (`solution.xml`, `customizations.xml`), OData `$metadata`, Excel data export | A real product; its profile is written from public Microsoft Learn documentation |
| `unknown` | — | Negative cases | Exports from systems that have no profile | The detector must say "unknown", and the design must still work |

Each system gets four pack variants: **full, data-only, metadata-only, noisy** (renamed files, irrelevant extras). One extra **injection** pack hides an instruction in a file, to prove Content Safety screening blocks it. The knowledge base also holds two **decoy profiles**, fictional systems that resemble the test systems, to catch false positives. Darko owns `samples/`. The packs are generated from a written spec, so **we know the ground truth for every pack**. That is what makes automated evaluation cheap (Elena's harness).

The fictional names are placeholders. Rename them freely, but check that no real product uses the name.

---

## 3. Architecture

```
                        ┌──────────────────────────────────────────────┐
                        │          Aspire AppHost (local)              │
                        │    orchestrates everything + dashboard       │
                        └──────────────────────────────────────────────┘

 ┌───────────────┐  /api    ┌────────────────────────────────────────────────────────────┐
 │ React (Vite)  │ ───────► │ DocAgent.Api (.NET 10 Minimal API)                         │
 │ Fluent UI v9  │ ◄── SSE  │  runs · templates · strategies · knowledge · ask (stream)  │
 └───────────────┘          │  RunWorker (Channel + BackgroundService)                   │
                            │  /mcp  ◄── MCP clients (our agents, Copilot in VS Code)    │
                            └─────────────────────────────┬──────────────────────────────┘
                                                          │ IRunPipeline
 ┌────────────────────────────────────────────────────────▼─────────────────────────────────────┐
 │ Run pipeline: a MAF Workflow (DocAgent.Agents)                                               │
 │                                                                                              │
 │  Screen ──► Profile ──► Detect ──────► Enrich ──► Author ─────────────────────────► Render   │
 │  (Content   (code)      [rules | rag   (code,     [single-agent | specialists |      (code)   │
 │   Safety)                | hybrid]     per        concurrent | group-chat | magentic]         │
 │                                        system)       │  draft parts ⇄ validate ⇄ review      │
 │                                                      │  tools: drill-down (MCP or in-proc)   │
 │                                                      │  knowledge: [none | prestep | tool |  │
 │                                                      │              agentic]                 │
 └──────────────────────────────────────────────────────┼───────────────────────────────────────┘
                                                        │
   DocAgent.Profiling: export → SystemModel,            │      DocAgent.Knowledge: index, detection,
   tools, validators, checks, renderers                 │      retrieval, Ask chat, lessons
                                                        ▼                       │
                                     Microsoft Foundry models        Azure AI Search (hybrid + semantic)
                                     chat-main · chat-mini · embed   Document Intelligence · Content Safety

 PostgreSQL ◄── DocAgent.Infrastructure (EF Core)
 OpenTelemetry ──► Aspire dashboard (local) · App Insights + Foundry tracing (cloud) · Langfuse (optional, ADR)
```

Square brackets mark **experiment slots**: interchangeable implementations, chosen per run.

### The pipeline, step by step

| Step | What happens | AI? | Owner |
|---|---|---|---|
| **Screen** | Uploaded text is checked with Content Safety: Prompt Shields for instructions hidden in documents, plus harmful content | Azure service | Elena |
| **Profile** | Every file is parsed into one neutral `SystemModel`: entities, fields, keys, relationships, interfaces, schedules, data-quality findings and detection *signals*. Each item records its exact source location | No | Riste |
| **Detect** | Which system is this? An answer with confidence, evidence and alternatives, or "unknown" | Depends on the strategy | Trajche |
| **Enrich** | Optional system-specific extraction once the system is known (e.g. reading Dataverse metadata properly) | No | Riste |
| **Author** | Agents draft each template section as **structured parts**, using drill-down tools and knowledge. Parts are validated by code and reviewed in a bounded loop | Yes | Boban, Stevcho |
| **Render** | Parts → Markdown (and .docx, should). Deterministic | No | Riste |
| **Ask** | After a run: a streamed chat that answers from the run's model, its design and the knowledge base, with citations | Yes | Todor |

### Design principles (decided — change only through an ADR)

1. **Deterministic first.** Anything code can extract, such as names, types, counts, keys and locations, is extracted by code. Agents interpret and design; they don't count columns.
2. **Agents read through tools.** Agents get an overview plus drill-down tools, not every file pasted into a prompt.
3. **Structured output, rendered by code.** Agents submit `DesignPart`s (statements, tables, open questions) that are validated against the section spec. A renderer produces the text, so formatting never depends on the model.
4. **Evidence, or say so.** Every statement is *Evidenced* (cites model ids or knowledge chunk ids), an *Assumption*, or *Unknown* with an open question. No invented fields, systems or numbers.
5. **Checks prove nothing was lost.** Deterministic checks (entity coverage, evidence that resolves, required sections) run on every design, alongside the LLM reviewer.
6. **Every slot is swappable and measured.** A new strategy is a new keyed implementation plus an eval run, never a rewrite.
7. **The Microsoft stack, done properly.** MAF agents over Foundry deployments referenced by alias (`chat-main`, `chat-mini`, `embed`); `DefaultAzureCredential` everywhere, no keys in code; MCP over **Streamable HTTP**.

---

## 4. Experiments

Each experiment has one owner, a hypothesis written **before** it runs, and a report produced by Elena's harness. The result is written up as an experiment note in `docs/experiments/` (template in Elena's second guide). The notes feed the pattern catalog.

| # | Question | Variants | Main metrics | Owner | Results by |
|---|---|---|---|---|---|
| **E1** | How should we detect the source system? | `rules` · `rag` · `hybrid` | accuracy, "unknown" precision/recall, confidence calibration, cost | Trajche | 4 Dec |
| **E2** | Which agent architecture writes the best design? | `single-agent` · `specialists` (+ reviewer loop) · `concurrent` · `group-chat` (should) · `magentic` (stretch) | fact recall, unsupported claims, check pass rate, **run-to-run variance**, tokens, latency | Stevcho, with Boban | 4 Dec |
| **E3** | When should agents get knowledge? | `none` · `prestep` · `tool` · `agentic` (stretch) | groundedness, recall of knowledge-dependent facts, tokens | Todor | 4 Dec |
| **E4** | How should we chunk and search? | heading-based vs fixed-size chunks × keyword / vector / hybrid / hybrid + semantic ranker | hit@5, MRR on the retrieval test set | Trajche (chunking), Todor (query) | 27 Nov |
| **E5** | Where do we need the big model? | `chat-main` everywhere · `chat-mini` for specialists · a different reviewer model | quality vs cost | Elena, with Stevcho | 11 Dec (should) |
| **E6** | Does memory help? | lessons off vs on | recurrence of the same review issues, fact recall | Todor | 11 Dec (should) |
| **E7** | Do tools over MCP cost us anything? | in-process tools vs the same tools over MCP | latency, failures, tokens | Riste | 4 Dec |

**Variance matters as much as averages.** The same input, run five times, should give similar designs. A strategy that is slightly better on average but much less stable is not better. The harness measures both.

---

## 5. Who does what

Seven people. Each area has one owner for the whole period, and each owner has a guide of their own in `plan/`. You can help others; you can't hand off your area.

| Area | Who | Owns | Guide | Delivers by 11 Dec |
|---|---|---|---|---|
| **Project setup** | **Elena**, with Darko | Repo, solution, Aspire, API, database, React app, CI; the skeleton everyone builds on | `elena-1-project-setup.md` | Skeleton running on fakes by **6 Nov**; then reviews every UI/API change |
| **Agent Framework: pipeline and the baseline author** | **Boban** | `DocAgent.Agents`: the run pipeline as a MAF workflow, the shared agent building blocks, the `single-agent` author, tool wiring (in-process and MCP), human-in-the-loop pause | `boban-agent-framework-pipeline.md` | Pipeline on Foundry; `single-agent` author; progress and usage |
| **Agent Framework: multi-agent orchestrations** | **Stevcho** | The Reviewer agent and the bounded review loop, the `specialists`, `concurrent` and `group-chat` authors, all prompts, experiment E2 | `stevcho-agent-framework-orchestrations.md` | Three more authors; E2 report |
| **Tools and MCP** | **Riste** | `DocAgent.Profiling`: file extractors and the profiler, drill-down tools, the MCP server, templates, validators and checks, renderers; E7 | `riste-tools-and-mcp.md` | Profiler for every pack format; tools over MCP; templates; checks |
| **RAG: knowledge and detection** | **Trajche** | Knowledge corpus, ingestion, index schema, chunking, Document Intelligence, the system detectors; E1, E4 (chunking) | `trajche-knowledge-and-detection.md` | Populated index; three detectors; E1 report |
| **RAG: retrieval, Ask chat and memory** | **Todor** | Retriever and query modes, retrieval strategies for authoring, the streamed Ask chat, lessons; E3, E4 (query), E6 | `todor-retrieval-ask-memory.md` | Retrieval modes; Ask chat; E3 report |
| **Production readiness: observability, evaluation and safety** | **Elena** (from W3) | Telemetry, cost, tracing ADR, Content Safety screening, eval harness, experiment runner, reports; E5 | `elena-2-observability-evals-safety.md` | End-to-end traces; harness + baseline; experiment reports |
| **Lead** | **Darko** | Azure and access, the contracts (`DocAgent.Core`), test-system specs and synthetic export packs, template guidance, seed eval cases, ADRs, the pattern catalog, unblocking | `darko-azure-and-lead.md` | Environment, samples, reviews, pattern catalog |

**Two phases.** Darko and Elena build the foundation first (Azure, repo, skeleton, contracts, fakes), done by **Fri 6 Nov**. Everyone else uses that time for a **research brief** on their area (§13) and starts building on the skeleton from **Mon 9 Nov**. Elena then moves to production readiness.

**Pairs share a design.** Boban and Stevcho write one design note for the agents; Trajche and Todor write one for knowledge and retrieval. Each pair agrees its internal split in the note (the guides propose one).

**UI and API slices.** Elena builds the skeleton and the shared pages. From W3, each owner adds the endpoints and the UI tab for their own area (detection view, reviews tab, Ask panel, knowledge page), following the patterns in the skeleton. Elena reviews those PRs.

---

## 6. Contracts — how we work independently

The usual failure for a part-time team is that everyone builds alone and nothing integrates until December. Two things prevent it:

1. **Shared contracts in `DocAgent.Core`**: plain C# records and interfaces with no package dependencies.
2. **Fakes for every contract** in `DocAgent.Fakes`, switched on and off per component by config.

One more thing is specific to a lab: **strategies are keyed services.** Each experiment slot (detector, author, retrieval mode) is an interface registered several times under different keys, and the run's `RunStrategy` picks the key.

### 6.1 The contracts

Copy these **verbatim** into `src/DocAgent.Core`. They compile as they are on .NET 10. Darko owns this project (§6.4).

```csharp
// ───────────── DocAgent.Core/Exports.cs ─────────────
namespace DocAgent.Core;

public enum FileKind { Unknown, Csv, Excel, Json, Xml, Xsd, OpenApi, Yaml, Sql, Pdf, Markdown, Text }

/// <summary>One file from an uploaded export pack. Text formats fill Text; binary formats (Excel, PDF) fill Bytes.</summary>
public sealed record ExportFile(string Path, FileKind Kind, long SizeBytes, string? Text, byte[]? Bytes);

/// <summary>Everything a user uploaded for one run: an export from one source system.</summary>
public sealed record ExportPack(Guid PackId, string Name, IReadOnlyList<ExportFile> Files);

/// <summary>
/// Exactly where something came from. The locator format depends on the file kind:
/// "L12-L40" (text lines) · "Orders!A1:F1" (Excel range) · "/paths/~1orders/get" (JSON pointer) ·
/// "/xs:schema/xs:element[@name='Order']" (XPath) · "p3" (PDF page).
/// </summary>
public sealed record SourceRef(string Path, string Locator);
```

```csharp
// ───────────── DocAgent.Core/SystemModel.cs ─────────────
namespace DocAgent.Core;

// Every Id is stable and content-derived (see §6.2), e.g. "entity:work_orders", "field:work_orders.technician_id".

public sealed record FieldInfo(
    string Id,
    string Name,
    string? DataType,                      // as declared, or inferred from values ("int", "date", "guid", ...)
    bool IsKey,
    bool? IsRequired,                      // null = unknown
    IReadOnlyList<string> SampleValues,    // at most 5; synthetic data only
    SourceRef Source);

public sealed record EntityInfo(
    string Id, string Name, IReadOnlyList<FieldInfo> Fields, long? RowCount, SourceRef Source);

public sealed record RelationshipInfo(
    string Id,
    string FromFieldId,
    string ToFieldId,
    string Basis,                          // "declared" (schema/metadata says so) or "inferred" (names, values)
    SourceRef Source);

public enum InterfaceKind { RestOperation, FileFeed, Webhook, Message, DatabaseView, Other }

public sealed record InterfaceInfo(
    string Id, string Name, InterfaceKind Kind, string? Direction, string? Detail, SourceRef Source);

public sealed record ScheduleInfo(string Id, string Name, string Expression, SourceRef Source);

public enum FindingLevel { Info, Warning, Error }

/// <summary>Something the profiler noticed: a parse problem or a data-quality fact (null rate, duplicates, odd formats).</summary>
public sealed record Finding(string Id, FindingLevel Level, string Message, SourceRef? Source);

/// <summary>A clue for detection, e.g. ("xml-root", "ImportExportXml"), ("column-prefix", "qw_"), ("id-format", "guid").</summary>
public sealed record Signal(string Name, string Value, SourceRef Source);

public sealed record FileSummary(string Path, FileKind Kind, long SizeBytes, string? Note);

/// <summary>Deterministic, code-extracted model of an export pack. No LLM involved: same input, same model.</summary>
public sealed record SystemModel(
    Guid PackId,
    IReadOnlyList<FileSummary> Files,
    IReadOnlyList<EntityInfo> Entities,
    IReadOnlyList<RelationshipInfo> Relationships,
    IReadOnlyList<InterfaceInfo> Interfaces,
    IReadOnlyList<ScheduleInfo> Schedules,
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<Signal> Signals);

public interface IExportProfiler
{
    /// <summary>Generic profiling: works for any pack, knows nothing about specific systems.</summary>
    Task<SystemModel> ProfileAsync(ExportPack pack, CancellationToken ct = default);

    /// <summary>System-specific extraction once detection knows the system. Returns the model unchanged if there is no enricher.</summary>
    Task<SystemModel> EnrichAsync(SystemModel model, ExportPack pack, SystemDetection detection, CancellationToken ct = default);
}

/// <summary>Turns PDFs and scans into Markdown (Document Intelligence). Used by the profiler and by knowledge ingestion.</summary>
public interface IDocumentTextExtractor
{
    Task<string> ToMarkdownAsync(string fileName, byte[] content, CancellationToken ct = default);
}
```

```csharp
// ───────────── DocAgent.Core/Detection.cs ─────────────
namespace DocAgent.Core;

public sealed record DetectionCandidate(string SystemId, string DisplayName, double Confidence);

public sealed record SystemDetection(
    string SystemId,                              // "unknown" when no candidate is confident enough
    string DisplayName,
    double Confidence,                            // 0..1
    IReadOnlyList<string> Evidence,               // human-readable, each naming its source:
                                                  // "root element ImportExportXml (customizations.xml L1)"
    IReadOnlyList<DetectionCandidate> Alternatives,
    string Strategy);                             // which detector produced it: "rules", "rag", "hybrid", "override"

/// <summary>Experiment slot E1. Registered as keyed services: "rules", "rag", "hybrid".</summary>
public interface ISystemDetector
{
    Task<SystemDetection> DetectAsync(SystemModel model, CancellationToken ct = default);
}
```

```csharp
// ───────────── DocAgent.Core/Designs.cs ─────────────
namespace DocAgent.Core;

// ── Templates: what a design type asks for ──

public sealed record TableSpec(string Id, string Title, IReadOnlyList<string> Columns);

public sealed record SectionSpec(
    string Id,
    string Title,
    string Guidance,                                  // what the author must produce; the reviewer checks against it
    bool Required,
    IReadOnlyList<TableSpec> Tables,
    IReadOnlyList<KnowledgeKind> KnowledgeKinds);     // what retrieval looks for when this section is drafted

public sealed record DesignTemplate(
    string Id,
    string Name,
    string Description,
    bool RequiresTarget,                              // e.g. migration needs a target platform
    IReadOnlyList<SectionSpec> Sections);

public interface ITemplateProvider
{
    Task<IReadOnlyList<DesignTemplate>> ListAsync(CancellationToken ct = default);
    Task<DesignTemplate> GetAsync(string templateId, CancellationToken ct = default);
}

// ── Designs: what the agents produce — structured parts, never free-form documents ──

public enum Certainty { Evidenced, Assumption, Unknown }

public enum EvidenceKind { Model, Knowledge }

/// <summary>Model: a SystemModel id ("field:work_orders.technician_id"). Knowledge: a KnowledgeChunk id.</summary>
public sealed record EvidenceRef(EvidenceKind Kind, string Ref);

public sealed record DesignStatement(string Id, string Text, Certainty Certainty, IReadOnlyList<EvidenceRef> Evidence);

/// <summary>Rows follow the columns of the TableSpec with the same Id.</summary>
public sealed record DesignTable(string TableId, IReadOnlyList<IReadOnlyList<string>> Rows, IReadOnlyList<EvidenceRef> Evidence);

public sealed record OpenQuestion(string Id, string Question, string WhyItMatters);

/// <summary>One section of a design. Text is produced later by a renderer, in code.</summary>
public sealed record DesignPart(
    string SectionId,
    IReadOnlyList<DesignStatement> Statements,
    IReadOnlyList<DesignTable> Tables,
    IReadOnlyList<OpenQuestion> OpenQuestions);

public sealed record SolutionDesign(
    string Title,
    string TemplateId,
    string? Target,
    SystemDetection System,
    IReadOnlyList<DesignPart> Parts);

public interface IDesignRenderer
{
    string ToMarkdown(SolutionDesign design, DesignTemplate template);
}

// ── Quality gates: deterministic validation and checks, plus the LLM review ──

public sealed record ValidationIssue(string SectionId, string Code, string Message);

public sealed record ValidationContext(SystemModel Model, IReadOnlySet<string> KnowledgeChunkIds);

/// <summary>Deterministic gate for one part: table columns, evidence that resolves, unknowns flagged.</summary>
public interface IDesignPartValidator
{
    IReadOnlyList<ValidationIssue> Validate(DesignPart part, SectionSpec spec, ValidationContext context);
}

public sealed record CheckResult(string CheckId, bool Passed, double Score, string Detail);

/// <summary>Deterministic whole-design check, e.g. "every entity in the model is covered". Several are registered.</summary>
public interface IDesignCheck
{
    string Id { get; }
    CheckResult Run(SolutionDesign design, SystemModel model);
}

public enum IssueSeverity { Info, Minor, Major, Critical }

public sealed record ReviewIssue(string SectionId, IssueSeverity Severity, string Problem, string SuggestedFix);

public sealed record ReviewResult(
    int Iteration,
    bool Approved,
    double Score,                                     // 0..1
    IReadOnlyList<ReviewIssue> Issues,
    IReadOnlyList<CheckResult> Checks,
    string Summary);
```

```csharp
// ───────────── DocAgent.Core/Runs.cs ─────────────
namespace DocAgent.Core;

public enum RunStatus { Queued, Screening, Profiling, Detecting, Drafting, Reviewing, Rendering, Completed, Failed }

/// <summary>
/// Which implementation fills each experiment slot. Values are keyed-service names:
/// Detector  "rules" | "rag" | "hybrid"
/// Author    "single-agent" | "specialists" | "concurrent" | "group-chat" | "magentic"
/// Retrieval "none" | "prestep" | "tool" | "agentic"
/// </summary>
public sealed record RunStrategy(
    string Detector = "rules",
    string Author = "single-agent",
    string Retrieval = "tool",
    bool Lessons = false,
    int MaxReviewLoops = 2);

/// <summary>One implemented strategy, registered as a singleton by its owner. GET /api/strategies lists them.</summary>
public sealed record StrategyOption(string Slot, string Key, string Description, bool IsFake);

public sealed record DesignRequest(
    Guid RunId,
    ExportPack Pack,
    string TemplateId,
    string? Target,                    // target platform or system, when the template needs one
    string? Brief,                     // the user's goal, in their own words
    RunStrategy Strategy,
    string? SystemOverride = null);    // the user corrects the detected system

public sealed record RunProgress(Guid RunId, RunStatus Status, string Message, int Iteration = 0);

public sealed record RunUsage(long InputTokens, long OutputTokens, int ModelCalls, int ToolCalls, decimal EstimatedCostUsd);

public sealed record ScreeningResult(bool Allowed, IReadOnlyList<string> Flags);

public sealed record DesignResult(
    Guid RunId,
    RunStrategy Strategy,
    ScreeningResult Screening,
    SystemModel Model,
    SystemDetection Detection,
    SolutionDesign Design,
    IReadOnlyList<ReviewResult> Reviews,
    RunUsage Usage);

/// <summary>The whole run: screen → profile → detect → enrich → author → render. One implementation (Boban).</summary>
public interface IRunPipeline
{
    Task<DesignResult> RunAsync(DesignRequest request, IProgress<RunProgress>? progress = null, CancellationToken ct = default);
}

public sealed record AuthoringContext(
    DesignRequest Request,
    DesignTemplate Template,
    SystemModel Model,
    SystemDetection Detection);

public sealed record AuthoringResult(SolutionDesign Design, IReadOnlyList<ReviewResult> Reviews, RunUsage Usage);

/// <summary>Experiment slot E2: the agent architecture. Keyed services: "single-agent", "specialists", "concurrent", ...</summary>
public interface IDesignAuthor
{
    Task<AuthoringResult> AuthorAsync(AuthoringContext context, IProgress<RunProgress>? progress = null, CancellationToken ct = default);
}

/// <summary>Content Safety: screens uploaded text for hidden instructions and harmful content before any agent reads it.</summary>
public interface IContentScreen
{
    Task<ScreeningResult> ScreenAsync(ExportPack pack, CancellationToken ct = default);
}

/// <summary>Read access to finished runs, for the drill-down tools, the MCP server and the Ask chat.</summary>
public interface IRunReader
{
    Task<DesignResult?> GetAsync(Guid runId, CancellationToken ct = default);
    Task<ExportPack?> GetPackAsync(Guid runId, CancellationToken ct = default);
}
```

```csharp
// ───────────── DocAgent.Core/Knowledge.cs ─────────────
namespace DocAgent.Core;

public enum KnowledgeKind { SystemProfile, Pattern, Standard, Exemplar, Glossary, Lesson }

public sealed record KnowledgeDocument(
    string Id,
    KnowledgeKind Kind,
    string Title,
    string Content,                                   // Markdown
    string SourceUri,                                 // public URL or repo path
    string License,                                   // "internal", "CC-BY-4.0", ...
    IReadOnlyDictionary<string, string> Tags);        // systemId, templateId, sectionId, ...

public sealed record KnowledgeQuery(
    string Text,
    IReadOnlyList<KnowledgeKind>? Kinds = null,
    IReadOnlyDictionary<string, string>? Filters = null,
    int Top = 5,
    string Mode = "hybrid-semantic");                 // "keyword" | "vector" | "hybrid" | "hybrid-semantic" (E4)

public sealed record KnowledgeChunk(
    string ChunkId,
    string DocumentId,
    KnowledgeKind Kind,
    string Title,
    string Content,
    string SourceUri,
    double Score,
    IReadOnlyDictionary<string, string> Tags);

public interface IKnowledgeRetriever
{
    Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(KnowledgeQuery query, CancellationToken ct = default);
}

public interface IKnowledgeIngestor
{
    Task IngestAsync(IReadOnlyList<KnowledgeDocument> documents, CancellationToken ct = default);
    Task DeleteAsync(string documentId, CancellationToken ct = default);
}

public sealed record SectionContextRequest(AuthoringContext Authoring, SectionSpec Section, bool IncludeLessons);

/// <summary>What an author gets for one section. "prestep" fills Preloaded; "tool" sets OfferSearchTool; "none" gives neither.</summary>
public sealed record SectionContext(IReadOnlyList<KnowledgeChunk> Preloaded, bool OfferSearchTool);

/// <summary>Experiment slot E3: when agents get knowledge. Keyed services: "none", "prestep", "tool", "agentic".</summary>
public interface ISectionContextProvider
{
    Task<SectionContext> GetAsync(SectionContextRequest request, CancellationToken ct = default);
}

public sealed record Lesson(
    string Id,
    string? SystemId,            // null = applies to any system
    string TemplateId,
    string SectionId,
    string Rule,                 // general, imperative, checkable
    string Evidence,             // what went wrong that taught us this
    Guid SourceRunId,
    int Count,                   // how many times it was learned again (dedupe bumps this)
    DateTimeOffset CreatedAt);
```

```csharp
// ───────────── DocAgent.Core/Chat.cs ─────────────
namespace DocAgent.Core;

public sealed record ChatTurn(string Role, string Text);                 // "user" | "assistant"

public sealed record ChatCitation(EvidenceKind Kind, string Ref, string Label);

/// <summary>A streamed piece of an answer. Text arrives in deltas; citations arrive with the final delta (Done = true).</summary>
public sealed record ChatDelta(string? Text, IReadOnlyList<ChatCitation>? Citations, bool Done);

public interface IRunChat
{
    /// <summary>runId = null asks the knowledge base only; otherwise the run's model and design are in scope too.</summary>
    IAsyncEnumerable<ChatDelta> AskAsync(Guid? runId, string question, IReadOnlyList<ChatTurn> history, CancellationToken ct = default);
}
```

### 6.2 Ids are stable and content-derived

| Thing | Id format | Example |
|---|---|---|
| Entity | `entity:<name>` | `entity:work_orders` |
| Field | `field:<entity>.<field>` | `field:work_orders.technician_id` |
| Relationship | `rel:<from field>-><to field>` | `rel:work_orders.technician_id->technicians.id` |
| Interface | `iface:<METHOD> <path>` or `iface:<name>` | `iface:GET /work-orders` |
| Schedule | `schedule:<name>` | `schedule:nightly_stock_export` |
| Finding | `finding:<scope>.<code>` | `finding:items.sku.duplicates` |

Names are lower-cased, with spaces and dashes turned into `_` (paths in interface ids stay as they are). The same input always gives the same ids, so tools, evidence, evals and lessons can refer to things across runs.

### 6.3 Fakes, switches and strategy keys

```json
// appsettings.Development.json
"Features": {
  "Fakes": {
    "Safety": true, "Profiler": true, "Detection": true, "Templates": true, "Authoring": true,
    "Pipeline": true, "Knowledge": true, "Chat": true, "DocumentIntelligence": true
  }
}
```

- **One flag = one entry point = one owner.** When the flag is `true` the API registers the fake; when it's `false` it calls the owner's entry point.
- **Fakes for keyed slots are registered for any key** (`KeyedService.AnyKey`), so the UI and the harness work before any real strategy exists.
- **Every real strategy registers itself and a `StrategyOption`** that describes it. `GET /api/strategies` lists them, so the UI and the harness only offer what is implemented.
- Flip your flag to `false` in your **own local config first**, prove it works end to end, then change the shared default in a PR.

| Flag | Entry point | Project | Registers | Owner |
|---|---|---|---|---|
| `Safety` | `AddDocAgentSafety` | `DocAgent.Safety` | `IContentScreen` | Elena |
| `Profiler` | `AddDocAgentProfiling` | `DocAgent.Profiling` | `IExportProfiler`, `IDesignPartValidator`, `IDesignCheck`s | Riste |
| `Templates` | `AddDocAgentTemplates` | `DocAgent.Agents` | `ITemplateProvider` | Riste (guidance text with Darko) |
| `Detection` | `AddDocAgentDetection` | `DocAgent.Knowledge` | keyed `ISystemDetector`s | Trajche |
| `DocumentIntelligence` | `AddDocAgentDocumentIntelligence` | `DocAgent.Knowledge` | `IDocumentTextExtractor` | Trajche |
| `Knowledge` | `AddDocAgentKnowledge` | `DocAgent.Knowledge` | `IKnowledgeIngestor` (Trajche), `IKnowledgeRetriever` and keyed `ISectionContextProvider`s (Todor) | Trajche, Todor |
| `Authoring` | `AddDocAgentAuthors` | `DocAgent.Agents` | keyed `IDesignAuthor`s: `single-agent` (Boban); `specialists`, `concurrent`, `group-chat` (Stevcho) | Boban, Stevcho |
| `Pipeline` | `AddDocAgentPipeline` | `DocAgent.Agents` | `IRunPipeline` | Boban |
| `Chat` | `AddDocAgentChat` | `DocAgent.Knowledge` | `IRunChat` | Todor |

Always real, no flag: `IDesignRenderer` (Elena writes v0, Riste owns it), `IRunReader` (Elena, reads the database), and the drill-down tools + MCP endpoint (Riste; they only need `IRunReader`).

Every entry point has the signature `IServiceCollection AddDocAgentX(this IServiceCollection services, IConfiguration configuration)`. Elena creates them as empty stubs in the skeleton, so every owner's starting point exists on day one.

### 6.4 Contract change rule

- **Behind your interface:** do anything. Refactor, change libraries, restructure. No permission needed.
- **Changing `DocAgent.Core`:** post in the team channel what and why, wait half a day for objections, open a PR. Darko is CODEOWNER of `src/DocAgent.Core/**` and approves. Update the fakes in the same PR.
- **Adding a strategy is not a contract change.** A new keyed implementation plus its `StrategyOption` needs no approval.
- **Never** change a contract silently. It's the one thing that really breaks someone else's week.

---

## 7. Repository conventions

```
docagent/
├── src/
│   ├── DocAgent.AppHost/          Aspire orchestrator (Elena)
│   ├── DocAgent.ServiceDefaults/  OTel, health checks, resilience (Elena)
│   ├── DocAgent.Api/              Minimal API, RunWorker, SSE (Elena) · Mcp/ (Riste)
│   ├── DocAgent.Core/             Contracts only, no dependencies (Darko)
│   ├── DocAgent.Fakes/            Fakes for every contract (Elena, then each owner)
│   ├── DocAgent.Profiling/        Extractors, profiler, enrichers, tools, validators, checks (Riste)
│   ├── DocAgent.Agents/           Pipeline, shared blocks (Boban) · authors, prompts (Boban, Stevcho) · Templates/, Rendering/ (Riste)
│   ├── DocAgent.Knowledge/        Ingestion, index, detection (Trajche) · retrieval, context, Ask, lessons (Todor)
│   ├── DocAgent.Safety/           Content Safety screening (Elena)
│   ├── DocAgent.Infrastructure/   EF Core, persistence, IRunReader (Elena)
│   └── web/                       React + Vite + TypeScript (Elena; each owner adds their own tab)
├── tests/
│   ├── DocAgent.UnitTests/
│   └── DocAgent.Evals/            Eval harness + experiment runner (Elena)
├── samples/                       Synthetic export packs, by system and variant (Darko)
├── knowledge/                     Knowledge corpus as Markdown with front matter (Trajche)
├── evals/                         Datasets with ground truth, experiments, baselines, reports (Elena)
├── docs/
│   ├── guides/                    These guides
│   ├── design/                    One design note per owner
│   ├── experiments/               One note per experiment
│   └── adr/                       Architecture decision records
├── AGENTS.md                      Instructions for AI coding agents
└── README.md
```

- **Branches:** `feat/<area>/<short-desc>`, e.g. `feat/rag/hybrid-search`. Short-lived. Merge via PR.
- **PRs:** one reviewer minimum, CI green, small enough to review in 15 minutes.
- **ADRs:** any decision someone might later question gets a one-page ADR in `docs/adr/` (Context / Decision / Consequences).
- **Prompts** are versioned Markdown files in `src/DocAgent.Agents/Prompts/` (and `DocAgent.Knowledge/Prompts/` for Trajche and Todor), never string literals in code.
- **Secrets** never go in the repo. Use `dotnet user-secrets` locally. Endpoints are not secrets; keys are.

---

## 8. Shared configuration

Darko publishes the real values in `docs/environments.md`.

| Key | Example | Notes |
|---|---|---|
| `Foundry:Endpoint` | `https://aif-docagent-dev.openai.azure.com/` | Azure OpenAI endpoint of our Foundry resource |
| `Foundry:ChatDeployment` | `chat-main` | Strong model |
| `Foundry:MiniDeployment` | `chat-mini` | Cheap model: detection, extraction, lessons, eval judge |
| `Foundry:EmbeddingDeployment` | `embed` | Embeddings |
| `AiServices:Endpoint` | `https://aif-docagent-dev.cognitiveservices.azure.com/` | Document Intelligence, Content Safety |
| `Search:Endpoint` | `https://srch-docagent-dev.search.windows.net` | Azure AI Search |
| `Search:IndexName` | `knowledge-v1` | Versioned; re-index by creating v2 |
| `Strategy:Default:Detector` / `:Author` / `:Retrieval` | `rules` / `single-agent` / `tool` | Defaults for runs that don't specify a strategy |
| `Features:Fakes:*` | `true` / `false` | See §6.3 |

---

## 9. Data rules

- **Synthetic or public, nothing else.** No customer data, exports, documents, templates, names or credentials from any engagement: not in the repo, the knowledge base, eval data, prompts or traces.
- **Public sources keep their licence.** Every knowledge document records `SourceUri` and `License`. Azure Architecture Center content is licensed CC BY 4.0, so it can be used with attribution. For any other source, check the licence first. Where in doubt, write a summary in our own words and link the source.
- **System profiles are ours.** We write them in our own words, from public documentation (Dataverse) or from the specs of our fictional systems.
- **Prompt and completion text in traces** only in local development.
- Unsure? Ask Darko before committing.

---

## 10. Timeline

Six and a half working weeks to the demo, counted from Wed 28 Oct. Checkpoints are **real code running together, with fakes off wherever they can be off**. A checkpoint where something is still faked that should be real is the early warning.

| Week | Dates | Darko + Elena (foundation) | Everyone else | Checkpoint |
|---|---|---|---|---|
| W1 | 28–30 Oct | Darko: Azure resources and access, GitHub repo. Elena: Phases 1–3 of the setup guide (solution, contracts, fakes, Aspire + Postgres) | Read this overview and your guide. Install the tooling. Start the research brief | — |
| W2 | 2–6 Nov | Elena: Phases 4–7 (API, web app, CI, hand-over). Darko: test-system specs, first synthetic packs, template guidance | Research brief: spike + design note due **Thu 5 Nov**, reviewed Fri 6 Nov. Pass the Azure smoke test | **Fri 6 Nov — "It runs"** |
| W3 | 9–13 Nov | Elena → telemetry basics, `IContentScreen` spike. Darko: reviews, more packs | Build v1 behind your contract: profiler, `rules` detector, corpus v1, `single-agent` author, retriever, tools over MCP | — |
| W4 | 16–20 Nov | Elena: traces end to end, harness v1 with 3 seed cases. Darko: `dataverse` packs | Real path end to end: real profiler → real detection → real author with tools → rendered design, traced | **Fri 20 Nov — "It's real"** |
| W5 | 23–27 Nov | Elena: judge metrics, variance, dataset to 10+ cases | `specialists` + review loop, `prestep` and `tool` retrieval, `rag` detector, Ask chat, `integration-design`, Document Intelligence, E4 done | — |
| W6 | 30 Nov–4 Dec | Elena: baseline, experiment runner, Content Safety on, Foundry evaluators | Experiment runs E1, E2, E3, E7. `concurrent` author | **Fri 4 Dec — "It's smart"** |
| W7 | 7–11 Dec | Darko + Elena: deploy to Azure, cloud telemetry | Experiment notes, architecture document, pattern catalog, polish, demo | **Fri 11 Dec — Demo** |
| W8 | 14–18 Dec | Buffer only. Goal due 18 Dec | | — |

**What each checkpoint must show:**

- **It runs (6 Nov):** upload a pack → fake screen, profile, detect and author → design visible in the UI. The strategy picker lists the fake options. Every owner can run the solution on their own machine, flip their own flag, and has called a Foundry model from their own machine. Every design note is reviewed.
- **It's real (20 Nov):** the real profiler on every `quarry-wms` and `helio-service` pack; the `rules` detector right on the full packs; the `single-agent` author writes a `system-assessment` from real model data, through tools on MCP; the whole run traced; harness v1 scores it on 3 cases.
- **It's smart (4 Dec):** the `specialists` author with its review loop; hybrid retrieval in `prestep` and `tool` modes; the `rag` detector; the streamed Ask chat; the `integration-design` template; Content Safety on; reports for E1–E4 and E7 against a committed baseline.
- **Demo (11 Dec):** deployed to Azure; a demo script that shows detection, two design types, a strategy comparison, Ask, and one trace; experiment notes, architecture document and pattern catalog done.

---

## 11. Scope

**Must (11 Dec)**
- Upload a pack (files or zip) → screen → profile → detect → author → validate and review → render, for `system-assessment` and `integration-design`
- All three test systems and the unknown packs profiled and detected; PDFs read with Document Intelligence
- At least three authors (`single-agent`, `specialists`, `concurrent`), selectable per run and compared (E2)
- RAG: hybrid search over the knowledge base, with `none`, `prestep` and `tool` modes compared (E3), and search configurations compared (E4)
- Detection: `rules` and `rag` compared (E1)
- Drill-down tools served over MCP (Streamable HTTP) and used by the agents (E7)
- Streamed Ask chat with citations
- Deterministic checks, an LLM judge and Foundry-backed evaluators; a committed baseline; experiment reports
- Content Safety screening of uploads, plus Prompt Shields on the model deployments
- Traces in App Insights; deployed to Azure Container Apps

**Should**
- `migration-design` template · `group-chat` author · `hybrid` detector
- System enrichers (Dataverse metadata) · confirming a low-confidence detection before authoring (human in the loop)
- Lessons (E6) · model mix (E5) · .docx export
- Ask agent hosted in Foundry Agent Service · rerun-and-compare view · PII check with Azure AI Language

**Later (H1 2027)**
`magentic` author and agentic retrieval, if not reached · more systems · Entra ID sign-in · eval gates in CI · a Copilot Studio front end · generating infrastructure (Bicep, Logic Apps definitions) from a design

---

## 12. How this covers the Track 1 requirements

| Requirement (practice deck) | Where it lives | Owner |
|---|---|---|
| Foundry project, Azure OpenAI models | `chat-main`, `chat-mini`, `embed` deployments, used by alias | Darko |
| AI Search + embeddings (RAG) | Hybrid index over the knowledge base; E3 and E4 | Trajche, Todor |
| Document Intelligence | PDFs in export packs and in knowledge uploads → Markdown | Trajche |
| C# SDKs (Azure.AI.OpenAI, Microsoft Agent Framework) | Every agent and the pipeline workflow | Boban, Stevcho, Trajche, Todor |
| Prompt engineering | Versioned prompts, every change backed by an eval number | Stevcho |
| RAG Q&A | Ask chat over a run or over the knowledge base | Todor |
| MAF, multi-agent, tool orchestration | Authors: single agent, specialists, concurrent, group chat; drill-down tools | Boban, Stevcho, Riste |
| MCP | Drill-down tools and knowledge search on our MCP server, also usable from Copilot in VS Code | Riste |
| Foundry Agent Service | Ask agent hosted in Foundry (should) | Todor |
| Streaming | Ask chat and run progress over Server-Sent Events | Todor, Elena |
| Evaluation (Foundry evaluation tools) | Harness with `Microsoft.Extensions.AI.Evaluation` quality evaluators and its Safety evaluators, which call the Foundry Evaluation service | Elena |
| Content safety filters | Prompt Shields on deployments; screening of every upload | Darko, Elena |
| Telemetry (App Insights) | OpenTelemetry → App Insights + Foundry tracing | Elena |
| GitHub Copilot | In every IDE; repo instructions; Copilot-generated unit tests; Copilot PR review | Everyone |
| Integration and showcase | The web app, deployed; architecture document; demo | Elena, Darko |
| "What can be reused from TRYche?" | Pattern catalog mapping our patterns to TRYche | Darko |

---

## 13. Ways of working

### The research brief (W1–W2, everyone except Elena)

Your guide lists the questions your research must answer. The output is two things, due **Thu 5 Nov**:

1. **A spike**: a scratch console app (not in the repo) that proves the one thing your area depends on most, against our Azure resources. Your guide says what it is.
2. **A design note**, one to two pages, in `docs/design/<area>.md`, with this structure:
   - *What I'm building* (two paragraphs, in your words)
   - *Answers to the research questions* (short, with links to the docs you used)
   - *Design*: components, how they implement the contracts, data flow, the libraries and package versions you'll use
   - *Risks and open questions*
   - *Week-by-week plan* for W3–W7, taken from your guide and adjusted
   - *Extras*: anything you found that we should also try. If it fits the architecture and the timeline, do it.

Darko reviews every note on Fri 6 Nov. That review, not the calendar, is the gate for starting to build.

### Rules

- **Weekly sync, 45 minutes, same slot.** Demo what moved, blockers, contract changes. No status theatre.
- **Fortnightly checkpoints** (§10) with fakes off.
- **Walking skeleton first**, then real implementations behind the contracts.
- **Concrete tasks, open ends.** Your guide gives you concrete tasks with gates so there is never a question of what to do next. It also tells you where the open ends are. If you find a better way or an extra capability worth trying, write it in your design note and do it, as long as the contracts and the checkpoints hold.
- **GitHub Copilot everywhere:** `.github/copilot-instructions.md` in the repo, unit tests generated with Copilot and then reviewed, Copilot PR review on every PR.
- **Coding agents** (Claude Code, Copilot agent mode) may execute a guide one phase at a time:

> Read `docs/guides/00-team-overview.md` and `docs/guides/<your guide>.md`. Execute only Phase N (or week N). Stop at the gate, run the verification and show me the output. Don't modify anything in `src/DocAgent.Core` without telling me.

  Review every diff. The agent is fast; you are accountable. Library APIs in this space move quickly. If a method in a guide doesn't exist in the version you installed, have the agent check the current docs instead of inventing an API.

---

## 14. Who to ask

| Question about | Ask |
|---|---|
| Azure access, endpoints, quotas, costs | Darko |
| Contracts, architecture, test systems, "can this go in the repo?" | Darko |
| Running the solution, API, database, UI patterns, CI | Elena |
| The run pipeline, agent building blocks, tool wiring, `single-agent` | Boban |
| Reviewer, review loop, `specialists` / `concurrent` / `group-chat`, prompts, E2 | Stevcho |
| Profiling, drill-down tools, MCP, templates, validators, checks, rendering | Riste |
| Corpus, index, chunking, Document Intelligence, detection | Trajche |
| Retrieval modes, Ask chat, lessons | Todor |
| Traces, cost, Content Safety, evals, experiments | Elena |
