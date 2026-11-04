using DocAgent.Core;

namespace DocAgent.Fakes;

/// <summary>Finds no issues. Replaced by Riste's validator when Features:Fakes:Profiler is false.</summary>
public sealed class FakeDesignPartValidator : IDesignPartValidator
{
    public IReadOnlyList<ValidationIssue> Validate(DesignPart part, SectionSpec spec, ValidationContext context) => [];
}
