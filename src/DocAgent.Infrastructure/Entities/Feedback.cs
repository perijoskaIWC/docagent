namespace DocAgent.Infrastructure.Entities;

/// <summary>A thumbs up or down from the user, for the whole design or one section.</summary>
public sealed class Feedback
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>-1 or +1. A check constraint in the database enforces it.</summary>
    public int Rating { get; set; }

    public string? SectionId { get; set; }
    public string? Comment { get; set; }
}
