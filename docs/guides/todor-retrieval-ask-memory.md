# Todor — RAG part 2: Retrieval, the Ask Chat and Memory

**You own how knowledge reaches the agents, the chat people talk to, and how the product gets better over time.** The retriever and its query modes, the retrieval strategies the authors use (E3), the streamed Ask chat with citations, and the lessons loop. You share the index and one design note with Trajche (`trajche-knowledge-and-detection.md`).

Read `00-team-overview.md` first, especially §4 (E3, E4, E6) and §6 (`IKnowledgeRetriever`, `ISectionContextProvider`, `IRunChat`, `Lesson`). Your consumers: Boban's `SectionContextBinder` (calls your providers), Riste's `search_knowledge` tool (hosts your behaviour), Elena's Ask endpoint (streams your deltas).

---

## Your mission and what you'll learn

| You deliver | You learn |
|---|---|
| `IKnowledgeRetriever` with four query modes (keyword, vector, hybrid, hybrid + semantic ranker) | **Azure AI Search querying**: BM25, vector queries, reciprocal rank fusion, the semantic ranker, OData filters; how to compare them (E4) |
| Four `ISectionContextProvider`s: `none`, `prestep`, `tool`, `agentic` (stretch) | **RAG architectures**: classic pre-retrieval vs agentic retrieval-as-a-tool vs the service's agentic retrieval; what each costs and when each fails (E3) |
| `IRunChat`: a streamed, tool-using MAF agent that answers about a run or the knowledge base with resolving citations | **Streaming agents**, grounding with citations, honesty rules; Server-Sent Events end to end with Elena |
| The lessons loop (should): extract → dedupe → store → retrieve; E6 | **Memory for LLM products**: what "self-improvement" can honestly mean and how to prove it |

---

## Research brief (now → Thu 5 Nov)

Answer these in `docs/design/knowledge-and-retrieval.md` (your half), with links. Sources: Microsoft Learn *Azure AI Search* (hybrid search, vector query, semantic ranker, filters, **agentic retrieval** overview and quickstart); Microsoft Learn *Agent Framework* (agents, streaming runs, tools); the `Azure.Search.Documents` .NET docs; Microsoft Learn *Foundry Agent Service* (hosted agents with MCP tools).

**Retrieval**
1. How does a **hybrid** query run in `Azure.Search.Documents` (keyword text + `VectorizedQuery` in one request), how does reciprocal rank fusion merge them, and how do you turn the **semantic ranker** on per query (`QueryType.Semantic`, configuration name)? Which score fields come back (`@search.score`, `@search.rerankerScore`), and why can't you compare scores across modes?
2. How do OData filters work with `search.in`, with string escaping, and with the `disabled`, `systemId`, `templateId`, `sectionId`, `chunking` fields from Trajche's schema?
3. **Agentic retrieval**: what are *knowledge bases* and *knowledge sources*, what is GA vs preview as of now (REST versions), how is it billed (retrieval tokens), does our tier support it, and is there .NET SDK support or do we call REST? Is it worth trying in W6 or not?
4. How do you measure retrieval quality (**hit@k**, **MRR**) and what makes a good retrieval test set?

**Ask chat**
5. In MAF: how do you run an agent **streaming** and receive text deltas (method and update types in the installed version)? How do tools behave mid-stream?
6. How do you make an agent **cite**: inline markers resolved after the fact vs structured output; what do production chat products do?
7. How does **Foundry Agent Service** host an agent with tools from an MCP server (toolbox), and what would it take to host the Ask agent there after deployment (W7 stretch)?

**Memory**
8. What patterns exist for "lessons" / "reflections" memory in agent systems (store generalized rules, retrieve by context), and how do teams avoid memory poisoning (dedupe, counts, approval, disable)?

**Spike** (one evening, after Darko's smoke test and Trajche's test index exists): the same 10 queries in all four modes against Trajche's test index, results side by side with notes on which wins and why; a MAF agent on `chat-mini` streaming an answer to the console with one tool call in the middle.

**Design note (your half):** query mode implementation; the `prestep` retrieval table (what, filters, top); context budget; `search_knowledge` behaviour; the Ask chat design (tools, context, streaming, citation format, honesty rules); the lessons loop design; the E3 hypothesis; the retrieval test set plan.

---

## Step 1 — The retriever and its query modes

`KnowledgeQuery.Mode` selects how a query runs, which is what E4 compares:

| Mode | What it sends to AI Search |
|---|---|
| `keyword` | BM25 full-text only |
| `vector` | Vector query only (embed the query text) |
| `hybrid` | Keyword + vector in one request (reciprocal rank fusion) |
| `hybrid-semantic` (default) | Hybrid, then the semantic ranker re-orders the top results |

```csharp
// Shape; verify against the current Azure.Search.Documents docs
var options = new SearchOptions
{
    Size = query.Top,
    Filter = BuildFilter(query),   // kinds, systemId/templateId/sectionId (or 'any'), chunking, disabled eq false
    VectorSearch = useVector ? new() { Queries = { new VectorizedQuery(vector) { KNearestNeighborsCount = 50, Fields = { "contentVector" } } } } : null,
    QueryType = semantic ? SearchQueryType.Semantic : SearchQueryType.Simple,
    SemanticSearch = semantic ? new() { SemanticConfigurationName = "default" } : null,
};
var results = await searchClient.SearchAsync<IndexChunk>(useKeyword ? query.Text : null, options, ct);
```

- **Always** filter `disabled eq false`. Use `search.in(kind, 'SystemProfile,Pattern', ',')` for several kinds. Escape user text in OData filters (single quotes).
- `Score` = reranker score when present, otherwise the search score. Scores from different modes are on different scales: **compare rankings, not raw scores.**
- Return `SourceUri` and `Title` so designs and answers can say where something came from.

---

## Step 2 — Retrieval strategies for authoring (E3)

The authors (Boban's and Stevcho's) call `ISectionContextProvider` once per section, keyed by `RunStrategy.Retrieval`. Your implementations decide **when and how** knowledge reaches the agent. The authors never query the index directly.

| Key | Behaviour | Teaches us |
|---|---|---|
| `none` | No knowledge. `Preloaded = []`, `OfferSearchTool = false` | The baseline: what the model does from the export alone |
| `prestep` | You build the queries and preload results into the prompt (table below). `OfferSearchTool = false` | Classic RAG: predictable, but may fetch what isn't needed |
| `tool` | Nothing preloaded; `OfferSearchTool = true`, and the agent calls `search_knowledge` when it decides to | Agentic RAG: cheaper when knowledge isn't needed, riskier when the agent forgets to look |
| `agentic` (stretch) | AI Search **agentic retrieval** (a knowledge base over our index): the service plans subqueries and returns merged, ranked content, which you preload | What a managed retrieval planner adds over our own queries |

**Starting point for `prestep`** (tune with evidence):

| What | Query text | Filter | Top |
|---|---|---|---|
| System profile | detected system name + section title | `kind = SystemProfile`, `systemId = <detected>` | 3 |
| Patterns | section title + guidance + brief + target | `kind = Pattern` (only when the section lists it) | 4 |
| Standards | section title + guidance | `kind = Standard`, `sectionId` in (section, any) | 2 |
| Exemplar | section title + template | `kind = Exemplar`, `templateId`, `sectionId` | 1 |
| Lessons (if `IncludeLessons`) | section title + detected system | `kind = Lesson`, `systemId` in (detected, any) | 5 |

**Context budget:** cap preloaded text per section (start at ~4k tokens), dedupe by `chunkId`, drop the lowest-ranked first.

**`search_knowledge` behaviour** (Riste hosts the tool; you define what it does): defaults `Mode = hybrid-semantic`, `Top = 6`; filters by the run's `systemId` when the agent asks for system knowledge; returns `chunkId`, title, heading path, content and source, so the agent can cite `chunkId` as `Knowledge` evidence; capped at ~6k tokens.

**About `agentic`:** as of October 2026, agentic retrieval is GA in the `2026-04-01` REST API for the basic ("minimal" reasoning, extractive) mode, while LLM query planning and answer synthesis are preview (`2026-08-01-preview`). It bills by retrieval tokens. Check that our Search tier supports it and whether the .NET SDK version you use exposes it (otherwise call REST). Only try it after `prestep` and `tool` work.

### Running E3
Same author and detector, cases from Elena's dataset, `none` vs `prestep` vs `tool` (vs `agentic`). Metrics: groundedness, recall of **knowledge-dependent** facts (Elena tags expected facts that can only come from the knowledge base, e.g. "uses a queue-based load-levelling pattern"), unsupported claims, tokens, number of searches. Hypothesis first.

### Running E4 (your half: query modes)
`evals/retrieval/queries.json`: ~25 queries, each with the chunk ids that *should* come back (build with Trajche once the corpus is in). Run every query mode × both chunkings. Report **hit@5** and **MRR**. Re-run whenever the corpus, chunking or embedding model changes.

---

## Step 3 — The Ask chat (`IRunChat`, streamed)

A MAF agent that answers questions about one run, or about the knowledge base when `runId` is null. This covers the Track 1 requirements "RAG Q&A" and "streaming".

- **Tools:** the drill-down tools for that run (from Riste, in-process or over MCP) and `search_knowledge`. No run → only `search_knowledge`.
- **Context:** the run's detection, design outline (section titles + statement counts) and the conversation history (from Elena's `ChatMessage` table).
- **Streaming:** use the agent's streaming run method (`RunStreamingAsync` in current MAF; check the update type name in your version) and yield a `ChatDelta` per text update. Respect the cancellation token: Elena's endpoint passes the request-aborted token, so a closed browser stops the model call.
- **Citations:** the prompt asks for inline markers like `[field:work_orders.status]` or `[chunk:pat-queue-load-levelling--2]`. At the end, parse the markers, keep only those that resolve (model ids via `IRunReader`, chunk ids returned by searches in this turn), and send them in the final `Done` delta. Unresolvable markers are removed from the text.
- **Honesty:** if the run and the knowledge base don't contain the answer, say so. No answers from general model knowledge presented as facts about the run.
- **Safety:** the model deployment's content filter with Prompt Shields applies to the user's question. Elena decides whether questions are also screened explicitly.

**Should — the Ask agent hosted in Foundry Agent Service.** The same agent, created in the Foundry project with our MCP endpoint as its toolbox. This needs the MCP server reachable from Foundry, so it's realistic only after deployment (W7). Compare it with the in-process agent on a few questions: latency, setup effort, what the Foundry portal shows.

---

## Step 4 — Lessons: how the product improves (should, E6)

Be clear about it in the demo: **the model doesn't learn; the product does.** We store general rules from past mistakes and give them to the authors next time. That is memory, not training, and it is measurable.

```
 Completed run
   ├─ Review issues that were FIXED between iterations (issue in round n, gone in n+1) ─┐
   └─ 👎 feedback with a comment on a section ────────────────────────────────────────────┤
                                                                                          ▼
                                                LessonExtractor (chat-mini): "generalise into 0–3 rules
                                                for this template section (and system, if specific)"
                                                                                          │
                                         dedupe against existing lessons (vector similarity ≥ ~0.9)
                                           ├─ near-duplicate → Count + 1
                                           └─ new → KnowledgeDocument(kind = Lesson) via IKnowledgeIngestor
                                                                                          │
 Next run, IncludeLessons = true ◄──── prestep retrieves top lessons for template + section + system
```

- Rules must be **general, imperative and checkable**: "In data-model, list relationships inferred from naming as assumptions, not facts." Never run-specific facts ("the key of orders is order_id").
- **Governance:** lessons are visible in the UI and can be disabled (`disabled = true`). A lesson from human feedback is used only once `Count ≥ 2` or after Darko approves, so one bad comment can't poison every run.
- **Trigger:** after a run completes, the `RunWorker` calls your extractor. Its failures never fail the run.
- **E6:** run the dataset with lessons off; run it once to accumulate lessons; run again with lessons on. Compare the recurrence of the same review issues and fact recall.

---

## Week plan

| Week | Do | Done when |
|---|---|---|
| **W1** 28–30 Oct | Read 00 and this guide. Tooling, smoke test. Start the research questions | — |
| **W2** 2–6 Nov | Spike (above). Design note with Trajche. Agree `SectionContext` rendering with Boban, `search_knowledge` hosting with Riste, the SSE contract with Elena | Design note reviewed Fri 6 Nov |
| **W3** 9–13 Nov | Real `IKnowledgeRetriever` with all four modes on Trajche's index. `/api/knowledge/search` returns real chunks. `none` and `tool` providers | Debug search works in the UI with a mode selector |
| **W4** 16–20 Nov | `prestep` provider. `search_knowledge` behaviour with Riste. Retrieval test set (25 queries) with Trajche | **Checkpoint "It's real"**: `single-agent` in `tool` mode searches knowledge in a real run |
| **W5** 23–27 Nov | **E4 (query half)** with Trajche and Elena's runner. Real `IRunChat`: streaming, tools, citations; Ask panel with Elena's `ChatPanel`. Lessons v1 extraction (should) | E4 report; Ask streams in the UI |
| **W6** 30 Nov–4 Dec | **Run E3** with Elena. `agentic` provider if the research says it's feasible. Lessons dedupe and injection | **Checkpoint "It's smart"**: E3 report |
| **W7** 7–11 Dec | E6 (should). Ask agent in Foundry Agent Service (should, after deployment). Experiment notes. Demo story for Ask and lessons | Demo |

---

## Definition of done (11 Dec)

- [ ] `IKnowledgeRetriever` real with four modes, filters and disabled-exclusion
- [ ] `none`, `prestep`, `tool` providers registered with `StrategyOption`s; `agentic` (stretch)
- [ ] `search_knowledge` returns citable chunk ids; used in real runs (visible in traces)
- [ ] `IRunChat` real: streamed, tool-using, with resolving citations; `Chat = false` shared default
- [ ] Retrieval test set with hit@5 / MRR; E4 report (with Trajche)
- [ ] E3 report and note
- [ ] Lessons extracted, deduped, retrievable, disable-able; E6 report (should)

---

## If you have time left (write it in the design note first)

- **Query rewriting**: let `chat-mini` rewrite the section query before `prestep` retrieval; measure hit@5 before/after.
- **Re-ranking by the model**: retrieve 20, let `chat-mini` pick 5; compare with the semantic ranker on E4's test set.
- **Conversation memory** for Ask: summarise long histories instead of truncating.
- **Answer synthesis** from agentic retrieval (preview) vs our own agent's answer: same questions, compare groundedness.

---

## Pitfalls

- **Retrieving too much.** More context isn't better designs. Let E3 tell you.
- **`tool` mode where the agent never searches.** Look at the traces; if needed, make the tool description say when to use it.
- **Lessons that are really facts** about one system. They're wrong for the next pack. Rules only.
- **No dedupe:** 40 copies of the same lesson drown everything else.
- **Unescaped filter values** break queries or allow injection.
- **Chat that answers from general knowledge.** If the run and the corpus don't say it, the answer says so.
