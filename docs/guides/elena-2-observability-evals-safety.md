# Elena — Guide 2: Observability, Evaluation & Safety

**From week 3 you own the evidence and the safety net.** Every run traced end to end, cost per run known, every upload screened, and an evaluation harness plus an experiment runner that tell the team, with numbers, which strategy is better and what it costs. This is a lab, so your harness is the instrument every experiment depends on. Without it, December's demo is opinions.

Read `00-team-overview.md` first, especially §4 (experiments) and §6 (contracts). Finish `elena-1-project-setup.md` before starting here; your Phase 4 `RunWorker` is where the root span lives.

---

## Your mission and what you'll learn

| You deliver | You learn |
|---|---|
| OpenTelemetry traces of every run: API → worker → pipeline → agents → tools → retrieval, in the Aspire dashboard locally and in Application Insights + Foundry tracing in the cloud | **OpenTelemetry in .NET**, the GenAI semantic conventions, how Microsoft Agent Framework and Microsoft.Extensions.AI emit spans, Azure Monitor and Foundry tracing |
| Tokens and cost per run, metrics by strategy | **Cost engineering** for LLM apps |
| `IContentScreen`: every upload screened with Azure AI Content Safety (Prompt Shields + harm categories) before an agent reads it | **Azure AI Content Safety**, indirect prompt injection and how to defend against it |
| An eval harness with deterministic checks, an LLM judge, Microsoft.Extensions.AI.Evaluation quality and Foundry-backed safety evaluators, variance measurement, and an experiment runner that produces reports | **LLM evaluation**: datasets with ground truth, judges and their calibration, Foundry evaluation tools, how to run a fair experiment |
| Experiment E5 (model mix) | What the big model is actually needed for |

---

## Research brief (W2, alongside the setup phases; design note due Thu 5 Nov)

You can't spend a full week on research, so keep this to about a day and a half spread over W2. Answer these in `docs/design/telemetry-evals-safety.md`:

**Telemetry**
1. Which `ActivitySource` names do Microsoft Agent Framework and Microsoft.Extensions.AI use in the versions Boban installs, and how is OpenTelemetry enabled on an agent (`UseOpenTelemetry`)? What does `EnableSensitiveData` capture? Sources: the MAF observability docs on Microsoft Learn (`learn.microsoft.com/agent-framework`), the `microsoft/agent-framework` samples.
2. How does Aspire's ServiceDefaults export to Azure Monitor (which package, which connection string), and what does the Foundry project show once Application Insights is connected (Tracing / Observability blade)?
3. Langfuse: what does its OTLP endpoint need (HTTP/protobuf, Basic auth), and what would it give us beyond App Insights (prompt management, datasets, scores)? Is it worth a second exporter? Decide in ADR 0003.

**Content Safety**
4. Which operations exist in Azure AI Content Safety for our case: Prompt Shields (`shieldPrompt`, with `userPrompt` and `documents`) and text analysis (harm categories, severity). What are the size limits per call, and does the current `Azure.AI.ContentSafety` .NET package expose Prompt Shields, or do we call REST? Source: `learn.microsoft.com/azure/ai-services/content-safety`.
5. What does the deployment-level content filter with Prompt Shields (configured by Darko in Foundry) already catch, and what does it not (hint: it sees prompts, not our raw uploads before they're read)?

**Evaluation**
6. In `Microsoft.Extensions.AI.Evaluation`: which evaluators exist in Quality (Groundedness, Completeness, Relevance, Retrieval, …) and Safety (GroundednessPro, IndirectAttack, ProtectedMaterial, …), what inputs do they need (question / answer / context), how does response caching work, and what does `dotnet aieval` report? Source: `learn.microsoft.com/dotnet/ai/evaluation/libraries`.
7. What does the Safety package need to reach the **Foundry Evaluation service** (project endpoint, credential, region support)?
8. How do we measure **run-to-run variance** fairly: how many repeats, which statistics, how to compare evidence sets between runs?

**Spike** (half a day): a toy MAF agent in an Aspire app (Boban has one from his spike; reuse it) whose spans appear in the Aspire dashboard, then exported to Application Insights; plus one call to Prompt Shields with a text that hides an instruction, returning "attack detected".

**Design note contents:** the span model below, confirmed against real source names; the ADR 0003 decision; the Content Safety policy (what blocks, what warns); the harness architecture and the experiment JSON format; which M.E.AI.Evaluation evaluators you'll use for which metric; the variance method.

---

## Part 1 — Telemetry

### Start from what Aspire gives you
The ServiceDefaults template already configures OpenTelemetry logs, metrics and traces with an OTLP exporter that the Aspire dashboard receives locally, and a hook for Azure Monitor when an Application Insights connection string is present. Build on it.

### Add the AI sources
Microsoft Agent Framework and Microsoft.Extensions.AI emit OpenTelemetry spans following the GenAI semantic conventions when you opt in on the agent / chat client builder (`.AsBuilder().UseOpenTelemetry(sourceName: ..., configure: c => c.EnableSensitiveData = ...)`). Register their sources with the tracer provider, plus ours:

```csharp
// in ServiceDefaults: AddDocAgentTelemetry()
builder.Services.AddOpenTelemetry().WithTracing(t => t
    .AddSource("DocAgent.*")                 // our ActivitySources
    .AddSource("Microsoft.Agents.AI*")       // confirm exact names in the MAF observability docs
    .AddSource("Microsoft.Extensions.AI*"));
```

**Sensitive data** (prompt and completion text in spans): on **only in local development**. Off in any shared or cloud environment.

### Span model

```
docagent.run                      run.id, template.id, docagent.strategy.detector/author/retrieval, lessons, pack.files
├── pipeline: screen              screen.allowed, screen.flags
├── pipeline: profile             model.entities, model.interfaces, model.findings, model.signals
├── pipeline: detect              detect.system_id, detect.confidence, detect.strategy
├── pipeline: enrich              enrich.applied
├── pipeline: author              author.key
│   ├── section / executor spans  section.id, iteration, prompt.name, prompt.version
│   │   ├── invoke_agent …        gen_ai.* (model, tokens)                      (MAF)
│   │   ├── execute_tool get_entity / search_knowledge / submit_part             (MAF)
│   │   └── docagent.retrieval    mode, kinds, top, hit ids, scores               (Todor)
│   └── review                    iteration, approved, score, issues.critical/major
├── pipeline: check               checks.passed, checks.failed
└── pipeline: render              usage.input_tokens, usage.output_tokens, usage.model_calls, cost.usd
```

**Put the strategy on the root span.** Then any trace can be filtered by "all `specialists` runs with `tool` retrieval". Our own attributes are prefixed `docagent.`.

### Logs
`ILogger` with message templates, never string interpolation. A logging scope with `RunId` around each run. `Information` for lifecycle, `Warning` for retries and parse warnings, `Error` for failures with the exception. No `Console.WriteLine`.

### Cost and metrics
A price table in config: input and output price per 1M tokens per deployment alias, from the Azure pricing page for the models Darko deploys. Boban's `UsageMeter` sums tokens; you turn them into `EstimatedCostUsd`. Metrics: `docagent.runs` (by status, author), `docagent.run.duration`, `docagent.tokens` (by deployment and direction), `docagent.cost`, `docagent.tool_calls` (by tool), `docagent.detection.confidence`.

### Where traces go: ADR 0003 by 6 Nov

| Option | Strengths | Weaknesses |
|---|---|---|
| **Aspire dashboard** (local) | Zero setup, shows GenAI spans | Local only, not persistent |
| **App Insights + Foundry tracing** (cloud) | Native Azure; the Foundry portal shows agent traces when the project is connected to App Insights; data stays in our subscription | Less LLM-specific UI |
| **Langfuse** (Cloud EU or self-hosted) | LLM-centric UI, prompt management, datasets, scores | Another vendor; data leaves Azure unless self-hosted; keys to manage |

Langfuse receives OTLP at `/api/public/otel` (EU traces endpoint `https://cloud.langfuse.com/api/public/otel/v1/traces`), **HTTP/protobuf only, no gRPC**, with Basic auth `publicKey:secretKey`. Keys go to user-secrets locally and Key Vault in the cloud.

**Recommended default:** Aspire locally + App Insights / Foundry tracing in the cloud for everyone. Add Langfuse as a second exporter only if your W1 spike shows it's worth it and Darko agrees.

---

## Part 2 — Content Safety (`IContentScreen`)

Uploaded exports are **untrusted input that our agents read**. A file can contain hidden instructions ("ignore your rules and…"). Two layers:

1. **Deployment level (Darko):** a content filter on `chat-main` and `chat-mini` with **Prompt Shields** on for jailbreaks and **indirect (document) attacks**.
2. **Upload level (you):** before any agent sees the pack, `IContentScreen` sends the pack's text to Content Safety:
   - **Prompt Shields** with the file text as `documents`, to detect document attacks
   - **Text analysis** for harmful content categories

Implementation notes:
- Use `AiServices:Endpoint` with `DefaultAzureCredential` (role: Cognitive Services User). The `Azure.AI.ContentSafety` package covers text analysis; for Prompt Shields use the package if your version exposes it, otherwise the REST `text:shieldPrompt` operation. **Check the current API version and size limits** and split long text accordingly.
- Text formats are screened directly. For Excel, screen the cell text; for PDFs, the Markdown from `IDocumentTextExtractor` (cache by file hash so Document Intelligence isn't called twice).
- **Policy (in config):** block on a detected document attack or high-severity harm; warn on lower severities. Flags appear on the run in the UI and on the `screen` span.
- **Test it:** Darko adds an `injection` variant pack with a planted instruction. It must be blocked, every time.

---

## Part 3 — The evaluation harness

### Dataset: ground truth comes from the pack generator

```
evals/datasets/v1/
└── case-helio-full-assessment/
    └── case.json
```

```json
{
  "id": "case-helio-full-assessment",
  "pack": "samples/helio-service/full",
  "templateId": "system-assessment",
  "target": null,
  "brief": "We want to understand this system before integrating it.",
  "expected": {
    "systemId": "helio-service",
    "entities": ["entity:work_orders", "entity:technicians", "entity:customers", "entity:sites"],
    "facts": [
      { "id": "f1", "sectionId": "data-model", "fact": "Work orders reference technicians through technician_id", "source": "model" },
      { "id": "f2", "sectionId": "data-quality", "fact": "About 8% of work orders have no completion date", "source": "model" },
      { "id": "f3", "sectionId": "interfaces", "fact": "Webhooks notify on work order status changes", "source": "model" },
      { "id": "f4", "sectionId": "risks", "fact": "API rate limits constrain bulk extraction", "source": "knowledge" }
    ],
    "mustNotContain": ["Quarry WMS", "Dataverse"]
  },
  "notes": "Who wrote this case and why it's interesting"
}
```

- **Expected facts, not golden documents.** 5–15 checkable facts per case are quick to write and catch what matters.
- `source: knowledge` marks facts that can only come from the knowledge base. That's what E3 needs.
- Darko seeds the first three cases (13 Nov); everyone adds at least one by W5. **Target: 15 cases by 4 Dec**, covering every system, every variant, the unknown packs and both main templates.
- Detection-only cases (no template) are fine for E1.

### Metrics

| Metric | How | Type |
|---|---|---|
| Detection correct, confidence | Compare with `expected.systemId` | Deterministic |
| Check pass rate, coverage, evidence rate | From Riste's `IDesignCheck` results | Deterministic |
| Entity coverage | `expected.entities` referenced in the design | Deterministic |
| Must-not-contain | String match on the rendered design | Deterministic |
| Fact recall | Per expected fact, the judge (`chat-mini`, temperature 0) answers stated / implied / missing, with a quote | LLM judge |
| Unsupported claims | The judge lists statements not supported by the cited evidence | LLM judge |
| Groundedness, completeness, relevance | `Microsoft.Extensions.AI.Evaluation.Quality` evaluators | LLM judge |
| Groundedness Pro, indirect attack, protected material | `Microsoft.Extensions.AI.Evaluation.Safety` evaluators (call the **Foundry Evaluation service**) | Service |
| Variance | Same case × N runs: std-dev of fact recall; overlap of evidence refs; spread of statement and open-question counts | Deterministic |
| Tokens, model calls, tool calls, cost, duration | From `DesignResult.Usage` and timing | Deterministic |

Keep the judge prompt versioned like any other. **Hand-check the judge** on the first runs and every time its prompt changes: a bad judge produces confident nonsense. The Reporting package and the `dotnet aieval` tool give response caching (cheaper re-runs) and HTML reports; use them, and add our own Markdown comparison report on top.

### Running
The harness calls `IRunPipeline` directly (no HTTP) with the same DI registrations as the API, so any strategy registered in the app can be evaluated.

```bash
dotnet run --project tests/DocAgent.Evals -- \
  --experiment evals/experiments/E2-agent-architecture.json \
  --out evals/reports/2026-12-02-E2.md
```

```json
// evals/experiments/E2-agent-architecture.json
{
  "id": "E2",
  "hypothesis": "Specialists + reviewer raise fact recall by ≥10 points and halve variance at ≤2× tokens",
  "dataset": "evals/datasets/v1",
  "cases": ["*-full-*", "*-noisy-*"],
  "fixed": { "detector": "rules", "retrieval": "tool", "lessons": false, "maxReviewLoops": 2 },
  "vary":  { "author": ["single-agent", "specialists", "concurrent"] },
  "repeats": 5,
  "metrics": ["factRecall", "unsupportedClaims", "checks", "variance", "tokens", "cost", "duration"]
}
```

Output: a Markdown report with one row per variant (mean, std-dev, cost), per-case details, and a diff against `evals/baselines/main.json`. **Not in CI on every PR** (cost and time): add a manual `workflow_dispatch` GitHub Action.

**Baseline:** the default strategy (`rules` / `single-agent` / `tool`) on dataset v1, committed by 4 Dec. Every later change is compared with it.

### Experiment note template — `docs/experiments/E<n>-<name>.md`

```markdown
# E<n> — <question>
**Owner:** … · **Date:** … · **Report:** evals/reports/…

## Hypothesis (written before running)
## Setup — dataset, fixed settings, variants, repeats, model versions, prompt versions
## Results — table: variant × metrics (mean ± std-dev), cost
## What we learned — 3–5 plain sentences
## Decision — what becomes the default, and why
## Pattern to reuse — the reusable idea, in one paragraph, for the pattern catalog
```

### E5 (should) — where do we need the big model?
With Stevcho: `chat-main` everywhere vs `chat-mini` for specialists vs a different model for the Reviewer. Quality vs cost, using the same E2 setup.

---

## Week plan

| Week | Do | Done when |
|---|---|---|
| **W2** 2–6 Nov | Research brief (above) alongside setup Phases 4–7. ADR 0003 drafted. | Design note reviewed Fri 6 Nov |
| **W3** 9–13 Nov | `AddDocAgentTelemetry()` in ServiceDefaults with the AI sources. Root span and strategy tags in `RunWorker`. Usage → cost. `IContentScreen` real, injection pack blocked. Harness skeleton runs on fakes. | One real run (Boban's `single-agent`) = one coherent trace; `Safety = false` locally |
| **W4** 16–20 Nov | Traces across tools (Riste) and retrieval (Todor). Harness v1 on Darko's 3 seed cases: deterministic metrics + judge. First report. | **Checkpoint "It's real"**: a scored report on a real run |
| **W5** 23–27 Nov | M.E.AI.Evaluation quality + safety evaluators. Variance (5 repeats). Dataset to 10+ cases (everyone adds one). Experiment runner with the JSON format. E4 reports for Trajche and Todor. | Experiment runner used by someone other than you |
| **W6** 30 Nov–4 Dec | Baseline committed. Run E1 (Trajche), E2 (Stevcho), E3 (Todor), E7 (Riste) with their owners. Compare view in the UI (should). | **Checkpoint "It's smart"**: reports for E1–E3, E7 |
| **W7** 7–11 Dec | Cloud telemetry: App Insights + Foundry tracing on the deployed app. One App Insights workbook: runs by strategy, cost, failures. E5 (should). Demo: walk through one trace. | Demo |

---

## Definition of done (11 Dec)

- [ ] One run = one coherent trace across API, worker, pipeline, agents, tools and retrieval, with the strategy on the root span
- [ ] Prompt name + version on every agent span
- [ ] Tokens and cost per run in the database and the UI
- [ ] ADR 0003 decided; cloud traces in App Insights / Foundry (and Langfuse if chosen)
- [ ] `IContentScreen` real; the injection pack is always blocked; `Safety = false` shared default
- [ ] Harness with ≥15 cases, deterministic + judge + Foundry-backed metrics, variance
- [ ] Experiment runner; baseline committed; reports for E1, E2, E3, E4, E7

---

## Pitfalls

- **Prompt text in shared traces.** Sensitive-data capture is local-only.
- **gRPC exporter to Langfuse.** It won't connect; use HTTP/protobuf.
- **Broken trace context across the channel** between the API request and `RunWorker`. Make the run span a new root linked to the request, deliberately.
- **Trusting the judge blindly.** Hand-check a sample whenever its prompt changes.
- **Comparing runs with different model versions** without noticing. Record model and prompt versions in every report.
- **A dataset that's too easy.** Include data-only, noisy and unknown packs, and cases where the first versions got things wrong.
