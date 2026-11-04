# Track 1 — DocAgent: Plan & Guides

DocAgent turns an export from a business system into an evidence-backed solution design: it detects the system, writes the design type the user picks, and answers questions about it. It is our lab for comparing agent architectures, RAG approaches and detection methods on the Microsoft stack.

**Everyone reads `00-team-overview.md` first, then their own guide.**

| File | For | Purpose |
|---|---|---|
| `00-team-overview.md` | **Everyone** | What we're building and why, architecture, experiments, who does what, contracts, timeline, scope, data rules, the research brief |
| `elena-1-project-setup.md` | Elena (W1–W2) | Phase-by-phase, agent-executable scaffold: solution, contracts, fakes, Aspire, Postgres, API, React, CI |
| `elena-2-observability-evals-safety.md` | Elena (W3–W7) | Telemetry, cost, Content Safety screening, eval harness, experiment runner, E5 |
| `boban-agent-framework-pipeline.md` | Boban | The run pipeline as a MAF workflow, shared agent building blocks, the `single-agent` author, tools in-process and over MCP, human-in-the-loop |
| `stevcho-agent-framework-orchestrations.md` | Stevcho | Reviewer and review loop, `specialists`, `concurrent`, `group-chat` authors, all prompts, E2 |
| `riste-tools-and-mcp.md` | Riste | Profiler and file extractors, drill-down tools, MCP server, templates, validators and checks, renderers, E7 |
| `trajche-knowledge-and-detection.md` | Trajche | Knowledge corpus, index, chunking, Document Intelligence, system detectors, E1, E4 |
| `todor-retrieval-ask-memory.md` | Todor | Query modes, retrieval strategies for authoring, streamed Ask chat, lessons, E3, E4, E6 |
| `darko-azure-and-lead.md` | Darko | Azure resources in the portal, access, costs, deployment; repo, contracts, test systems and synthetic packs, templates, eval cases, reviews, pattern catalog |

**These files belong in the repo** under `docs/guides/`, so every developer and every coding agent reads the same source. Elena copies them in during Phase 1.

**Using them with Claude Code or Copilot agent mode:** point the agent at `00-team-overview.md` plus your guide, and execute one phase or one week at a time. Review every diff.

**Key dates:** design notes Thu 5 Nov · checkpoints Fri 6 Nov ("It runs") · Fri 20 Nov ("It's real") · Fri 4 Dec ("It's smart") · Fri 11 Dec (Demo) · buffer to 18 Dec.

**Data rule:** synthetic or public only. Nothing from any customer or engagement.
