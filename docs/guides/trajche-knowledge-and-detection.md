# Trajche — RAG part 1: Knowledge and Detection

**You own what the product knows and how it recognises a system.** The knowledge corpus and its index in Azure AI Search, ingestion and chunking, Document Intelligence for PDFs, and the system detectors. Detection sits with you because it's a retrieval problem: matching an export's signals against system profiles that live in your corpus.

Todor owns how the index is *queried* for writing and chat (`todor-retrieval-ask-memory.md`). You share the index and one design note; agree the schema together in W2.

Read `00-team-overview.md` first, especially §2 (test systems), §4 (E1, E4), §6 (contracts) and §9 (data rules).

---

## Your mission and what you'll learn

| You deliver | You learn |
|---|---|
| A knowledge corpus (system profiles, patterns, standards, glossary, exemplars) with licences recorded | **Building a knowledge base** that is small, good and legal; writing system profiles that drive both detection and generation |
| `IKnowledgeIngestor`: chunking (two strategies), embeddings, idempotent upload; a versioned index schema | **Azure AI Search**: index design, vector fields and HNSW profiles, semantic configuration, hybrid search; **embeddings** with Foundry models; chunking trade-offs (E4) |
| `IDocumentTextExtractor` on Azure **Document Intelligence** (layout → Markdown) | Turning PDFs and scans into agent-readable text |
| Three `ISystemDetector`s (`rules`, `rag`, `hybrid`) and experiment E1 | **Detection as retrieval + structured LLM output**; confidence calibration; saying "unknown" |

---

## Research brief (now → Thu 5 Nov)

Answer these in `docs/design/knowledge-and-retrieval.md` (your half; Todor writes the other), with links. Sources: Microsoft Learn *Azure AI Search* (vector search, hybrid search, semantic ranker, index design, chunking guidance, agentic retrieval overview); Microsoft Learn *Document Intelligence* (layout model, Markdown output); the `Azure.Search.Documents` and `Azure.AI.DocumentIntelligence` .NET package docs; Microsoft Learn *Power Platform / Dataverse* (solution files, `customizations.xml`, Web API `$metadata`), for the Dataverse profile.

**Index and ingestion**
1. How is a vector field declared in `Azure.Search.Documents` (dimensions, HNSW algorithm configuration, vector profile), and how is a **semantic configuration** declared? Which of these can change on a live index and which require a new index?
2. What does the **Basic** tier allow: indexes, storage, vector quota? What does the semantic ranker's free plan include?
3. **Chunking**: what does Microsoft's guidance say about chunk size, overlap and heading context for RAG? How will you count tokens in .NET (`Microsoft.ML.Tokenizers`)?
4. **Embeddings**: `text-embedding-3-large` at 3072 dims (Darko's deployment): cost per 1M tokens, batch limits, max input tokens per text.
5. **Idempotent re-ingest**: how do you find and delete all chunks of a document by filter, and what are the batch limits for upload?

**Document Intelligence**
6. Which model gives Markdown with tables (`prebuilt-layout`, `OutputContentFormat = Markdown`), what are the page and size limits per request on our tier, and how is it billed?

**Detection**
7. What distinguishes our three test systems in their exports (file names, XML roots, OpenAPI titles, column prefixes, id formats, publisher prefixes in Dataverse solutions)? Read Darko's specs (Fri 30 Oct) and write the **signal catalogue** with Riste: signal names, value formats, examples.
8. For the `rag` detector: how do you constrain `chat-mini`'s structured output to a closed set of system ids (the retrieved ones, or `unknown`) and make every evidence item quote a signal?
9. For the Dataverse profile: what does a solution export contain (`solution.xml`, `customizations.xml` entities, attributes, option sets, relationships), what does `$metadata` look like, and what public Microsoft Learn pages document it? Write the profile **in your own words**, with links. No copied documentation.

**Spike** (one evening, after Darko's smoke test and Search role assignment): create a test index in our Search service with a vector field and a semantic configuration; embed and upload 10 Markdown chunks with `embed`; run keyword, vector and hybrid queries in the portal's Search explorer and in code; run Document Intelligence layout on one PDF and look at the Markdown.

**Design note (your half):** the index schema (final, with Todor); chunking rules for both strategies; the profile front-matter format and the signal catalogue; the three detector designs with the confidence rule; the corpus list with licences; the embedding ADR; the E1 hypothesis.

---

## Step 1 — The corpus

| Kind | What | Who writes it | Licence |
|---|---|---|---|
| `SystemProfile` | One document per known system: what it is, typical exports, **fingerprints**, core entities, APIs and integration options, known quirks, sources. Three test systems plus two decoys | You, with Darko (fictional systems from their specs; Dataverse from public Microsoft Learn docs, **in our own words**, with links) | `internal` |
| `Pattern` | Integration, migration and cloud design patterns: e.g. Azure Architecture Center integration and migration guidance, cloud design patterns, messaging patterns | Excerpts with attribution, or our own summaries with links | `CC-BY-4.0` for Architecture Center content; check any other source first |
| `Standard` | How we write designs: evidence rules, section-writing guidance, naming, what "good" looks like per section | Team, with Darko | `internal` |
| `Glossary` | Terms used in designs (integration, data, Azure services) | Team | `internal` |
| `Exemplar` | One hand-reviewed example design per template, for a fictional system (W5, after the first real runs) | Team, reviewed by Darko | `internal` |
| `Lesson` | Generalised rules learned from reviews (Todor writes them) | Generated | `internal` |

**Start small and good:** 40–80 documents beat thousands of random ones. Every file is Markdown with front matter:

```markdown
---
id: sys-helio-service
kind: SystemProfile
title: Helio Service — field service and ticketing SaaS
license: internal
sourceUri: samples/specs/helio-service.md
tags: { systemId: helio-service }
fingerprints:
  - { signal: openapi-title,  match: "Helio Service API",     weight: 5 }
  - { signal: file-name,      match: "webhooks*.json",        weight: 2 }
  - { signal: column-prefix,  match: "hs_",                   weight: 3 }
  - { signal: id-format,      match: "WO-\\d{6}",             weight: 3 }
  - { signal: path-prefix,    match: "/api/v2/work-orders",   weight: 3 }
---
# Helio Service
## What it is
...
## Typical exports
...
## Core entities
...
## APIs and integration options
...
## Known quirks
...
## Sources
...
```

The **front matter feeds the `rules` detector**, the **body feeds retrieval** (the `rag` detector, the authors and Ask). One file, one source of truth.

**Decoy profiles:** two fictional systems that deliberately share some signals with Quarry WMS and Helio Service (similar column prefixes, a similar API path). Without them, detection is too easy to learn anything from.

---

## Step 2 — Index schema (finalise with Todor in W2)

| Field | Type | Attributes |
|---|---|---|
| `chunkId` | `Edm.String` | **key**: `{documentId}--{chunkIndex}` (URL-safe) |
| `documentId` | `Edm.String` | filterable |
| `kind` | `Edm.String` | filterable, facetable |
| `title` | `Edm.String` | searchable |
| `headingPath` | `Edm.String` | searchable, e.g. "Helio Service › APIs and integration options" |
| `content` | `Edm.String` | searchable |
| `contentVector` | `Collection(Edm.Single)` | vector; dimensions = the embedding model's; HNSW profile |
| `systemId` | `Edm.String` | filterable (`any` for general material) |
| `templateId` | `Edm.String` | filterable (`any`) |
| `sectionId` | `Edm.String` | filterable (`any`) |
| `sourceUri` | `Edm.String` | retrievable |
| `license` | `Edm.String` | filterable, retrievable |
| `chunking` | `Edm.String` | filterable: `heading` or `fixed` (for E4, both live in one index) |
| `disabled` | `Edm.Boolean` | filterable: switch off a bad lesson without deleting it |
| `createdAt` | `Edm.DateTimeOffset` | filterable, sortable |

Plus a **semantic configuration** (`title` → title, `content` → content, `headingPath` → keywords) so Todor can use the semantic ranker.

**The index name is versioned** (`knowledge-v1`). A breaking change means creating `knowledge-v2`, re-ingesting, switching `Search:IndexName` and deleting v1. Never mutate a live index's field types. **The embedding model and dimensions are fixed in an ADR in W2.** Changing them later means re-embedding everything.

---

## Step 3 — Ingestion

### Chunking (two strategies, for E4)

| Strategy | Rule |
|---|---|
| `heading` (default) | Split by headings; split long sections at ~500–800 tokens with ~10% overlap; **prefix each chunk with its heading path**. Glossary: one term per chunk. Lessons: one per chunk. Exemplars: one chunk per template section, tagged `sectionId` |
| `fixed` | Fixed windows of ~600 tokens with ~10% overlap, ignoring structure (still prefixed with the document title) |

Ingest the corpus with both strategies (the `chunking` field tells them apart) so Todor can compare them without two indexes. Count tokens with a tokenizer (`Microsoft.ML.Tokenizers` with the model's encoding), not characters.

### Code shapes
> Verify method names against the current `Azure.Search.Documents`, `Azure.AI.DocumentIntelligence` and `Microsoft.Extensions.AI` docs for the versions you install.

```bash
dotnet add src/DocAgent.Knowledge package Azure.Search.Documents
dotnet add src/DocAgent.Knowledge package Azure.AI.OpenAI
dotnet add src/DocAgent.Knowledge package Microsoft.Extensions.AI.OpenAI
dotnet add src/DocAgent.Knowledge package Azure.AI.DocumentIntelligence
dotnet add src/DocAgent.Knowledge package Azure.Identity
```

```csharp
// Embeddings
var openAi = new AzureOpenAIClient(new Uri(cfg["Foundry:Endpoint"]!), credential);
IEmbeddingGenerator<string, Embedding<float>> embedder =
    openAi.GetEmbeddingClient(cfg["Foundry:EmbeddingDeployment"]!).AsIEmbeddingGenerator();
var vectors = await embedder.GenerateAsync(chunkTexts, cancellationToken: ct);   // batches of 16–64
```

- **Index creation** on startup if missing: `SearchIndexClient.CreateOrUpdateIndexAsync(...)` with one HNSW configuration and vector profile, `contentVector` as a vector field with the right dimensions, and the semantic configuration.
- **Upload** with `SearchClient.MergeOrUploadDocumentsAsync` in batches of ~100.
- **Idempotency:** on re-ingest of a `documentId`, delete its existing chunks first (filter by `documentId`, delete by key), then upload. Otherwise a shorter new version leaves orphan chunks.
- **Auth:** `DefaultAzureCredential`. You need **Search Service Contributor** and **Search Index Data Contributor** (Darko assigns them).

### Document Intelligence → `IDocumentTextExtractor`
Use the prebuilt **layout** model with **Markdown output**, so headings and tables survive. It serves two callers: the profiler (PDFs inside export packs, e.g. the Quarry WMS admin guide) and knowledge uploads (`POST /api/knowledge` with a PDF).

```csharp
var client = new DocumentIntelligenceClient(new Uri(cfg["AiServices:Endpoint"]!), credential);
var options = new AnalyzeDocumentOptions("prebuilt-layout", BinaryData.FromBytes(content))
{
    OutputContentFormat = DocumentContentFormat.Markdown
};
var op = await client.AnalyzeDocumentAsync(WaitUntil.Completed, options, ct);
return op.Value.Content;   // Markdown
```
Page limits and sizes depend on the tier; return a clear error for oversized files.

---

## Step 4 — Detectors (E1)

All three implement `ISystemDetector`, read the same `SystemModel.Signals` (plus file names, entity and field names), and return a `SystemDetection` with **evidence that names its source**. Register each as a keyed service with a `StrategyOption`.

| Key | How it works | Cost |
|---|---|---|
| `rules` | Load every profile's `fingerprints` (front matter, cached). Score each system = sum of weights of matching signals. Confidence = score share with a margin over the runner-up (calibrate on the dataset). Below a threshold → `unknown`. Evidence = the matched signals with their `SourceRef`s | No model calls |
| `rag` | Build a short **signal summary** from the model (file names, XML roots, API titles, column names sample, id formats). Retrieve the top `SystemProfile` chunks (hybrid). Ask `chat-mini` for structured output: `{ systemId, confidence, evidence[] }`, constrained to the retrieved system ids or `unknown`, where every evidence item must quote a signal | 1 model call + 1 search |
| `hybrid` (should) | Run `rules`. If confidence ≥ 0.8 and margin ≥ 0.2, accept. Otherwise run `rag` with the rule scores as hints | Model call only when needed |

**Prompt for `rag`** lives in `src/DocAgent.Knowledge/Prompts/detector.md` (versioned, like Stevcho's). It must say: prefer `unknown` over a guess; evidence must come from the signal summary; confidence reflects how distinctive the evidence is, not how familiar the product name sounds.

### Running E1
On every pack in the dataset (12 system packs + unknowns), with the ground truth from Darko's generator:
- **Accuracy** on known systems, per variant (full / data-only / metadata-only / noisy)
- **Unknown handling:** precision and recall of `unknown`
- **Calibration:** bucket by confidence (0–0.5, 0.5–0.8, 0.8–1) and check accuracy per bucket. A detector that says 0.95 and is wrong is worse than one that says 0.6
- **Cost and latency** per detection

Write the hypothesis in W2 (e.g. "rules win on full packs; rag wins on data-only and noisy packs; hybrid gets the best of both at a third of rag's cost").

### Running E4 (your half: chunking)
With Todor, on the retrieval test set: `heading` vs `fixed` chunking, crossed with Todor's query modes. Report hit@5 and MRR.

---

## Week plan

| Week | Do | Done when |
|---|---|---|
| **W1** 28–30 Oct | Read 00 and this guide. Tooling, smoke test (also the Search statistics call). Start the research questions | — |
| **W2** 2–6 Nov | Spike (above). Design note with Todor. Signal catalogue with Riste from Darko's specs. ADR: embedding model + dimensions. Corpus list with Darko | Design note reviewed Fri 6 Nov |
| **W3** 9–13 Nov | Real `IKnowledgeIngestor` (both chunkings), index created on startup. Profiles for `quarry-wms`, `helio-service` + 2 decoys. Standards and glossary v1. `rules` detector | `rules` right on the full packs; documents searchable in the portal |
| **W4** 16–20 Nov | Patterns ingested (licence + source). `dataverse` profile. `IDocumentTextExtractor` real. Knowledge page upload end to end (with Elena's endpoint) | **Checkpoint "It's real"**: real detection in a real run |
| **W5** 23–27 Nov | `rag` detector. E4 chunking runs with Todor and Elena's runner. Exemplars (one per template). Lesson documents writable for Todor | E4 report (chunking half) |
| **W6** 30 Nov–4 Dec | **Run E1** with Elena's runner. `hybrid` detector (should). Corpus quality pass: duplicates, empty and oversized chunks | **Checkpoint "It's smart"**: E1 report |
| **W7** 7–11 Dec | Seed the cloud index with one command. E1 note. Document the corpus. Architecture document section on knowledge and detection | Demo |

---

## Definition of done (11 Dec)

- [ ] Schema agreed with Todor, documented, index versioned; embedding ADR merged
- [ ] Corpus: 5 system profiles (3 real test systems + 2 decoys), patterns with licences, standards, glossary, exemplars
- [ ] `IKnowledgeIngestor` real, idempotent, both chunking strategies
- [ ] `IDocumentTextExtractor` real (Document Intelligence layout → Markdown)
- [ ] `rules` and `rag` detectors registered with `StrategyOption`s; `hybrid` (should)
- [ ] `Knowledge`, `Detection` and `DocumentIntelligence` flags `false` as the shared defaults
- [ ] Re-index script populates a fresh environment from `knowledge/` in one command
- [ ] E1 report and note; E4 chunking results

---

## If you have time left (write it in the design note first)

- **Integrated vectorization** in AI Search (indexer + skillset that chunks and embeds for you) as a third ingestion path; compare with your own pipeline.
- **Agentic retrieval** knowledge base over the index (Todor's stretch) needs a knowledge source definition from you.
- A **detector that learns**: store confirmed detections (user overrides) as extra fingerprints with low weight.
- **Multilingual** profiles: a Macedonian glossary, to see how the semantic ranker and embeddings behave.

---

## Pitfalls

- **Copying documentation wholesale.** Write profiles in our own words; record licence and source for everything else.
- **Profiles that leak the answer.** A fingerprint matching the pack generator's file names one-to-one makes `rules` look perfect and teaches us nothing. Use the noisy and data-only variants honestly.
- **Changing embedding model or dimensions** after ingestion. Decide in W2 and record it.
- **Chunks without context.** "Retry 3 times" is useless without its heading path. Always prefix.
- **Orphan chunks** after re-ingest. Delete by `documentId` first.
- **A detector that never says "unknown".** Test it on the unknown packs from day one.
