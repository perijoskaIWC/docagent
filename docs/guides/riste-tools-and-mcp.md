# Riste — Tools and MCP: the Profiler, Drill-down Tools, the MCP Server and the Quality Gates

**You own what the agents can see and do, and the guarantees around what they produce.** The profiler turns any export pack into one neutral `SystemModel` with exact source locations. The drill-down tools let agents read that model on demand, in-process and over **MCP** from our own server. The templates say what a design must contain; the validators and checks make sure the agents delivered it. The renderers turn the structured design into a document.

Read `00-team-overview.md` first, especially §2 (design types, test systems), §3 (principles) and §6 (contracts, ids). Your consumers are Boban (tools, validator), Stevcho (templates, reviewer uses tools), Trajche (signals for detection), Todor (`search_knowledge` is hosted by you, defined by him), Elena (checks feed the harness).

---

## Your mission and what you'll learn

| You deliver | You learn |
|---|---|
| `IExportProfiler`: extractors for CSV, Excel, JSON, OpenAPI, XML, XSD, SQL DDL, PDF (via Document Intelligence), Markdown; relationship inference; data-quality findings; detection signals | **Deterministic extraction** as the foundation of a reliable AI system; .NET parsing libraries (`Microsoft.OpenApi`, `ClosedXML`, `System.Xml.Schema`, T-SQL ScriptDom) |
| Drill-down tools as `AIFunction`s with tight descriptions and bounded output | **Tool design for LLMs**: what makes a tool usable by a model, how descriptions drive behaviour |
| An **MCP server** on the API (Streamable HTTP) exposing the same tools to our agents, Copilot in VS Code and any MCP client; experiment E7 | **Model Context Protocol**: the spec, the C# SDK, transports, security (tool poisoning, path traversal, read-only design) |
| Templates with checkable guidance; `IDesignPartValidator`; `IDesignCheck`s | **Quality gates around LLM output**: structured parts, validation, coverage checks |
| Markdown renderer (and .docx, should) | Deterministic rendering with the Open XML SDK |

---

## Research brief (now → Thu 5 Nov)

Answer these in `docs/design/profiling-and-tools.md`, with links. Sources: `modelcontextprotocol.io` (specification: transports, tools, security best practices); the `modelcontextprotocol/csharp-sdk` GitHub repo (README and samples for `ModelContextProtocol.AspNetCore`); Microsoft.Extensions.AI docs on `AIFunctionFactory`; the `Microsoft.OpenApi` README; `ClosedXML` docs; Microsoft Learn on `System.Xml.Schema`; the Open XML SDK docs.

**MCP**
1. What is the current MCP spec version, and what does **Streamable HTTP** transport define (single endpoint, POST + optional SSE stream, session ids)? Why was SSE-only transport dropped?
2. In the C# SDK (`ModelContextProtocol.AspNetCore`, stable 1.x): how do you register a server (`AddMcpServer().WithHttpTransport().WithTools<T>()`), declare tools (`[McpServerToolType]`, `[McpServerTool]`, `[Description]`), map the endpoint (`MapMcp`), and how do tools get per-request context (DI, `IHttpContextAccessor`, headers)? How are errors returned to the client?
3. How does an MCP **client** in C# connect to a Streamable HTTP server, list tools and expose them as `AIFunction`s (Boban consumes them)? How are arguments typed and validated?
4. **Security**: what are the known attack classes against MCP servers (tool poisoning via descriptions, path traversal in file tools, command injection, confused deputy) and what does the spec's security guidance recommend? What does it mean for our read-only, run-scoped tools?
5. How do you add our MCP server to **VS Code** (user or workspace `mcp.json`) and use it from Copilot agent mode? This is the demo.

**Profiling**
6. Which libraries will you use per format, and what provenance (line numbers, cell ranges, JSON pointers, XPath) does each give you? For OpenAPI: does `Microsoft.OpenApi` read YAML out of the box in the version you'll use, and how do you get line info?
7. How will you infer field types and keys from values (sample size, rules), and relationships from names and value overlap? What false positives do you expect?
8. Document Intelligence's layout model returns Markdown with tables: how will you turn a Markdown table into candidate entities, and how do you record that they came from a document (an `Info` finding)?

**Spike** (one evening): parse one CSV, one OpenAPI YAML and one XSD into a draft `SystemModel` with ids per 00 §6.2 and locators you trust; then host **one** tool (`list_entities`) on a Streamable HTTP MCP server in a scratch ASP.NET app and call it from VS Code Copilot. Give Boban the endpoint for his spike.

**Design note:** extractor list with libraries and versions; the **signal catalogue** (agreed with Trajche: names, value formats, examples per test system); the **tool list** with signatures and bounds (agreed with Boban and Stevcho); the MCP server design (endpoint, run scoping, security measures); template section guidance drafts (reviewed with Darko on Mon 2 Nov); the E7 hypothesis.

---

## Step 1 — The profiler: any export pack → one `SystemModel`

### Principles
- **Generic first.** `ProfileAsync` knows nothing about specific systems. It reads file formats, not products. System knowledge comes later: detection (Trajche) and enrichers (below).
- **Provenance on everything.** Every entity, field, interface and finding carries a `SourceRef` with a precise locator (00 §6.1). Evidence and citations depend on it.
- **Stable ids** as in 00 §6.2. Same input, same ids.
- **Never throw on a weird file.** Return what you could read and add a `Finding` (`Warning` or `Error`). A partial model beats a failed run.
- **Signals are first-class.** Detection depends on them. Collect them deliberately (table below).
- **No LLM.** If you're tempted to "just ask the model", it's a job for the agents, not the profiler.

### One extractor per file kind

`CompositeExportProfiler` (registered as `IExportProfiler`) runs one `IFileExtractor` per file (an internal interface, not in Core), then merges the results and infers relationships across files.

| Kind | Library (suggested) | Entities and fields | Interfaces / schedules | Signals |
|---|---|---|---|---|
| CSV / NDJSON | `Sep` or `CsvHelper`; `System.Text.Json` | One entity per file; columns → fields; types inferred from values; row count; key candidates (unique, non-null) | — | column-name prefix, id format (guid, numeric, prefixed code), date format, delimiter, encoding |
| Excel | `ClosedXML` | One entity per sheet with a header row; same inference as CSV | — | sheet names, header styles, workbook properties |
| JSON | `System.Text.Json` | Array of objects → entity; nested objects → child entities; JSON Schema → declared fields and `required` | Config files with `cron` / `schedule` keys → schedules; `webhooks` / `url` + `events` → webhook interfaces | top-level keys, `$schema`, product-like keys |
| OpenAPI (JSON/YAML) | `Microsoft.OpenApi` (+ its YAML reader) | `components.schemas` → entities with declared types, required, keys (`id`) | Every path + method → `RestOperation` | `info.title`, `servers`, security schemes, path prefixes |
| XML | `System.Xml.Linq` with `LoadOptions.SetLineInfo` | Repeated sibling elements → entity; attributes and leaf children → fields | — | root element, namespaces, processing instructions |
| XSD | `System.Xml.Schema` | Elements and complex types → entities/fields with types; `minOccurs` → required; `key`/`keyref` → declared relationships | — | target namespace, root element names |
| SQL DDL | `Microsoft.SqlServer.TransactSql.ScriptDom` (or a careful regex for simple DDL) | `CREATE TABLE` → entities; columns, types, nullability; `PRIMARY KEY`; `FOREIGN KEY` → declared relationships | `CREATE VIEW` → `DatabaseView` | schema names, naming conventions |
| PDF | `IDocumentTextExtractor` (Trajche, Document Intelligence) → Markdown | Tables in the Markdown → candidate entities, each with an `Info` finding saying it came from a document | Headings like "Interfaces", "Schedules" → findings for agents to read | title, headings, product names in text |
| Markdown / text | — | — | — | headings, keywords |

**Relationship inference** (after all files): `declared` from DDL foreign keys, XSD keyrefs and metadata; `inferred` when a field named like `<entity>_id` / `<entity>Id` matches the key of another entity *and* a sample of values overlaps. Record which, in `Basis`.

**Data-quality findings** (cheap, valuable): null rate per field above a threshold, duplicate keys, mixed formats in one column, orphan foreign-key values, dates in the future, empty files. These give the `data-quality` and `risks` sections real material.

### Enrichers (should, W5–W6)
`EnrichAsync(model, pack, detection)` runs **after** detection. One enricher per system id, e.g. `DataverseEnricher` reads `customizations.xml` properly: table and column display names, option sets, relationship definitions. If no enricher exists for the detected system, return the model unchanged. This keeps the generic profiler generic while still showing the "detect, then specialise" pattern.

### Tests
`tests/DocAgent.UnitTests/Profiling/`: for every pack in `samples/`, a golden JSON of the expected model (ids, counts, signals). Regenerate deliberately and review the diff. Darko's pack generator spec gives you the ground truth.

---

## Step 2 — Drill-down tools

Agents get an overview, then pull detail on demand. That keeps prompts small and evidence precise.

| Tool | Returns | Bound |
|---|---|---|
| `get_overview()` | Files, entity names with field counts, interface count, finding counts, detection | ~1–2k tokens |
| `list_entities(filter?)` | Entity ids, names, row counts | 100 rows |
| `get_entity(entityId)` | Fields with types, keys, required, sample values, source | one entity |
| `get_relationships(entityId?)` | Relationships with basis | 100 rows |
| `list_interfaces(kind?)` | Interfaces with direction and detail | 100 rows |
| `list_findings(level?)` | Findings with sources | 100 rows |
| `find(text)` | Any element whose id, name or detail contains the text | 30 hits |
| `read_source(path, locator)` | The exact original text for a source location | 200 lines / 50 cells |
| `search_knowledge(query, kinds?)` | Knowledge chunks (wraps Todor's retriever; Todor defines the behaviour) | top 8, 6k tokens |

**Run scoping.** In-process, the tool class is created per run and closes over that run's `SystemModel`. Over MCP, every tool takes a `runId` parameter and loads the model through `IRunReader`.

```csharp
using System.ComponentModel;
using Microsoft.Extensions.AI;

public sealed class ModelTools(SystemModel model, ExportPack pack)
{
    [Description("Returns one entity by id (e.g. 'entity:work_orders') with its fields, keys, types, sample values and source.")]
    public string GetEntity([Description("Entity id from get_overview or list_entities")] string entityId) { /* ... */ }

    [Description("Returns the original text at a source location, e.g. path 'orders.csv' and locator 'L1-L20'. Max 200 lines.")]
    public string ReadSource(string path, string locator) { /* ... */ }

    public IList<AITool> AsTools() => [ AIFunctionFactory.Create(GetEntity), AIFunctionFactory.Create(ReadSource), /* ... */ ];
}
```

**Rules for good tools:** short, specific descriptions with an example id; bounded output (truncate and say so); compact JSON or text; never throw. Return an error message the model can act on ("No entity 'entity:order'. Did you mean 'entity:orders'?").

---

## Step 3 — The MCP server (W3–W4, must)

Serve the same tools from the API at `/mcp` over **Streamable HTTP**, using the official C# SDK (`ModelContextProtocol.AspNetCore`, 1.x stable as of October 2026).

```csharp
// Program.cs (shape; check the SDK README for the installed version)
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<McpModelTools>();          // [McpServerToolType] class whose methods are [McpServerTool]
// ...
app.MapMcp("/mcp");
```

`McpModelTools` wraps `ModelTools`: each method takes a `runId`, loads the run through `IRunReader`, and delegates.

**Security, treated as code:**
- **Read-only tools only.** Nothing that writes, deletes or calls out.
- **Validate inputs.** `read_source` must only read files of that run's pack: no paths with `..` and no absolute paths.
- **Tool descriptions are part of the attack surface** (tool poisoning). Review description changes like code changes.
- Behind the same auth as the API once auth arrives. Until then, the endpoint is only reachable inside the dev environment.

**Demo:** add the server to VS Code's MCP config and ask Copilot "list the entities of run X and show me the fields of work_orders".

### Experiment E7 — tools in-process vs over MCP
Same author, same cases, `Agents:ToolSource = inprocess` vs `mcp`. Measure added latency per tool call, failures and token differences (tool schemas can differ). Hypothesis first. The result tells the practice what MCP costs when you also own the agent.

---

## Step 4 — Templates

Three JSON files matching `DesignTemplate` (section ids from 00 §2). **The guidance text is the most important thing you write**: both the authors and the Reviewer read it. Make it specific and checkable.

```json
{
  "id": "system-assessment",
  "name": "System assessment",
  "description": "Describes the system as it is: data, interfaces, rules, data quality and risks.",
  "requiresTarget": false,
  "sections": [
    {
      "id": "data-model",
      "title": "Data model",
      "guidance": "Describe every entity in the model: purpose (assumption unless documented), key, row count, important fields. Then list relationships with their basis. Every row cites the entity id. Entities you cannot explain become open questions.",
      "required": true,
      "tables": [
        { "id": "entities", "title": "Entities", "columns": ["Entity id", "Name", "Purpose", "Key", "Rows"] },
        { "id": "relationships", "title": "Relationships", "columns": ["From field id", "To field id", "Basis"] }
      ],
      "knowledgeKinds": ["SystemProfile", "Standard"]
    }
  ]
}
```

| Template | Sections that need tables |
|---|---|
| `system-assessment` | `data-model` (entities, relationships), `interfaces` (interface id, kind, direction, detail), `data-quality` (finding id, impact, recommendation) |
| `integration-design` | `flows` (flow, trigger, source, target, Azure services, error handling), `mapping` (source field id, target, transformation), `operations` (signal, threshold, action) |
| `migration-design` | `inventory` (entity id, rows, in scope), `mapping` (source field id, target field, transformation), `reconciliation` (check, method, tolerance) |

Knowledge kinds per section: `patterns`, `flows`, `approach` → `Pattern`; `system`, `source` → `SystemProfile`; everything → `Standard`; Todor decides how to use them.

---

## Step 5 — Validator and checks (the honesty layer)

### `IDesignPartValidator` (per part, called by the `submit_part` tool)

| Code | Rule |
|---|---|
| `table.unknown` | Every table id exists in the section spec |
| `table.columns` | Every row has exactly the spec's column count |
| `evidence.missing` | Every `Evidenced` statement and every table has ≥1 evidence ref |
| `evidence.unresolved` | Every `Model` ref exists in the `SystemModel`; every `Knowledge` ref is in `ValidationContext.KnowledgeChunkIds` |
| `unknown.unasked` | Every `Unknown` statement has an open question in the same part |
| `ids.invented` | Source-id columns (e.g. "Source field id") contain only ids that exist in the model |
| `part.empty` | A required section has at least one statement or table row |

Messages must tell the agent exactly what to fix: "Row 3 of table 'mapping' cites 'field:orders.customer' which does not exist. Use get_entity('entity:orders') to see its fields."

### `IDesignCheck`s (whole design, after authoring)

| Check id | Score |
|---|---|
| `sections.required` | share of required sections present |
| `coverage.entities` | share of model entities referenced somewhere in the design |
| `coverage.interfaces` | share of interfaces referenced |
| `coverage.findings` | share of Warning/Error findings referenced (in data-quality or risks) |
| `evidence.rate` | share of statements that are Evidenced |
| `detection.consistent` | design's system equals the run's detection |

These run on every design and are also the cheapest metrics in Elena's harness.

---

## Step 6 — Rendering

You take over `MarkdownDesignRenderer` from Elena in W3:
- Template order; `##` per section; statements as paragraphs; assumptions marked; unknowns as **[TBD]**; tables with the spec's titles and columns; open questions as a list.
- A compact evidence line per section; model ids shown with their source (`orders.csv L1`).
- A header block: detected system with confidence, target, template, strategy used.

**.docx (should):** render the `SolutionDesign` (not the Markdown) with the Open XML SDK (`DocumentFormat.OpenXml`): headings, paragraphs, tables, an evidence line. Add `IDocxRenderer` in `DocAgent.Agents` (not a Core contract) and an endpoint with Elena.

---

## Week plan

| Week | Do | Done when |
|---|---|---|
| **W1** 28–30 Oct | Read 00 and this guide. Tooling, smoke test. Start the research questions. Draft template section guidance for Darko's review on Mon 2 Nov | — |
| **W2** 2–6 Nov | Spike (above). Design note. Signal catalogue with Trajche; tool list with Boban and Stevcho; `search_knowledge` behaviour with Todor | Design note reviewed Fri 6 Nov |
| **W3** 9–13 Nov | Profiler v1: CSV, Excel, JSON, XSD, OpenAPI on Darko's first packs. Golden tests. Tools v1 in-process. `system-assessment` template real. Validator | `Profiler = false`, `Templates = false` locally; Boban's `single-agent` uses your tools |
| **W4** 16–20 Nov | **MCP server** with all tools, run-scoped, read-only. XML, SQL DDL. Relationship inference. Checks. Renderer taken over from Elena | **Checkpoint "It's real"**: agents call tools over MCP; MCP demo from VS Code |
| **W5** 23–27 Nov | PDF via Trajche's `IDocumentTextExtractor`. `integration-design` template. Data-quality findings. `dataverse` packs profiled | All test packs profile without errors |
| **W6** 30 Nov–4 Dec | **Run E7** with Elena's runner. `DataverseEnricher` (should). `migration-design` template (should) | **Checkpoint "It's smart"**: E7 report |
| **W7** 7–11 Dec | .docx (should). Edge cases: huge, malformed, mixed encodings. E7 note. Architecture document section on tools and MCP | Demo |

---

## Definition of done (11 Dec)

- [ ] Every pack in `samples/` profiles without an unhandled error; golden tests green
- [ ] Signals catalogue documented and used by Trajche's detectors
- [ ] Drill-down tools in-process and over MCP (Streamable HTTP), read-only, inputs validated
- [ ] `system-assessment` and `integration-design` templates with specific guidance; `migration-design` (should)
- [ ] Validator wired into `submit_part`; checks run on every design
- [ ] `Profiler = false` and `Templates = false` are the shared defaults
- [ ] Markdown output clean; .docx (should)
- [ ] E7 report and experiment note committed

---

## If you have time left (write it in the design note first)

- **Knowledge search over MCP** too: expose `search_knowledge` on the server so Copilot in VS Code can query our knowledge base.
- **MCP resources**: expose a run's rendered design and model as MCP *resources*, not only tools; see what clients do with them.
- **MCP prompts**: a server-provided prompt "assess this run" that a client can invoke.
- **Streaming tool results** for large `read_source` calls, if the SDK version supports it.
- A **SQL DDL dialect** beyond T-SQL (PostgreSQL) if a test pack needs it.

---

## Pitfalls

- **Locators off by one.** The evidence system breaks quietly. Test them explicitly.
- **Type inference from a handful of rows.** Sample enough rows, and record "inferred" vs "declared".
- **Tools returning megabytes.** Cap output and say "truncated".
- **A profiler that secretly knows the test systems.** Generic extractors only; system knowledge belongs to detection and enrichers.
- **`read_source` with path traversal.** Only files of that run's pack.
- **Vague template guidance.** The agents can only be as precise as your guidance.
