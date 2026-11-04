# Darko — Azure Resources (portal, step by step) and Lead Tasks

Two parts. **Part A** creates everything the team needs in Azure, in the portal, in the order they need it, with the access that makes it usable. **Part B** is the rest of the lead's work: repo, contracts, test systems and synthetic packs, templates, eval cases, reviews, the pattern catalog.

You're the only one with portal access, so **access (Step 7) is as important as the resources themselves**. A resource nobody can use blocks the team as much as a missing one. Target: everything in Phase 1 done by **Fri 30 Oct**, latest **Tue 3 Nov**, so everyone passes the smoke test before the 6 Nov checkpoint.

Portal menu names move around. Where a name below doesn't match, use the portal's search box; the resource types and settings are stable.

---

# Part A — Azure

## Summary

| # | Resource | Name | Tier | Needed by | Phase |
|---|---|---|---|---|---|
| 1 | Resource group | `rg-docagent-dev` | — | Everything | **1** |
| 2 | Microsoft Foundry resource + project | `aif-docagent-dev` / `proj-docagent-dev` | S0 | Boban, Stevcho, Trajche, Todor, Elena | **1** |
| 3 | Model deployments | `chat-main`, `chat-mini`, `embed` | Data Zone Standard (EU) or Global Standard | same | **1** |
| 4 | Azure AI Search | `srch-docagent-dev` | **Basic**, semantic ranker Free | Trajche, Todor | **1** |
| 4b | Content filter with Prompt Shields; Document Intelligence and Content Safety endpoints | on `aif-docagent-dev` | included | Elena, Trajche | **1** |
| 5 | Log Analytics workspace + Application Insights | `log-docagent-dev`, `appi-docagent-dev` | Pay-as-you-go, 1 GB/day cap | Elena | **1** |
| 6 | Budget + alerts | on `rg-docagent-dev` | — | You | **1** |
| 7 | Access: security group + role assignments | `sg-docagent-devs` | — | Everyone | **1** |
| 8 | `docs/environments.md` | repo | — | Everyone | **1** |
| 9 | Key Vault | `kv-docagent-dev` | Standard, RBAC | only if secrets appear | 2 |
| 10 | Container Apps, Container Registry, Postgres Flexible, managed identity | created by `azd up` | Consumption / Basic / B1ms | deployed app | **3 — W7** |

Locally, Postgres runs in a container via Aspire; **no cloud database until December**.

**Region: Sweden Central** (EU residency, wide model catalogue). At Step 3, if a model you want isn't offered there, pick another EU region and use it for everything.

**Naming:** Foundry and Search names must be globally unique. If taken, add a short suffix (e.g. `-iwc`) and use the same suffix everywhere.

**Tags on every resource:** `project=docagent`, `env=dev`, `owner=<your email>`, `practice=microsoft-h2`.

---

## Step 0 — Check you can create resources *and* assign roles

1. Azure portal → **Subscriptions** → your subscription → **Access control (IAM)** → **View my access**.
2. You need **Owner**, or **Contributor** plus **User Access Administrator** (or **Role Based Access Control Administrator**). Contributor alone can create resources but **cannot assign roles**, and role assignment is what unblocks the team.
3. If you lack it, ask the subscription owner **today**. Fallback if it doesn't come in time: API keys (end of Step 7).

## Step 1 — Resource group (2 min)

Portal → **Resource groups** → **Create** → Subscription, name `rg-docagent-dev`, Region **Sweden Central** → **Tags** tab: add the four tags → **Review + create** → **Create**.

## Step 2 — Microsoft Foundry resource + project (10 min)

1. Open the **Microsoft Foundry portal** (`ai.azure.com`) and sign in with your work account.
2. **Create new** → choose a **Foundry resource** project (the current model: a Foundry resource with a project inside; **not** a hub-based project).
3. Project name `proj-docagent-dev`. Open **Advanced options**: Subscription, Resource group `rg-docagent-dev`, Region Sweden Central, Foundry resource name `aif-docagent-dev`.
4. **Create**. Wait for provisioning.
5. Open the project's **Overview** and copy three values into a note (they go into `docs/environments.md` at Step 8):
   - **Azure OpenAI endpoint**: `https://aif-docagent-dev.openai.azure.com/` → `Foundry:Endpoint`
   - **Azure AI Services endpoint**: `https://aif-docagent-dev.cognitiveservices.azure.com/` → `AiServices:Endpoint` (Document Intelligence, Content Safety)
   - **Project endpoint**: `https://aif-docagent-dev.services.ai.azure.com/api/projects/proj-docagent-dev` (Foundry evaluators, hosted agents later)
6. In the Azure portal, find the new resource `aif-docagent-dev` (type *Azure AI Foundry* / *AI Services*) and add the tags.

## Step 3 — Model deployments (10 min)

In the Foundry portal, inside the project: **Models + endpoints** (or **Deployments**) → **Deploy model** → **Deploy base model**. Use these **deployment names exactly**; the code references the names, never the model names.

| Deployment name | Model | Used for | Starting rate limit |
|---|---|---|---|
| `chat-main` | The strongest GA GPT chat model offered in the region (a GPT-5-family model if offered; otherwise the newest GPT-4.1-class model) | Design authors, Reviewer, Ask chat | 50K–100K TPM |
| `chat-mini` | The matching *mini* model | `rag` detector, lesson extraction, eval judge, cheaper specialists (E5) | 50K–100K TPM |
| `embed` | `text-embedding-3-large` | Knowledge base embeddings, **3072 dimensions** | 50K TPM |

For each: pick the model → **Customize** → deployment name → **Deployment type**: *Data Zone Standard* if offered (processing stays in the EU data zone), otherwise *Global Standard*; note which → **Tokens per minute rate limit** as above → content filter: leave default for now (Step 4b replaces it) → **Deploy**.

Write down for Step 8: model names and versions behind each alias, the deployment type, and **tell Trajche the embedding model and dimensions**. The index schema depends on them; changing them later means re-indexing everything.

Keep rate limits modest. They are a cost and blast-radius limit, not just a quota; raise them when E2's five-repeat runs need it.

## Step 4 — Azure AI Search (10 min)

1. Azure portal → **Create a resource** → search **Azure AI Search** → **Create**.
2. **Basics**: Resource group `rg-docagent-dev`, Service name `srch-docagent-dev`, Location Sweden Central, **Pricing tier: Basic** (change from the default; Free is too small for two people iterating on index versions).
3. **Scale**: 1 replica, 1 partition. **Tags**. **Review + create** → **Create**.
4. After creation, open the service → **Settings → Keys** → **API access control** → choose **Both** (API keys and role-based access control) → **Save**. This is what lets the app authenticate with `DefaultAzureCredential`.
5. **Settings → Semantic ranker** → select the **Free** plan → **Save**.
6. **Overview** → copy the **URL** (`https://srch-docagent-dev.search.windows.net`) → `Search:Endpoint`.

Agentic retrieval (a stretch item for Todor) may need a higher tier or has its own limits; check the Search service limits page before W6 and tell him.

## Step 4b — Content filter with Prompt Shields; Document Intelligence (10 min)

**Content filter.** Our agents read uploaded export packs, which are untrusted input, so the model deployments get a filter with Prompt Shields on:

1. Foundry portal → project → **Guardrails + controls** (older name: *Content filters* / *Safety + security*) → **Create content filter**.
2. Name `docagent-filter`. **Input filter**: turn on **Prompt shields for jailbreak attacks** and **Prompt shields for indirect attacks**, action *Annotate and block*. Keep the default harm-category thresholds. **Output filter**: defaults.
3. **Deployments**: apply to `chat-main` and `chat-mini` (replace their default filter) → **Create**.

Elena additionally screens every upload with the Content Safety API before any agent reads it (her second guide). Both layers matter: the deployment filter sees prompts; her screen sees the raw files.

**Document Intelligence.** It is part of the Foundry (AI Services) resource and reachable at the AI Services endpoint from Step 2. Verify in the Azure portal: resource `aif-docagent-dev` → **Resource Management → Keys and Endpoint** shows the endpoint. If the region or resource doesn't expose Document Intelligence (you'd see 404s when Trajche first calls `prebuilt-layout`), create a standalone **Document Intelligence** resource (S0) in `rg-docagent-dev` and publish its endpoint as `DocumentIntelligence:Endpoint`.

**Azure AI Language (optional, should):** same endpoint; PII detection, if we add the PII check.

## Step 5 — Log Analytics + Application Insights (10 min)

1. **Create a resource** → **Log Analytics workspace** → `log-docagent-dev`, `rg-docagent-dev`, Sweden Central → tags → **Create**.
2. Open it → **Settings → Usage and estimated costs** → **Daily cap** → enable, **1 GB/day** → **OK**.
3. **Create a resource** → **Application Insights** → name `appi-docagent-dev`, resource group, region, **Resource mode: Workspace-based**, select `log-docagent-dev` → tags → **Create**.
4. Open it → **Overview** → copy the **Connection string**. Give it to Elena privately (chat DM or Key Vault later). It isn't a credential in the strict sense, but it doesn't go in the repo.
5. Foundry portal → project → **Tracing** (or **Observability**) → **Connect Application Insights** → select `appi-docagent-dev`. This makes agent traces appear in the Foundry portal once the app exports to App Insights.

## Step 6 — Budget (5 min)

Azure portal → `rg-docagent-dev` → **Cost Management → Budgets** → **Add**: scope the resource group, **Monthly**, amount **€200** (adjust after two weeks of real usage) → **Alert conditions**: 50%, 80%, 100% of **Actual**, and 100% of **Forecasted** → alert recipients: you + one backup → **Create**.

## Step 7 — Access for the team (20 min, plus propagation)

### 7.1 One security group
Azure portal → **Microsoft Entra ID** → **Groups** → **New group**: type **Security**, name `sg-docagent-devs`, members: Boban Lazarevski, Riste Bachovski, Trajche Smilevski, Elena Perijoska, Todor Sidorov, Stevcho Naumovski, **and yourself** → **Create**. (If you can't create groups, ask IT, or assign the roles below per person.)

### 7.2 Role assignments
For each row: open the resource → **Access control (IAM)** → **Add → Add role assignment** → pick the role → **Members: User, group, or service principal** → select the group (or the people) → **Review + assign**.

| Scope | Role | Who | Why |
|---|---|---|---|
| Resource group `rg-docagent-dev` | **Reader** | group | See resources in the portal |
| Foundry resource `aif-docagent-dev` | **Reader** | group | Recommended pairing with Foundry User |
| Foundry resource | **Cognitive Services OpenAI User** | group | Model inference through the Azure OpenAI endpoint (what our code uses) |
| Foundry resource | **Cognitive Services User** | group | Document Intelligence, Content Safety, Language APIs |
| Foundry **project** `proj-docagent-dev` | **Foundry User** | group | Build and run against the project; Foundry evaluators; hosted agents. Assign it in the Foundry portal: **Management center → Project → Users → Add** (the role was called *Azure AI User* before May 2026; use whichever name the UI shows) |
| Search service `srch-docagent-dev` | **Search Index Data Reader** | group | Query the index |
| Search service | **Search Index Data Contributor** and **Search Service Contributor** | Trajche, Todor (and you) | Create/update indexes, write documents |
| Log Analytics `log-docagent-dev` | **Log Analytics Reader** | group | Query traces and logs |

Assign yourself too: owning the subscription doesn't grant data-plane access to models or the index.

**Propagation takes 15–30 minutes.** Tell everyone to run `az logout && az login` afterwards. A `403` right after assignment is usually propagation, not a wrong role.

### 7.3 The smoke test everyone must pass (by Fri 6 Nov)
Send this to the team. Each person needs the Azure CLI and `az login`, then:

```bash
dotnet new console -n FoundrySmoke && cd FoundrySmoke
dotnet add package Azure.AI.OpenAI && dotnet add package Azure.Identity
```
```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;

var client = new AzureOpenAIClient(new Uri("https://aif-docagent-dev.openai.azure.com/"), new DefaultAzureCredential());
ChatClient chat = client.GetChatClient("chat-mini");
var reply = await chat.CompleteChatAsync("Reply with the single word OK.");
Console.WriteLine(reply.Value.Content[0].Text);
```
Prints `OK` → access works. `401/403` → wait for propagation, re-login, then ask you. Trajche and Todor also run one `SearchIndexClient.GetServiceStatisticsAsync()` against the Search endpoint.

### 7.4 Fallback if role assignment isn't possible in time
API keys for Foundry (Azure portal → `aif-docagent-dev` → Keys and Endpoint) and Search (→ Keys), shared through a password manager, **never** chat, email or the repo, set by each developer with `dotnet user-secrets`. Treat it as temporary and switch to RBAC as soon as you can.

## Step 8 — Publish the configuration

Fill `docs/environments.md` in the repo (endpoints and names only, **no keys**):

| Key | Value |
|---|---|
| `Foundry:Endpoint` | `https://aif-docagent-dev.openai.azure.com/` |
| `Foundry:ChatDeployment` | `chat-main` → *model + version* |
| `Foundry:MiniDeployment` | `chat-mini` → *model + version* |
| `Foundry:EmbeddingDeployment` | `embed` → `text-embedding-3-large`, 3072 dims |
| Deployment type | Data Zone Standard / Global Standard |
| `AiServices:Endpoint` | `https://aif-docagent-dev.cognitiveservices.azure.com/` |
| `Search:Endpoint` | `https://srch-docagent-dev.search.windows.net` |
| `Search:IndexName` | `knowledge-v1` |
| Foundry project endpoint | `https://aif-docagent-dev.services.ai.azure.com/api/projects/proj-docagent-dev` |
| Region | Sweden Central |
| Who has which role | the table from 7.2 |

---

## Phase 2 — when needed

**Key Vault** (only if secrets appear, e.g. Langfuse keys): **Create a resource → Key Vault** → `kv-docagent-dev`, **Permission model: Azure role-based access control** → Create. IAM: Elena **Key Vault Secrets Officer**; later the deployed app's managed identity **Key Vault Secrets User**.

**Langfuse** (if ADR 0003 chooses it): Langfuse Cloud **EU** (`cloud.langfuse.com`) → organisation + project → keys to Elena via Key Vault. Decide with her whether prompt/completion text is sent (acceptable only because all our data is synthetic or public).

## Phase 3 — W7: deploy with `azd`

This part is CLI; the portal can't do it. With Elena, from the repo root:
```bash
azd auth login
azd init      # detects the Aspire AppHost
azd up        # subscription, same EU region, environment name "dev"
```
`azd up` provisions from the AppHost: Container Apps environment, Container Registry, Log Analytics, a user-assigned managed identity and **Azure Database for PostgreSQL Flexible Server**. It prompts for the `foundry-endpoint`, `aiservices-endpoint` and `search-endpoint` parameters.

Then grant the **app's managed identity** (portal IAM, as in 7.2): Foundry User on the project; Cognitive Services OpenAI User and Cognitive Services User on the Foundry resource; Search Index Data Contributor on Search; Key Vault Secrets User if used. Set `APPLICATIONINSIGHTS_CONNECTION_STRING` on the API container app so cloud traces flow to `appi-docagent-dev`. `azd` creates its own resource group (`rg-dev`); tag it and add it to the budget.

## Cost guide (approximate)

| Item | Character | Rough dev cost |
|---|---|---|
| AI Search Basic | **Fixed, 24/7**: the main fixed cost | ~USD 75 / month |
| Foundry models | Per token | Low tens of USD / month for dev; the five-repeat experiment runs in W6 are the spikes |
| Log Analytics / App Insights | Per GB | Low, with the 1 GB cap and sensitive-data capture off |
| PostgreSQL Flexible B1ms (Dec) | Fixed while running | ~USD 15–25 / month |
| Container Apps (Dec) | Consumption, scales to zero | Minimal |
| Container Registry Basic (Dec) | Fixed | ~USD 5 / month |

Keep rate limits low, keep the Log Analytics cap, delete experimental indexes, and after the demo decide whether the Search service stays.

## Phase 1 checklist

- [ ] Can create resources **and** assign roles
- [ ] `rg-docagent-dev` created and tagged
- [ ] Foundry resource + project created; three endpoints noted
- [ ] `chat-main`, `chat-mini`, `embed` deployed; embedding model + dims sent to Trajche
- [ ] AI Search Basic with API access control *Both* and semantic ranker Free
- [ ] Content filter with both Prompt Shields applied to `chat-main` and `chat-mini`; Document Intelligence endpoint confirmed
- [ ] Log Analytics (1 GB cap) + App Insights, connected to the Foundry project; connection string to Elena
- [ ] Budget with alerts
- [ ] Security group and all role assignments (yourself included)
- [ ] Everyone passed the smoke test
- [ ] `docs/environments.md` published

---

# Part B — Lead tasks

## B1. GitHub repo (Tue 27–Wed 28 Oct)

1. GitHub → IWConnect organisation → **New repository**: `docagent`, **Private**, no template, no README (Elena's Phase 1 creates the files).
2. **Settings → Collaborators and teams**: add the six developers with **Write**; yourself **Admin**.
3. After Elena's first push to `main`: **Settings → Branches → Add branch protection rule** (or Rulesets) for `main`: *Require a pull request before merging* (1 approval), *Require status checks to pass* (select the `ci` check once it exists), *Require review from Code Owners*.
4. **Settings → Code security**: enable Copilot code review for PRs if the org licence offers it. Everyone should also have Copilot in their IDE.
5. `CODEOWNERS` (Elena adds the file in Phase 6): `src/DocAgent.Core/** @<your GitHub handle>`.

## B2. Contracts (W1–W2)

Elena copies the contracts from `00-team-overview.md` §6.1 into `src/DocAgent.Core` verbatim and opens the PR; you review it. From then on, you approve every change to `DocAgent.Core` (§6.4 rule). Expect two kinds of requests: additions to `SystemModel` from Riste (fine, additive) and new fields on `DesignRequest` from Boban (fine, with defaults). Push back on anything that moves logic into Core.

## B3. Test-system specs (Fri 30 Oct)

One spec per system in `samples/specs/<system-id>.md` (+ a machine-readable `<system-id>.yaml` the generator reads). Claude can draft them; you review that they are realistic and that the three systems are **distinguishable but not trivially so**.

| System | What the spec defines |
|---|---|
| `quarry-wms` (fictional) | 8–10 entities (items, locations, stock, inbound receipts, outbound shipments, shipment lines, carriers, cycle counts, users); keys and foreign keys; naming convention `qw_` prefix; id formats; an XSD + sample XML for outbound shipments; a job schedule JSON (nightly stock export, hourly carrier sync); a 3–4 page PDF admin guide (headings, two tables); planted data-quality issues (duplicate SKUs, 12% missing carrier on shipments, mixed date formats in one column) |
| `helio-service` (fictional) | OpenAPI 3 spec: work orders, technicians, customers, sites, assets, tickets, SLAs; webhooks config (events `work_order.status_changed`, `ticket.created`); NDJSON dumps for 5 entities; an Excel KPI report (3 sheets); ids like `WO-000123`; planted issues (8% missing completion dates, orphan technician ids) |
| `dataverse` (real, public) | A solution export shape: `solution.xml`, `customizations.xml` with 5–6 custom tables (publisher prefix `iwc_`), option sets, relationships; an OData `$metadata` excerpt; an Excel data export. Written from public Microsoft Learn documentation of the solution file format; **only our own fictional tables and data** |
| 2 decoy profiles | Knowledge-base only: fictional systems resembling the above (e.g. a WMS with a `qx_` prefix; a ticketing API with a similar path layout). No packs needed |
| unknown ×2 | Two small packs from systems with no profile (e.g. a generic CSV accounting export; a JSON config dump) |
| injection ×1 | A `helio-service` data-only pack where one text field contains "Ignore previous instructions and …". Must be blocked by Elena's screen |

**Variants per system:** `full`, `data-only` (no schemas, no docs), `metadata-only` (schemas and config, no data), `noisy` (renamed files, two irrelevant extras, one empty file).

**Ground truth per pack** (`samples/<system>/<variant>/ground-truth.json`): `systemId`, expected entity ids, field counts, relationships with basis, interfaces, schedules, planted findings, and 5–10 facts a correct design must state. This is what Riste's golden tests, Trajche's E1 and Elena's harness read.

## B4. Synthetic pack generator (Wed 4 Nov → first packs Fri 6 Nov; `dataverse` by Fri 20 Nov)

`samples/_generator/`: a small script (Python with `-I`, or a C# console) that reads a spec YAML, uses a **seeded** random generator, and writes every variant plus `ground-truth.json`. Deterministic output, so regenerating produces identical packs and the golden tests stay stable. Commit both the generator and its output. Claude can write the generator from the specs; you review the output for realism (plausible names, dates, volumes of 200–5,000 rows).

## B5. Template guidance review (Mon 2 Nov, with Riste)

Riste drafts the three templates (section ids from 00 §2). You review the **guidance text** of every section: is it specific, checkable, and does it name the tables and their columns? The authors and the Reviewer can only be as precise as that text.

## B6. Seed eval cases (Fri 13 Nov)

Three cases in `evals/datasets/v1/` in Elena's format: one `quarry-wms` full → `system-assessment`, one `helio-service` noisy → `system-assessment`, one `helio-service` full → `integration-design` with a brief. Each with 5–10 expected facts (at least one `source: knowledge`), `mustNotContain`, and notes. The ground-truth files from B3 make this mostly copy-and-phrase.

## B7. Design note reviews (Fri 6 Nov)

Review every note (`docs/design/*.md`) against this checklist, and write your comments as PR review comments:
- Does it answer every research question in the person's guide, with links?
- Does the design implement the contract as written, with no hidden contract changes?
- Are the package versions named and current?
- Is the W3–W7 plan realistic for ~4 protected hours a week plus a coding agent?
- Are the risks real, and is there a fallback for the biggest one?
- Extras: do they fit the architecture? Say yes or no explicitly.

A note that fails the checklist gets one revision by Mon 9 Nov. Building starts anyway on Mon 9 Nov on the parts that are agreed.

## B8. Weekly sync and checkpoints

**Calendar:** weekly sync (same slot every week); checkpoints Fri 6 Nov, Fri 20 Nov, Fri 4 Dec; demo Fri 11 Dec. Send the invites this week.

**Weekly sync (45 min, fixed slot):** 5 min per person: what moved (show it), what's blocked, any contract change request. Then 10 min on the checkpoint ahead. No slides.

**Checkpoint runbook:** the day before, everyone merges to `main`; you run the AppHost from a clean clone with the agreed flags off; the checkpoint meeting walks through the "what each checkpoint must show" list in 00 §10. Anything faked that should be real goes on the risk list with an owner and a date.

## B9. Pattern catalog v1 (Fri 4 Dec)

`docs/patterns/README.md` + one page per pattern: context, the problem, our implementation (component, strategy key), evidence (which experiment, which numbers), when to use, when not. Starting list: deterministic-first extraction; drill-down tools over MCP; structured parts with validation; evidence-or-say-so; bounded review loop; keyed strategies with evaluation; retrieval modes; rules-then-RAG detection; lessons as memory. A final column maps each pattern to what TRYche does, which answers management's "what can be reused from TRYche" question.

## Part B checklist

- [ ] Repo, team access, branch protection, CODEOWNERS
- [ ] Contracts PR reviewed and merged
- [ ] Test-system specs written and reviewed
- [ ] Generator and `quarry-wms` + `helio-service` packs committed (6 Nov); `dataverse` (20 Nov)
- [ ] Template guidance reviewed with Riste
- [ ] Design notes reviewed (6 Nov)
- [ ] Seed eval cases (13 Nov)
- [ ] Pattern catalog v1 (4 Dec)
