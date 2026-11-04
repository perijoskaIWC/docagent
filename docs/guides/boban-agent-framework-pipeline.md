# Boban — Microsoft Agent Framework: the Run Pipeline and the Baseline Author

**You build the spine of the product on Microsoft Agent Framework (MAF).** The run pipeline is a MAF **workflow** of deterministic steps that calls everyone else's contracts in order. Inside it, the Author step runs agents. You build the shared pieces every agent uses, and the first, simplest agent architecture: one agent with tools. Stevcho builds the multi-agent architectures on top of your building blocks and compares them all in experiment E2.

Read `00-team-overview.md` first, especially §3 (architecture and principles), §4 (experiments) and §6 (contracts, flags, strategy keys). Then read `stevcho-agent-framework-orchestrations.md`: you two share one design note and one project.

---

## Your mission and what you'll learn

| You deliver | You learn |
|---|---|
| `IRunPipeline`: a MAF workflow Screen → Profile → Detect → Enrich → Author → Check → Render, with progress events | **MAF Workflows**: executors, typed messages, edges, events, `InProcessExecution`; mixing code steps and agent steps in one graph |
| The shared agent building blocks: agent factory, model overview, `submit_part` tool, knowledge binding, usage meter, MCP tool loading | **MAF agents** over Foundry models, **tools** as `AIFunction`s, structured output, run-scoped tools, MCP as a tool source |
| The `single-agent` author: one agent writes all sections through tools and a `submit_part` tool, no separate reviewer | How far one well-equipped agent gets, and where it fails (this is E2's baseline) |
| Human-in-the-loop pause after detection when confidence is low (should) | **Request/response in workflows**: pausing for external input and resuming |
| OpenTelemetry on every agent (with Elena) | GenAI spans, what they show, what they cost |

---

## How Boban and Stevcho split `DocAgent.Agents`

| Area | Boban | Stevcho |
|---|---|---|
| `Pipeline/`: `RunPipelineWorkflow`, executors, progress mapping | ✔ | |
| `Shared/`: `AgentFactory`, `ModelOverview`, `SubmitPartTool`, `SectionContextBinder`, `UsageMeter`, `ToolSource` (in-process / MCP) | ✔ | uses |
| `Authors/SingleAgentAuthor` | ✔ | |
| `Shared/ReviewerAgent`, `Shared/LoopGuard` | | ✔ |
| `Authors/SpecialistsAuthor`, `ConcurrentAuthor`, `GroupChatAuthor`, `MagenticAuthor` | | ✔ |
| `Prompts/*.md` (author, specialist, reviewer, editor) | `author.md` draft | owner of all prompts |
| `AddDocAgentPipeline`, `AddDocAgentAuthors` registration | ✔ (file), registers `single-agent` | registers the rest |
| `docs/design/agents.md` | co-written | co-written |
| Experiment E2 | supports (runs the baseline) | owner |

Rule: **the building blocks are shared so that E2 compares architectures, not incidental code.** If Stevcho needs a change in a shared block, he asks; you make it (or agree he does) so both of you keep using the same one.

---

## Research brief (now → Thu 5 Nov)

Answer these in the shared `docs/design/agents.md` (your half), with links to the docs you used. Sources: Microsoft Learn, section *Agent Framework* (`learn.microsoft.com/agent-framework`: *Agents*, *Workflows*, *Tools*, *Observability*); the `microsoft/agent-framework` GitHub repo, `dotnet/samples`; Microsoft.Extensions.AI docs for `AIFunctionFactory` and `ChatResponseFormat`.

**Agents and tools**
1. How is an agent created over an Azure OpenAI deployment in the current .NET packages (`Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI`)? Which method turns a `ChatClient` into an agent, and how are `instructions`, `name` and `tools` passed? Which packages are stable and which are preview?
2. How are tools defined (`AIFunctionFactory.Create` on methods with `[Description]`) and how are **run-scoped** tools passed (per run, not per agent singleton)? What happens with tool-call errors: does the framework feed the exception message back to the model?
3. **Structured output**: how do we force JSON that matches a C# record (`ChatResponseFormat.ForJsonSchema<T>()` in Microsoft.Extensions.AI, or a typed run method in MAF)? What does the model return on failure?
4. How are `InputTokenCount` / `OutputTokenCount` read from an agent run, and are tool calls counted anywhere?
5. **MCP as a tool source**: with the C# MCP SDK (`ModelContextProtocol`), how does a client connect to a Streamable HTTP server, list tools, and get them as `AIFunction`s usable by a MAF agent? (Riste hosts the server; you consume it.)

**Workflows**
6. In `Microsoft.Agents.AI.Workflows`: how is an **executor** defined in the installed version (base class, handler signature), how are typed messages routed, how are edges and conditional edges declared, and how is the workflow's final output produced? What has changed between 1.0 and the current version (the docs and samples differ by version)?
7. How are workflow **events** consumed from `InProcessExecution` (streaming the run), and which events exist (executor started/completed, agent updates, errors)? You'll map them to `RunProgress`.
8. **Human in the loop**: how does a workflow pause for external input and resume (request/response, checkpointing)? What state must we store to resume after the user confirms a detection in the UI?
9. **Observability**: what does `UseOpenTelemetry` on the agent/chat client emit, which `ActivitySource` names, and what does `EnableSensitiveData` add? (Elena needs the exact names.)

**Spike** (one evening): a scratch console app, not in the repo, against our Foundry deployments (after you pass Darko's smoke test):
- one agent on `chat-mini` with one tool (`get_time`) and structured output into a C# record;
- a three-executor workflow: code step → agent step → code step, with typed messages, run via `InProcessExecution`, events printed as they arrive;
- the same agent with its tool coming from an MCP client connected to any public or local MCP server (Riste can give you an early endpoint, or use a stdio sample server from the SDK repo).

**Design note (your half):** pipeline executors and their messages; how `RunStrategy` keys resolve to services; the shared blocks' interfaces; how tools are scoped per run; how progress events map; the HITL design; the span names you'll emit; package versions.

---

## What you build

### 1. The run pipeline (W3)

A MAF workflow of deterministic executors. Only the Author executor contains agents (and it delegates to an `IDesignAuthor`).

```
DesignRequest
   │
   ▼
 Screen ──► Profile ──► Detect ──► Enrich ──► Author ──► Check ──► Render ──► DesignResult
 IContent   IExport     ISystem    IExport    IDesign    IDesign   IDesign
 Screen     Profiler    Detector   Profiler   Author     Check(s)  Renderer
 (Elena)    (Riste)     [key]      .Enrich    [key]      (Riste)   (Riste)
                        (Trajche)  (Riste)    (you,
                                              Stevcho)
```

Behaviour per executor:

| Executor | Does | On failure |
|---|---|---|
| **Screen** | `IContentScreen.ScreenAsync(pack)`. If `Allowed = false` → stop; the result's message lists the flags | Run fails with a clear message, never "exception" |
| **Profile** | `IExportProfiler.ProfileAsync(pack)` → `SystemModel` | A profiler exception fails the run; parse problems are findings, not exceptions (Riste's rule) |
| **Detect** | If `request.SystemOverride` is set → `SystemDetection(systemId, name, 1.0, ["user override"], [], "override")`. Otherwise resolve `ISystemDetector` by `request.Strategy.Detector` (`GetRequiredKeyedService`) and call it | Unknown key → fail fast with the list of registered keys |
| **Enrich** | `IExportProfiler.EnrichAsync(model, pack, detection)` | — |
| **Author** | Resolve `IDesignAuthor` by `request.Strategy.Author`; build `AuthoringContext(request, template, model, detection)` (template from `ITemplateProvider`); call it; forward its progress | — |
| **Check** | Run every registered `IDesignCheck` on the design; append the `CheckResult`s to the last `ReviewResult` (or create one if the author produced none) | — |
| **Render** | `IDesignRenderer.ToMarkdown` is called by Elena's `RunWorker` after the result; your executor only assembles `DesignResult` (strategy, screening, model, detection, design, reviews, usage) | — |

- Messages between executors are your own internal records (`ScreenedPack`, `ProfiledPack`, `DetectedPack`, …) that carry the request along.
- Report `RunProgress` at the start of every executor (`Screening`, `Profiling`, `Detecting`, `Drafting`, `Reviewing`, `Rendering`) through the `IProgress<RunProgress>` the API passes in. Author progress (per section, per iteration) is forwarded from the author.
- Wrap each executor in an `Activity` (`DocAgent.Pipeline` source) with the attributes from Elena's span model (`detect.system_id`, `detect.confidence`, `model.entities`, …).
- Register `IRunPipeline` in `AddDocAgentPipeline`. Flip `Features:Fakes:Pipeline=false` in your local config first.

**Should (W6): human in the loop.** When `detection.Confidence < Strategy:DetectionConfirmThreshold` (config, default 0.7) and no override is set, the workflow pauses with a request for confirmation. The API stores the pending state and the UI shows "We think this is Helio Service (58%). Confirm or pick another." The user's answer resumes the run. Design it with Elena (API/UI) and use MAF's request/response mechanism from research question 8; if resuming a paused workflow turns out to be hard in the installed version, the fallback is: fail the run with status `NeedsConfirmation` and let the UI rerun with `SystemOverride`. Either way, the behaviour is the same for the user.

### 2. Shared building blocks (W3, first)

| Block | What it does | Notes |
|---|---|---|
| `AgentFactory` | `Create(role, modelAlias, promptName, tools)` → a MAF agent over the Foundry deployment for the alias, instructions from the prompt file + optional suffix, tools attached, OpenTelemetry enabled. `AzureOpenAIClient` and `TokenCredential` are singletons; agents are created per run because tools are run-scoped | `name` = role (e.g. `Specialist.data-model`) so spans are readable |
| `ModelOverview` | Compact text of the `SystemModel`: file list with kinds; entities with field counts and row counts; interfaces; schedules; finding counts by level; the detection with its evidence. Target ≤ 1,500 tokens; truncate lists with "… and N more; use list_entities" | Never the full model. Detail comes through tools |
| `SubmitPartTool` | `submit_part(sectionId, partJson)`: parses JSON into `DesignPart`, runs `IDesignPartValidator` with a `ValidationContext` (the model + the chunk ids retrieved so far in this run), and returns "accepted" or the issue list for the agent to fix. Stores accepted parts per section (the last accepted wins) | This is how agents return structured output. Cap retries per section (3) and then accept the last submission with the issues attached as review issues |
| `SectionContextBinder` | For a section: calls the keyed `ISectionContextProvider` (`request.Strategy.Retrieval`) → `SectionContext`; renders `Preloaded` chunks into the section message (title, heading path, content, `chunkId` for citing); if `OfferSearchTool`, adds Riste's `search_knowledge` tool to that section's tool list; records the chunk ids it exposed, so the validator can accept them as evidence | The only place agents meet knowledge. Todor owns what comes back |
| `UsageMeter` | Sums input/output tokens, model calls and tool calls from every agent response in a run → `RunUsage` (cost filled by Elena's price table) | Thread-safe: Stevcho's `concurrent` author calls it from parallel tasks |
| `ToolSource` | `Agents:ToolSource = inprocess | mcp`. `inprocess`: Riste's `ModelTools(model, pack).AsTools()`. `mcp`: connect an MCP client to the API's `/mcp` once per run, list tools, pass the `runId` as a bound parameter (agree the mechanism with Riste: either tools take `runId` and you pre-fill it via a wrapper `AIFunction`, or the server scopes by a header) | This switch is Riste's experiment E7; you make it possible |
| `Prompts.Load(name)` | Loads `Prompts/<name>.md` (embedded resource), returns text + version from its header | Stevcho owns the content |

### 3. The `single-agent` author (W3–W4)

One agent (`chat-main`), instructions from `Prompts/author.md` + the template's full section list with guidance, tools = drill-down tools + `submit_part` (+ `search_knowledge` if the retrieval mode offers it). The first message is the model overview, the brief and the target. The agent works through the sections in template order and submits each part. No separate reviewer: the deterministic validator is the only gate; the single `ReviewResult` is built from the final validator state (`Approved = no Critical issues`, `Score` = share of sections accepted on the first try).

- Hard limits: max tool calls per run (config, default 60), max submissions per section (3), run timeout (config, default 6 min). Code enforces them; the model is never asked to stop itself.
- On 429 from the deployment: retry with backoff (the Azure SDK's retry policy covers most of it; set `MaxRetries` and a sensible delay).
- Report `RunProgress(Drafting, "Section 3/8: data-model")` as sections are submitted.
- Register as keyed `IDesignAuthor` `"single-agent"` with `StrategyOption("author", "single-agent", "One agent with tools writes every section; validator only", IsFake: false)`.

Why this one first: it's the baseline Stevcho compares against, it exercises every shared block, and it's what the 20 Nov checkpoint needs.

### 4. Code shapes

> Names match the MAF docs as of October 2026. **If a method doesn't exist in the version you installed, check the current docs or the `microsoft/agent-framework` samples. Don't let a coding agent invent an API.**

```bash
dotnet add src/DocAgent.Agents package Microsoft.Agents.AI
dotnet add src/DocAgent.Agents package Microsoft.Agents.AI.OpenAI
dotnet add src/DocAgent.Agents package Microsoft.Agents.AI.Workflows
dotnet add src/DocAgent.Agents package Azure.AI.OpenAI
dotnet add src/DocAgent.Agents package Azure.Identity
dotnet add src/DocAgent.Agents package ModelContextProtocol        # MCP client
```

```csharp
// AgentFactory (shape)
var openAi = new AzureOpenAIClient(new Uri(config["Foundry:Endpoint"]!), new DefaultAzureCredential());

AIAgent agent = openAi
    .GetChatClient(config[$"Foundry:{aliasKey}"]!)           // "chat-main" / "chat-mini"
    .AsAIAgent(name: role, instructions: Prompts.Load(promptName).Text + suffix, tools: tools);
// OpenTelemetry: wrap the chat client / agent with .AsBuilder().UseOpenTelemetry(...) per Elena's design note.
```

```csharp
// A tool the agent calls to return structured output
public sealed class SubmitPartTool(SectionSpec spec, IDesignPartValidator validator, ValidationContext ctx)
{
    public DesignPart? Accepted { get; private set; }
    public IReadOnlyList<ValidationIssue> LastIssues { get; private set; } = [];

    [Description("Submit the finished part for the current section as JSON matching the DesignPart schema. Returns 'accepted' or a list of problems to fix.")]
    public string SubmitPart([Description("Section id, e.g. 'data-model'")] string sectionId,
                             [Description("The DesignPart as JSON")] string partJson)
    {
        // parse → validate → store or return issues (shape only)
        return "accepted";
    }
}
```

```csharp
// Pipeline graph (shape; executor base class and output declaration per the installed version)
var workflow = new WorkflowBuilder(screen)
    .AddEdge(screen, profile)
    .AddEdge(profile, detect)
    .AddEdge(detect, enrich)
    .AddEdge(enrich, author)
    .AddEdge(author, check)
    .AddEdge(check, assemble)
    .Build();
// run with InProcessExecution, consume events, map to RunProgress
```

```csharp
// MCP tools for an agent (shape)
var mcp = await McpClient.CreateAsync(new HttpClientTransport(new() { Endpoint = new Uri(apiBase + "/mcp") }));
IList<McpClientTool> tools = await mcp.ListToolsAsync();      // McpClientTool derives from AIFunction
```

### 5. Telemetry you emit (agree names with Elena)

`docagent.pipeline.<step>` spans with: `detect.system_id`, `detect.confidence`, `detect.strategy`, `model.entities`, `model.findings`, `author.key`, `section.id`, `iteration`, `prompt.name`, `prompt.version`, `usage.*`. The MAF spans (`invoke_agent`, `execute_tool`) come from `UseOpenTelemetry`.

---

## Week plan

| Week | Do | Done when |
|---|---|---|
| **W1** 28–30 Oct | Read 00 and both MAF guides. Install .NET 10, Aspire templates, Azure CLI. Pass the Foundry smoke test. Start the research questions | — |
| **W2** 2–6 Nov | Spike (above). Co-write `docs/design/agents.md` with Stevcho. Agree the tool-passing mechanism with Riste, the `SectionContext` rendering with Todor, span names with Elena | Design note reviewed Fri 6 Nov |
| **W3** 9–13 Nov | Shared blocks. `RunPipelineWorkflow` on fakes behind it. `single-agent` author v1 writing `system-assessment` on Foundry from a `quarry-wms` pack, tools in-process | `Pipeline=false`, `Authoring=false` locally; a real design from a real pack |
| **W4** 16–20 Nov | Tools over MCP (`ToolSource=mcp`). Real profiler (Riste), real detector (Trajche). Limits, retries, usage. All `quarry-wms` and `helio-service` packs | **Checkpoint "It's real"** |
| **W5** 23–27 Nov | `SectionContextBinder` with Todor's `prestep` and `tool` providers. `integration-design` runs. Help Stevcho integrate `specialists` | Both retrieval modes work in `single-agent` |
| **W6** 30 Nov–4 Dec | HITL pause (should). Run E2's baseline (5 repeats per case) with Elena's runner. Hardening from E2 findings | **Checkpoint "It's smart"** |
| **W7** 7–11 Dec | Timeouts, large packs, clear failure messages. Architecture document section on the pipeline. Demo script for one run end to end | Demo |

---

## Definition of done (11 Dec)

- [ ] `Pipeline = false` and `Authoring = false` are the shared defaults
- [ ] Pipeline runs Screen → Profile → Detect → Enrich → Author → Check → assemble on Foundry, with progress and spans per step
- [ ] `SystemOverride` and unknown strategy keys handled
- [ ] Shared blocks used by every author; `ToolSource` switch works for `inprocess` and `mcp`
- [ ] `single-agent` author registered with its `StrategyOption`; limits enforced by code
- [ ] Usage recorded per run
- [ ] Unit tests for the pipeline on fakes and for `SubmitPartTool` with a fake `IChatClient`
- [ ] HITL pause or its fallback (should)

---

## If you have time left (write it in the design note first)

- **Checkpointing** the workflow so a failed run can resume at the Author step without re-profiling.
- A `parallel-sections` option inside `single-agent`: the same single prompt, but sections drafted concurrently with a shared `SubmitPartTool`; compare with Stevcho's `concurrent`.
- **Foundry Agent Service** for the `single-agent` author (hosted agent with our MCP server as its toolbox) once the app is deployed; compare latency and the portal view with the in-process version.
- Agent **middleware** (MAF supports wrapping tool calls): log every tool call with its arguments size and duration.

---

## Pitfalls

- **Letting the model count or list structure** the profiler already has. Give it ids and tools.
- **Tools as singletons.** They must close over this run's model and pack.
- **Unbounded runs.** Limits live in code: tool calls, submissions, time.
- **Different blocks for different authors.** Then E2 measures code differences, not architectures.
- **Executor APIs from an older MAF version** pasted by a coding agent. Check the installed version's docs.
- **Prompt text in shared traces.** `EnableSensitiveData` only in local development.
