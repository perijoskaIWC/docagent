using System.Runtime.CompilerServices;
using System.Text;
using DocAgent.Core;
using DocAgent.Infrastructure;
using DocAgent.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using static DocAgent.Api.ApiResults;
using static DocAgent.Api.ApiText;

namespace DocAgent.Api;

/// <summary>POST /api/ask. RunId null asks the knowledge base only; ConversationId null starts a new conversation.</summary>
internal sealed record AskRequest(string? Question, Guid? RunId = null, Guid? ConversationId = null);

internal static class AskEndpoints
{
    public const string ConversationHeader = "X-Conversation-Id";
    private const int MaxQuestionLength = 4000;

    public static void MapAskEndpoints(this IEndpointRouteBuilder api) =>
        api.MapPost("/ask", AskAsync).WithTags("Ask");

    /// <summary>
    /// The answer streams back as Server-Sent Events, one ChatDelta per event. The conversation id comes back in the
    /// X-Conversation-Id header; send it as conversationId to ask a follow-up.
    /// </summary>
    private static async Task<IResult> AskAsync(AskRequest body, HttpContext http, DocAgentDbContext db, IRunChat chat,
        CancellationToken ct)
    {
        var question = NullIfBlank(body.Question);
        if (question is null)
            return Invalid("question", "Ask a question.");
        if (question.Length > MaxQuestionLength)
            return Invalid("question", $"Keep the question under {MaxQuestionLength} characters.");
        if (body.RunId is { } runId && !await db.Runs.AnyAsync(r => r.Id == runId, ct))
            return RunNotFound(runId);

        var conversationId = body.ConversationId ?? Guid.CreateVersion7();
        var history = await db.ChatMessages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ChatTurn(m.Role, m.Text))
            .ToListAsync(ct);

        http.Response.Headers[ConversationHeader] = conversationId.ToString();
        // ct is the request's token: when the browser tab closes, the model call is cancelled with it.
        return TypedResults.ServerSentEvents(StreamAsync(chat, db, body.RunId, conversationId, question, history, ct));
    }

    private static async IAsyncEnumerable<ChatDelta> StreamAsync(IRunChat chat, DocAgentDbContext db, Guid? runId,
        Guid conversationId, string question, IReadOnlyList<ChatTurn> history, [EnumeratorCancellation] CancellationToken ct)
    {
        var askedAt = DateTimeOffset.UtcNow;
        var answer = new StringBuilder();
        var saved = false;

        await foreach (var delta in chat.AskAsync(runId, question, history, ct))
        {
            answer.Append(delta.Text);
            // Saved before the final delta goes out, so a follow-up sent the moment it arrives finds both turns in the
            // history. A stream cancelled before the end saves nothing: half an answer isn't history.
            if (delta.Done && !saved)
            {
                await SaveAsync(delta.Citations ?? []);
                saved = true;
            }
            yield return delta;
        }

        if (!saved)
            await SaveAsync([]);   // the chat ended without a Done delta

        async Task SaveAsync(IReadOnlyList<ChatCitation> citations)
        {
            db.ChatMessages.AddRange(
                new ChatMessage
                {
                    Id = Guid.CreateVersion7(), RunId = runId, ConversationId = conversationId, CreatedAt = askedAt,
                    Role = "user", Text = question, CitationsJson = DocAgentJson.Serialize<IReadOnlyList<ChatCitation>>([]),
                },
                new ChatMessage
                {
                    Id = Guid.CreateVersion7(), RunId = runId, ConversationId = conversationId, CreatedAt = DateTimeOffset.UtcNow,
                    Role = "assistant", Text = answer.ToString(), CitationsJson = DocAgentJson.Serialize(citations),
                });
            await db.SaveChangesAsync(ct);
        }
    }
}
