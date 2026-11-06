using DocAgent.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<DocAgentDbContext>("docagentdb");
builder.Services.AddDocAgentInfrastructure();

var app = builder.Build();
app.MapDefaultEndpoints();

// Fine for this internal dev environment: the schema is created or updated on every start.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DocAgentDbContext>().Database.MigrateAsync();
}

// Placeholder until the real endpoints arrive in Phase 4.
app.MapGet("/", () => "DocAgent API");

app.Run();
