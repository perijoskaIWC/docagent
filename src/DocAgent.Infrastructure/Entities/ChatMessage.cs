namespace DocAgent.Infrastructure.Entities;

/// <summary>One turn of an Ask conversation. RunId is null for questions over the knowledge base only.</summary>
public sealed class ChatMessage
{
    public Guid Id { get; set; }
    public Guid? RunId { get; set; }
    public Guid ConversationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>"user" or "assistant", as in <c>ChatTurn</c>.</summary>
    public required string Role { get; set; }

    public required string Text { get; set; }
    public required string CitationsJson { get; set; }
}
