# Onboarding

How to get DocAgent running on your machine and find your place in the code. It is also the script for the team
hand-over session (guide 1, Phase 7): the presenter walks through the five steps below, about 20 minutes in total.

**You're done when** the solution runs on your own machine and you have turned off your own fake flag. A demo on
someone else's laptop doesn't count.

## Before you start

| Tool | Check that it works |
|---|---|
| .NET SDK 10 | `dotnet --version` |
| Node.js 22 or newer | `node --version` |
| Docker Desktop or Podman, running | `docker info` or `podman info` |
| Access to the team repo on GitHub | you can open it in the browser |

Podman users: set the user environment variable `ASPIRE_CONTAINER_RUNTIME` to `podman`, then open a new terminal.

## 1. Clone and run (5 min)

```bash
git clone <team repo URL> docagent
cd docagent
cd src/web && npm install && cd ../..

# Once: the Azure endpoints (placeholders are fine while every fake is on)
cd src/DocAgent.AppHost
dotnet user-secrets set "Parameters:foundry-endpoint" "https://placeholder.invalid/"
dotnet user-secrets set "Parameters:aiservices-endpoint" "https://placeholder.invalid/"
dotnet user-secrets set "Parameters:search-endpoint" "https://placeholder.invalid/"
cd ../..

dotnet run --project src/DocAgent.AppHost
```

Open the dashboard link the console prints, then the URL next to **web**. The first start pulls the Postgres image,
which takes a minute.

## 2. Your entry point and your flag (4 min)

Every contract has a fake. Each flag in `src/DocAgent.Api/appsettings.Development.json` switches one area between its
fake and the owner's real code. The source of truth is [team overview §6.3](guides/00-team-overview.md); in short:

| Who | Flag | Entry point |
|---|---|---|
| Riste | `Profiler`, `Templates` | `AddDocAgentProfiling`, `AddDocAgentTemplates`, `AddDocAgentTools` |
| Trajche | `Detection`, `DocumentIntelligence`, `Knowledge` (ingestion) | `AddDocAgentDetection`, `AddDocAgentDocumentIntelligence`, `AddDocAgentKnowledge` |
| Todor | `Knowledge` (retrieval), `Chat` | `AddDocAgentKnowledge`, `AddDocAgentChat` |
| Boban | `Pipeline`, `Authoring` (single-agent) | `AddDocAgentPipeline`, `AddDocAgentAuthors` |
| Stevcho | `Authoring` (specialists, concurrent) | `AddDocAgentAuthors` |
| Elena | `Safety` | `AddDocAgentSafety` |

Turn off your fake in your own user-secrets, never in the shared settings file (replace `Chat` with your flag):

```bash
dotnet user-secrets set "Features:Fakes:Chat" "false" --project src/DocAgent.Api
```

While your entry point is still an empty stub, your slot simply stays empty; the API still starts. When your real
implementation works end to end, change the shared default in a pull request.

## 3. How strategies plug in (4 min)

Adding a strategy isn't a contract change. In your entry point:

```csharp
services.AddKeyedSingleton<IDesignAuthor, MyAuthor>("my-key");
services.AddSingleton(new StrategyOption("author", "my-key", "What it does", IsFake: false));
```

It then shows up in `GET /api/strategies` and in the web app under **New run → Lab settings**.

## 4. The Aspire dashboard (4 min)

- **Resources:** `postgres`, `docagentdb`, `api` and `web`, with their state and health.
- **Console logs:** each resource's output; `web` prints which API address Vite proxies to.
- **Traces:** every run is one `docagent.run` trace, tagged with its strategy; database calls nest under it.

## 5. The rules (3 min)

- Changes to `src/DocAgent.Core` are agreed with Darko first ([team overview §6.4](guides/00-team-overview.md)).
- Pull requests need green CI and a review; use the pull request template.
- Synthetic or public data only, and no secrets in the repo.
- AI coding agents follow [AGENTS.md](../AGENTS.md).

## Check off on your own machine

- [ ] The dashboard shows `postgres`, `docagentdb`, `api` and `web` as healthy
- [ ] A run on a small synthetic pack reaches **Completed** in the web app
- [ ] My fake flag is off in my user-secrets, and the API still starts
- [ ] I know where my entry point is and who reviews my pull requests

## If something goes wrong

- **Postgres doesn't start:** Docker or Podman isn't running. Start it and run the AppHost again.
- **Podman isn't used:** `ASPIRE_CONTAINER_RUNTIME=podman` is missing, or the terminal was opened before you set it.
- **Certificate warnings:** run `dotnet dev-certs https --trust` once.
- **No sample packs:** `samples/` stays empty until Darko adds his packs. Upload any small CSV you write yourself.
- **A white page or an error box in the web app:** reload with Ctrl+Shift+R. If it stays, send a screenshot of the
  error.
- **The API won't start after pulling:** someone added a migration or a package. Run `dotnet build` and start again;
  migrations apply on start.

## Read next

- [README.md](../README.md): running, fakes, strategies and the repository layout
- [AGENTS.md](../AGENTS.md): the rules your AI coding agent follows
- [docs/guides/00-team-overview.md](guides/00-team-overview.md), then your own guide in [docs/guides/](guides/)
