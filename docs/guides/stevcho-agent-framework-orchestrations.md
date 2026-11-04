# Stevcho — Microsoft Agent Framework: Multi-Agent Orchestrations, Prompts and Experiment E2

**You build and compare the agent architectures.** Boban builds the pipeline, the shared agent building blocks and the baseline (`single-agent`). You build the Reviewer agent and the bounded review loop, then three more ways of arranging agents around the same task: specialists with a reviewer, specialists in parallel with an editor, and MAF's built-in group chat. You own every prompt. Then you run experiment **E2** and tell the team, with numbers, which architecture writes the best designs at what cost and how stable it is.

Read `00-team-overview.md` first, especially §3 (principles), §4 (experiments) and §6 (contracts, strategy keys). Then `boban-agent-framework-pipeline.md`: you two share one design note and one project, and the split table there applies.

---

## Your mission and what you'll learn

| You deliver | You learn |
|---|---|
| `ReviewerAgent` with schema-validated structured output, and `LoopGuard` | **Structured output** that holds up in practice; designing a critic that neither rubber-stamps nor blocks everything |
| `specialists` author: one specialist agent per section, each part validated by code and reviewed, rejected parts go back, bounded loop | **Custom MAF workflow graphs**: conditional edges, cycles, fan-out by section, typed messages |
| `concurrent` author: specialists in parallel, an Editor agent makes the parts consistent | **MAF `BuildConcurrent` / fan-out–fan-in**, concurrency against rate limits |
| `group-chat` author (should): Writer and Reviewer in MAF's round-robin group chat | **MAF built-in orchestrations** and when a built-in beats a hand-built graph |
| `magentic` author (stretch) | **Planner-manager orchestration** (experimental in MAF) |
| All prompts, versioned, every change backed by an eval number | **Prompt engineering as engineering**: versions, eval-backed changes, the difference between a prompt and an architecture |
| Experiment E2: report + note | How to run a fair comparison, including **run-to-run variance** |

---

## Research brief (now → Thu 5 Nov)

Answer these in `docs/design/agents.md` (your half), with links. Sources: Microsoft Learn *Agent Framework* → *Workflows → Orchestrations* (sequential, concurrent, group chat, handoff, magentic) and *Workflows → Executors / Edges*; the `microsoft/agent-framework` samples; Microsoft.Extensions.AI docs on `ChatResponseFormat`; Microsoft Learn on Azure OpenAI structured outputs and which models support `json_schema`.

**Orchestration patterns**
1. For each built-in pattern in `Microsoft.Agents.AI.Workflows` (`AgentWorkflowBuilder.BuildSequential`, `BuildConcurrent`, `CreateGroupChatBuilderWith`, `CreateHandoffBuilderWith`, `MagenticWorkflowBuilder`): what does it do, what does the output look like, how is termination bounded, and what is its status in the installed version (stable / experimental warning `MAAIW001`)?
2. In a custom `WorkflowBuilder` graph: how do you fan out one message per section and fan in the results? How does a **conditional edge** with a loop back work, and how do you make sure a loop terminates (hint: the condition reads a counter in the message, never a model decision)?
3. How does the default **concurrent** aggregator combine results, and when do we need our own fan-in executor instead?
4. For **group chat**: how does `RoundRobinGroupChatManager` decide turns and stop (`MaximumIterationCount`), and how would a custom manager end the chat on "approved"?

**Reviewer and prompts**
5. Which of our deployed models support **strict JSON schema** output, and what are the schema restrictions (no optional properties? `additionalProperties: false`?) that affect how the `ReviewResult` schema must be written?
6. What is known about **LLM-as-judge / reviewer bias**: same-model blind spots, position bias, verbosity bias? What does that imply for using `chat-main` as both author and reviewer (and for E5 with Elena)?
7. How do we measure **run-to-run variance** of a generated design: which statistics, how many repeats, how to compare evidence sets (Jaccard of evidence refs) and counts (statements, open questions)? Agree the method with Elena.

**Spike** (one evening, after Darko's smoke test), scratch console, not in the repo:
- a Writer → Reviewer loop on a toy task (write a product description from a fact list; reviewer returns JSON `{approved, issues[]}`; loop until approved or 3 rounds) built with a custom `WorkflowBuilder` graph with a conditional edge;
- the same task with `AgentWorkflowBuilder.CreateGroupChatBuilderWith(...)` and `MaximumIterationCount = 4`;
- three agents in `BuildConcurrent` writing three paragraphs in parallel. Note the differences: control, output shape, token usage.

**Design note (your half):** the `ReviewResult` JSON schema; the `specialists` graph with messages and the loop rule; the `concurrent` fan-out/fan-in and the Editor's job; `group-chat` manager settings; prompt inventory with the rules each prompt enforces; the **E2 hypothesis** (written before any run) and the E2 setup; package versions.

---

## What you build

### 1. `ReviewerAgent` and `LoopGuard` (W5, first)

**ReviewerAgent** (`chat-main`, low temperature, `Prompts/reviewer.md`): input = the section spec (guidance, tables), the submitted `DesignPart`, the model overview and the drill-down tools (so it can spot-check evidence). Output = JSON matching an internal `ReviewSchema` record → mapped to `ReviewResult`. Validate the JSON; on failure, retry once with the validation error in the message; on a second failure, return `Approved = false` with one Critical issue "reviewer output invalid" (never crash the run).

What the Reviewer checks (put this in the prompt, in this order):
- Required content for the section's guidance is present.
- Every `Evidenced` statement is actually supported by the cited model element or chunk (spot-check with `get_entity`, `read_source`, or the chunk text).
- Coverage: entities, interfaces and Warning/Error findings that belong in this section are mentioned.
- No design decision contradicts the knowledge the section was given.
- Severity: **Critical** = wrong or invented; **Major** = missing required content; **Minor** = clarity; **Info** = style. **Approve when no Critical or Major remains.** `Score` = calibrated 0–1.

**LoopGuard**: `CanRetry(iteration) = iteration < request.Strategy.MaxReviewLoops`. Code decides when to stop, never the model. The guard also caps total agent calls per run (config) so a pathological case can't burn the budget.

### 2. The `specialists` author (W5)

```
AuthoringContext
   │  dispatch: one SectionJob per section, in template order
   ▼
 ┌────────────┐   SectionDraft    ┌──────────┐  approved or !CanRetry   ┌─────────┐
 │ Specialist │ ────────────────► │ Reviewer │ ───────────────────────► │ Collect │ → AuthoringResult
 │ (per sec.) │                   │          │                          └─────────┘
 └────────────┘ ◄──────────────── └──────────┘
                 rejected & CanRetry (issues attached, iteration + 1)
```

- **Specialist** = `AgentFactory.Create("Specialist.<sectionId>", "chat-main", "specialist", tools)` with the section's guidance appended to the prompt, Boban's `SubmitPartTool` for that section, drill-down tools, and knowledge bound by `SectionContextBinder`. On revision it receives its previous part and the issues, and changes only what was flagged.
- **Messages**: `SectionJob(ctx, section, iteration, issues)`, `SectionDraft(job, part)`, `SectionVerdict(draft, review, canRetry)`; all internal records.
- Sections run **sequentially** in this author (that's the point: compare with `concurrent`). Each section's review feeds only that section.
- `AuthoringResult.Reviews` = one `ReviewResult` per iteration per section, merged per iteration (so the UI shows round 1, round 2 …). `UsageMeter` sums everything.
- Register as keyed `IDesignAuthor` `"specialists"` with `StrategyOption("author", "specialists", "One specialist per section, deterministic validation, Reviewer agent, bounded loop", false)`.

```csharp
// Shape; executor base class and output declaration per the installed MAF version
var workflow = new WorkflowBuilder(dispatch)
    .AddEdge(dispatch, specialist)
    .AddEdge(specialist, review)
    .AddEdge<SectionVerdict>(review, specialist, v => v is { Review.Approved: false, CanRetry: true })
    .AddEdge<SectionVerdict>(review, collect,    v => v is { Review.Approved: true } or { CanRetry: false })
    .Build();
```

### 3. The `concurrent` author (W6)

Fan out one specialist per section **in parallel** (`AgentWorkflowBuilder.BuildConcurrent` if its aggregator fits, otherwise a fan-out/fan-in pair of executors in `WorkflowBuilder`), each submitting its part through its own `SubmitPartTool`. Then an **Editor** agent (`chat-main`, `Prompts/editor.md`) receives all accepted parts and may: rename things consistently, remove overlaps between sections, merge duplicate open questions, and flag contradictions; it returns the edited parts through `submit_part` too (so the validator still runs). Then **one** Reviewer pass over the whole design (Reviewer with all parts; issues keyed by section) and **one** revision round for flagged sections, parallel again. Cap parallelism (config, default 4) and handle 429s with retry and backoff; measure how much wall-clock it saves and what it costs.

### 4. The `group-chat` author (should, W6)

MAF's built-in **round-robin group chat** with two participants: a Writer (all sections, like `single-agent`, still submitting through `submit_part` so the validator always runs) and the Reviewer, `MaximumIterationCount = 2 × MaxReviewLoops + 1`. A custom manager subclass ends the chat when the Reviewer's last message says approved. The comparison you want: does the framework's built-in loop produce what your hand-built loop produces, and at what cost in control and tokens?

### 5. `magentic` author (stretch)

`MagenticWorkflowBuilder` with a manager agent and three participants: an Analyst (model tools only), a Researcher (`search_knowledge` only), a Writer (`submit_part` only). `WithMaxRounds`, `WithMaxStalls`, `RequirePlanSignoff(false)`. Experimental API (`MAAIW001`); try it only after the three must-have authors run.

### 6. Prompts (you own them all)

Versioned Markdown in `src/DocAgent.Agents/Prompts/`, one per role, with a header:

```markdown
<!-- prompt: specialist | version: 4 | changed: 2026-11-24 | why: tables must use model field ids in the Source column -->
```

Elena records prompt name and version on every agent span. **Bump the version on every change**, and never change a prompt without an eval run before and after.

| Prompt | Used by | Must enforce |
|---|---|---|
| `author.md` | `single-agent`, `group-chat` Writer | the rules below, for all sections |
| `specialist.md` | `specialists`, `concurrent` | the rules below, for one section |
| `reviewer.md` | all authors with a Reviewer | the checks in §1 |
| `editor.md` | `concurrent` | consistency across parts; never add facts |
| `manager.md` | `magentic` | plan → delegate → verify; stop when all sections are accepted |

Rules every author prompt must enforce:
- Work only from the model overview, the tools and the provided knowledge. **Never invent** entities, fields, systems, volumes or numbers.
- Every statement is `Evidenced` (with `EvidenceRef`s to model ids or chunk ids), `Assumption`, or `Unknown` with a matching open question.
- System-specific claims need `SystemProfile` knowledge as evidence; otherwise they are assumptions.
- Tables follow the `TableSpec` columns exactly; source columns hold model ids.
- Submit through `submit_part`; fix every validation issue before moving on.
- Address the user's brief and target explicitly in the first section.
- On revision, change only what the review flagged.

### 7. Experiment E2 (W6)

With Elena's experiment runner, on the eval dataset (full and noisy packs, `system-assessment` and `integration-design`):

- Fixed: `detector = rules`, `retrieval = tool`, `lessons = false`, `maxReviewLoops = 2`, same prompt versions.
- Vary: `author ∈ {single-agent, specialists, concurrent}` (+ `group-chat` if it exists).
- **Five repeats** per case per variant.
- Metrics: fact recall, unsupported claims, check pass rate, entity coverage, **variance** (std-dev of fact recall; Jaccard of evidence sets; spread of statement and open-question counts), tokens, model calls, tool calls, latency, cost.
- Write `docs/experiments/E2-agent-architecture.md`: hypothesis (before), setup, results table (mean ± std-dev per variant), what we learned, decision (which author becomes the default), the pattern to reuse.

Hypothesis to write down in W2, in your words. Example: "Specialists with a reviewer raise fact recall by ≥10 points over the single agent and halve variance, at ≤2× the tokens; concurrent matches specialists on quality at ~half the wall-clock time."

---

## Week plan

| Week | Do | Done when |
|---|---|---|
| **W1** 28–30 Oct | Read 00 and both MAF guides. Tooling, smoke test. Start the research questions | — |
| **W2** 2–6 Nov | Spike (above). Co-write `docs/design/agents.md` with Boban. Draft `author.md`, `specialist.md`, `reviewer.md`. E2 hypothesis written | Design note reviewed Fri 6 Nov |
| **W3** 9–13 Nov | Pair with Boban on the shared blocks (you need `SubmitPartTool` and `AgentFactory`). `ReviewSchema` + `ReviewerAgent` v1 tested on fake parts. Prompt iteration on `single-agent` outputs with Boban | Reviewer returns valid JSON on 20/20 fake parts |
| **W4** 16–20 Nov | `LoopGuard`. `specialists` author v1 on `system-assessment`, fakes for knowledge still fine | **Checkpoint "It's real"**: `specialists` runs end to end on one pack |
| **W5** 23–27 Nov | `specialists` hardened on all `quarry-wms` and `helio-service` packs; `integration-design` prompts; knowledge bound through Todor's providers. `concurrent` started | Loop runs 1–N iterations and always stops |
| **W6** 30 Nov–4 Dec | `concurrent` done. **Run E2** (5 repeats) with Elena. `group-chat` if time allows | **Checkpoint "It's smart"**: E2 report |
| **W7** 7–11 Dec | E2 experiment note. Prompt freeze (versions recorded). Demo script: the same pack through three authors, side by side. `magentic` only if everything else is done | Demo |

---

## Definition of done (11 Dec)

- [ ] `ReviewerAgent` schema-validated; malformed output handled; never crashes a run
- [ ] `specialists` and `concurrent` registered with `StrategyOption`s; `group-chat` (should)
- [ ] Review loop stops at approval or `MaxReviewLoops`, every time; unit test with a fake `IChatClient` proving it
- [ ] All prompts versioned in files; versions visible in traces; every prompt change has an eval before/after
- [ ] E2 report and experiment note committed; a default author decided and set in `Strategy:Default:Author`

---

## If you have time left (write it in the design note first)

- **A different Reviewer model** (E5 with Elena): `chat-mini` as reviewer, or a model from another family if the region offers one, to avoid same-model blind spots.
- **Self-consistency**: run the specialist twice and let the Reviewer pick; measure cost vs variance.
- **Handoff orchestration**: a triage agent that routes each section to a "data" specialist or an "integration" specialist by section kind; compare with per-section specialists.
- A **prompt regression suite**: 10 fixed sections with expected validator outcomes, run on every prompt change in CI (cheap, `chat-mini`).

---

## Pitfalls

- **A Reviewer that always approves** (or never does). Calibrate on the eval set, not on one pack; look at the issue distribution.
- **Rewriting whole parts on revision.** Quality oscillates. Revise only what was flagged.
- **Comparing architectures with different prompts or tools.** Then E2 measures prompts. Shared blocks, same prompt versions, same retrieval mode.
- **Concurrency without limits.** Eight parallel specialists hit the deployment's TPM. Cap it and back off.
- **Loop termination decided by the model.** It isn't. The counter is.
- **One sample.** Use at least three packs from the start, and five repeats for E2.
