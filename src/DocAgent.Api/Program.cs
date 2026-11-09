using System.Text.Json.Serialization;
using System.Threading.Channels;
using DocAgent.Api;
using DocAgent.Api.Runs;
using DocAgent.Api.Uploads;
using DocAgent.Infrastructure;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<DocAgentDbContext>("docagentdb");
builder.Services.AddDocAgentInfrastructure();

// Guide 1 §4.1: for every Features:Fakes flag, the fake or the owner's entry point.
builder.Services.AddDocAgentServices(builder.Configuration);

// POST /api/runs writes run ids into the channel; the RunWorker reads them and runs one at a time.
builder.Services.AddSingleton(Channel.CreateUnbounded<Guid>());
builder.Services.AddHostedService<RunWorker>();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
// Enums as names ("Completed", not 7), the same as the JSON stored in the database (DocAgentJson).
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// The multipart default is 128 MB in total; an upload may be 30 files of 10 MB.
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = UploadLimits.MaxRequestBytes);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler();   // unhandled exceptions become a ProblemDetails 500; Development shows the exception
app.UseStatusCodePages();        // error responses without a body (404, 405, ...) get a ProblemDetails body
app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    // Fine for this internal dev environment: the schema is created or updated on every start.
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DocAgentDbContext>().Database.MigrateAsync();

    app.MapOpenApi();            // /openapi/v1.json
}

var api = app.MapGroup("/api");
api.MapRunEndpoints();
api.MapAskEndpoints();
api.MapCatalogEndpoints();
api.MapKnowledgeEndpoints();

app.Run();
