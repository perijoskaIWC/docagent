var builder = DistributedApplication.CreateBuilder(args);

// Postgres: container locally, Azure Database for PostgreSQL Flexible Server when deployed with azd.
var postgres = builder.AddAzurePostgresFlexibleServer("postgres")
                      .RunAsContainer(c => c.WithDataVolume());
var db = postgres.AddDatabase("docagentdb");

// Azure endpoints are parameters (values in AppHost user-secrets, see guide 1 §3.5). Not secrets, but not committed.
var foundryEndpoint    = builder.AddParameter("foundry-endpoint");
var aiServicesEndpoint = builder.AddParameter("aiservices-endpoint");
var searchEndpoint     = builder.AddParameter("search-endpoint");

var api = builder.AddProject<Projects.DocAgent_Api>("api")
                 .WithReference(db).WaitFor(db)
                 .WithEnvironment("Foundry__Endpoint", foundryEndpoint)
                 .WithEnvironment("AiServices__Endpoint", aiServicesEndpoint)
                 .WithEnvironment("Search__Endpoint", searchEndpoint)
                 .WithExternalHttpEndpoints();

// The React app (src/web). Vite's dev server proxies /api to the api resource, whose address Aspire injects.
builder.AddViteApp("web", "../web")
       .WithReference(api).WaitFor(api)
       .WithExternalHttpEndpoints();

builder.Build().Run();
