using System.Globalization;
using System.Text;
using DocAgent.Core;

namespace DocAgent.Agents.Rendering;

/// <summary>
/// Renderer v0: turns a structured <see cref="SolutionDesign"/> into Markdown, in code, so formatting never depends
/// on a model. Always real (no flag). Elena wrote v0 for the skeleton; Riste owns it from week 3.
/// </summary>
public sealed class MarkdownDesignRenderer : IDesignRenderer
{
    public string ToMarkdown(SolutionDesign design, DesignTemplate template)
    {
        // "\n" rather than Environment.NewLine: the same design gives the same bytes on every machine.
        var md = new StringBuilder();
        md.Append("# ").Append(design.Title).Append("\n\n");
        md.Append(SystemLine(design)).Append("\n\n");

        var partsBySection = design.Parts.ToLookup(part => part.SectionId);
        foreach (var section in template.Sections)
            AppendSection(md, section.Title, [.. partsBySection[section.Id]], section);

        // Parts for sections the template doesn't know are still shown, so nothing is silently dropped.
        var known = template.Sections.Select(section => section.Id).ToHashSet();
        foreach (var extra in partsBySection.Where(group => !known.Contains(group.Key)))
            AppendSection(md, extra.Key, [.. extra], spec: null);

        return md.ToString().TrimEnd() + "\n";
    }

    private static string SystemLine(SolutionDesign design)
    {
        var system = design.System;
        var confidence = system.Confidence.ToString("0.00", CultureInfo.InvariantCulture);
        var line = $"**System:** {system.DisplayName} (`{system.SystemId}`) · **Confidence:** {confidence}";
        return design.Target is { Length: > 0 } target ? $"{line} · **Target:** {target}" : line;
    }

    private static void AppendSection(StringBuilder md, string title, IReadOnlyList<DesignPart> parts, SectionSpec? spec)
    {
        md.Append("## ").Append(title).Append("\n\n");

        if (parts.Count == 0)
        {
            md.Append("**[TBD]** This section was not drafted.\n\n");
            return;
        }

        foreach (var statement in parts.SelectMany(part => part.Statements))
            md.Append(Statement(statement)).Append("\n\n");

        foreach (var table in parts.SelectMany(part => part.Tables))
            AppendTable(md, table, spec);

        var questions = parts.SelectMany(part => part.OpenQuestions).ToList();
        if (questions.Count > 0)
        {
            md.Append("**Open questions**\n\n");
            foreach (var question in questions)
                md.Append("- ").Append(question.Question).Append(" _Why it matters: ").Append(question.WhyItMatters).Append("_\n");
            md.Append('\n');
        }

        var evidence = parts
            .SelectMany(part => part.Statements.SelectMany(s => s.Evidence).Concat(part.Tables.SelectMany(t => t.Evidence)))
            .Select(reference => reference.Ref)
            .Distinct()
            .ToList();
        md.Append("*Evidence:* ")
          .Append(evidence.Count == 0 ? "none" : string.Join(", ", evidence.Select(id => $"`{id}`")))
          .Append("\n\n");
    }

    private static string Statement(DesignStatement statement) => statement.Certainty switch
    {
        Certainty.Assumption => $"{statement.Text} *(assumption)*",
        Certainty.Unknown => $"**[TBD]** {statement.Text}",
        _ => statement.Text,
    };

    private static void AppendTable(StringBuilder md, DesignTable table, SectionSpec? spec)
    {
        var tableSpec = spec?.Tables.FirstOrDefault(t => t.Id == table.TableId);
        IReadOnlyList<string> columns = tableSpec?.Columns
            ?? [.. Enumerable.Range(1, table.Rows.Select(row => row.Count).DefaultIfEmpty(0).Max()).Select(i => $"Column {i}")];
        if (columns.Count == 0)
            return;

        md.Append("**").Append(tableSpec?.Title ?? table.TableId).Append("**\n\n");
        md.Append("| ").Append(string.Join(" | ", columns.Select(Cell))).Append(" |\n");
        md.Append('|').Append(string.Concat(columns.Select(_ => " --- |"))).Append('\n');
        foreach (var row in table.Rows)
        {
            // Exactly one cell per column; the validator reports rows that don't fit the spec.
            var cells = columns.Select((_, i) => i < row.Count ? Cell(row[i]) : "");
            md.Append("| ").Append(string.Join(" | ", cells)).Append(" |\n");
        }
        md.Append('\n');
    }

    // A pipe or a line break inside a cell would break the Markdown table.
    private static string Cell(string value) =>
        value.Replace("|", "\\|").Replace("\r\n", " ").Replace('\n', ' ');
}
