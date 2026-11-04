using DocAgent.Core;
using DocAgent.Fakes;

namespace DocAgent.UnitTests.Fakes;

public class FakeDesignAuthorTests
{
    [Fact]
    public async Task Writes_one_part_per_template_section_in_order()
    {
        var context = await TestData.AuthoringContextAsync(TestData.Pack());

        var result = await new FakeDesignAuthor("specialists").AuthorAsync(context);

        Assert.Equal(context.Template.Sections.Select(s => s.Id), result.Design.Parts.Select(p => p.SectionId));
        Assert.Equal(context.Template.Id, result.Design.TemplateId);
        Assert.Same(context.Detection, result.Design.System);
    }

    [Fact]
    public async Task Each_part_has_an_assumption_naming_the_key_an_evidenced_statement_and_an_open_question()
    {
        var context = await TestData.AuthoringContextAsync(TestData.Pack());

        var result = await new FakeDesignAuthor("specialists").AuthorAsync(context);

        Assert.All(result.Design.Parts, part =>
        {
            Assert.Collection(part.Statements,
                first =>
                {
                    Assert.Equal(Certainty.Assumption, first.Certainty);
                    Assert.Contains("(specialists)", first.Text);
                },
                second =>
                {
                    Assert.Equal(Certainty.Evidenced, second.Certainty);
                    Assert.Equal(new EvidenceRef(EvidenceKind.Model, "entity:work_orders"), Assert.Single(second.Evidence));
                });
            Assert.Single(part.OpenQuestions);
        });
    }

    [Fact]
    public async Task Without_entities_there_is_no_evidenced_statement()
    {
        var context = await TestData.AuthoringContextAsync(TestData.EmptyPack());

        var result = await new FakeDesignAuthor("single-agent").AuthorAsync(context);

        Assert.All(result.Design.Parts, part => Assert.Equal(Certainty.Assumption, Assert.Single(part.Statements).Certainty));
    }

    [Fact]
    public async Task Returns_one_approved_review_and_zero_usage()
    {
        var context = await TestData.AuthoringContextAsync(TestData.Pack());

        var result = await new FakeDesignAuthor("single-agent").AuthorAsync(context);

        var review = Assert.Single(result.Reviews);
        Assert.True(review.Approved);
        Assert.Equal(0.8, review.Score);
        Assert.Empty(review.Issues);
        Assert.Empty(review.Checks);
        Assert.Equal(new RunUsage(0, 0, 0, 0, 0m), result.Usage);
    }
}
