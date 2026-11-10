# Environments

Darko fills this in when the Azure resources exist (team overview §8). Endpoints and deployment names are not
secrets and belong here; **keys never do**: the code authenticates with `DefaultAzureCredential`.

## Development

| Key | Value | Notes |
|---|---|---|
| `Foundry:Endpoint` | _to do_ | Azure OpenAI endpoint of the Foundry resource |
| `Foundry:ChatDeployment` | _to do_ | Strong model |
| `Foundry:MiniDeployment` | _to do_ | Cheap model: detection, extraction, lessons, eval judge |
| `Foundry:EmbeddingDeployment` | _to do_ | Embeddings |
| `AiServices:Endpoint` | _to do_ | Document Intelligence, Content Safety |
| `Search:Endpoint` | _to do_ | Azure AI Search |
| `Search:IndexName` | _to do_ | Versioned, e.g. `knowledge-v1`; re-index by creating v2 |

## Using the values locally

The AppHost passes the three endpoints to the API as parameters. Set them once in the AppHost's user-secrets:

```bash
cd src/DocAgent.AppHost
dotnet user-secrets set "Parameters:foundry-endpoint" "<Foundry:Endpoint>"
dotnet user-secrets set "Parameters:aiservices-endpoint" "<AiServices:Endpoint>"
dotnet user-secrets set "Parameters:search-endpoint" "<Search:Endpoint>"
```

Until the values exist, `https://placeholder.invalid/` works: with every fake on, nothing calls Azure.
