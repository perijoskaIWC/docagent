using DocAgent.Core;

namespace DocAgent.Fakes;

/// <summary>
/// Writes one placeholder part per template section and approves it. Registered for any key; the key appears in
/// every placeholder so you can see which author ran. Replaced by Boban's and Stevcho's authors when
/// Features:Fakes:Authoring is false.
/// </summary>
public sealed class FakeDesignAuthor(string key) : IDesignAuthor
{
    public Task<AuthoringResult> AuthorAsync(AuthoringContext context, IProgress<RunProgress>? progress = null, CancellationToken ct = default)
    {
        var firstEntity = context.Model.Entities.FirstOrDefault();
        var parts = context.Template.Sections.Select(section => Part(section, firstEntity)).ToList();

        var design = new SolutionDesign(
            Title: $"{context.Template.Name}: {context.Request.Pack.Name}",
            context.Template.Id, context.Request.Target, context.Detection, parts);

        var review = new ReviewResult(Iteration: 1, Approved: true, Score: 0.8,
            Issues: [], Checks: [], Summary: "Approved by the fake reviewer.");

        return Task.FromResult(new AuthoringResult(design, [review], new RunUsage(0, 0, 0, 0, 0m)));
    }

    private DesignPart Part(SectionSpec section, EntityInfo? firstEntity)
    {
        List<DesignStatement> statements =
        [
            new($"{section.Id}.s1", $"Placeholder for {section.Title} ({key})", Certainty.Assumption, Evidence: []),
        ];
        if (firstEntity is not null)
        {
            statements.Add(new($"{section.Id}.s2", $"The export contains the entity {firstEntity.Name}.",
                Certainty.Evidenced, [new EvidenceRef(EvidenceKind.Model, firstEntity.Id)]));
        }

        var question = new OpenQuestion($"{section.Id}.q1",
            $"What should the {section.Title} section say about this system?",
            "The fake author can't tell. A real author would draft it from the model and the knowledge base.");

        return new DesignPart(section.Id, statements, Tables: [], OpenQuestions: [question]);
    }
}
