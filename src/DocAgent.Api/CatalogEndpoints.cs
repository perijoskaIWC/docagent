using DocAgent.Core;
using Microsoft.AspNetCore.Mvc;

namespace DocAgent.Api;

/// <summary>What a run can be made of: the templates and the registered strategies.</summary>
internal static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/templates", (ITemplateProvider templates, CancellationToken ct) => templates.ListAsync(ct))
            .WithTags("Catalog");

        // Every registered StrategyOption grouped by slot, fakes included, so the UI offers only what exists.
        // Run requests are checked against the same list.
        api.MapGet("/strategies", ([FromServices] IEnumerable<StrategyOption> options) =>
                options.GroupBy(option => option.Slot).ToDictionary(group => group.Key, group => group.ToList()))
            .WithTags("Catalog");
    }
}
