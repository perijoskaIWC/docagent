using DocAgent.Core;
using DocAgent.Fakes;

namespace DocAgent.UnitTests.Fakes;

public class FakeDesignPartValidatorTests
{
    [Fact]
    public void Finds_no_issues()
    {
        var part = new DesignPart("summary", [], [], []);
        var spec = new SectionSpec("summary", "Summary", "Summary", Required: true, [], []);
        var context = new ValidationContext(new SystemModel(Guid.NewGuid(), [], [], [], [], [], [], []), new HashSet<string>());

        Assert.Empty(new FakeDesignPartValidator().Validate(part, spec, context));
    }
}
