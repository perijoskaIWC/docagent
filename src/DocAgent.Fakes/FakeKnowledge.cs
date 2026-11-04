using DocAgent.Core;

namespace DocAgent.Fakes;

/// <summary>Six canned, synthetic knowledge chunks shared by the knowledge fakes and the chat fake.</summary>
internal static class FakeKnowledge
{
    public static readonly IReadOnlyList<KnowledgeChunk> Chunks =
    [
        Chunk("profile-quarry-wms", KnowledgeKind.SystemProfile, "Quarry WMS profile",
            "Quarry WMS is a fictional warehouse management system. Its exports contain CSV extracts of stock items " +
            "and outbound shipments, an XSD for shipment XML and a nightly job schedule.",
            ("systemId", "quarry-wms")),
        Chunk("profile-helio-service", KnowledgeKind.SystemProfile, "Helio Service profile",
            "Helio Service is a fictional field service and ticketing SaaS. It is API-first: an OpenAPI 3 specification " +
            "describes work orders and technicians, and webhooks notify other systems when a ticket changes.",
            ("systemId", "helio-service")),
        Chunk("pattern-file-feed", KnowledgeKind.Pattern, "File feed integration",
            "A file feed moves data in scheduled batches. Pick each file up from a landing area, validate it against " +
            "its schema and archive the original for reconciliation."),
        Chunk("pattern-webhook", KnowledgeKind.Pattern, "Webhook integration",
            "A webhook pushes an event to a subscriber as soon as data changes. Verify the signature, respond quickly " +
            "and process the event asynchronously from a queue."),
        Chunk("standard-evidence", KnowledgeKind.Standard, "Evidence in designs",
            "Every statement in a design cites the model or knowledge it is based on. When there is no evidence, mark " +
            "the statement as an assumption or raise an open question."),
        Chunk("lesson-check-keys", KnowledgeKind.Lesson, "Check keys before mapping",
            "Before writing a data mapping, confirm which field is the key of each entity. Mark inferred keys as assumptions."),
    ];

    private static KnowledgeChunk Chunk(string documentId, KnowledgeKind kind, string title, string content,
        params (string Key, string Value)[] tags) =>
        new($"{documentId}#1", documentId, kind, title, content, $"fake://knowledge/{documentId}", Score: 0,
            tags.ToDictionary(tag => tag.Key, tag => tag.Value));
}
