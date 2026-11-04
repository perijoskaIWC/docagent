namespace DocAgent.Fakes;

/// <summary>Ids in the format of 00-team-overview §6.2: names lower-cased, spaces and dashes turned into "_".</summary>
internal static class FakeIds
{
    public static string Normalize(string name) =>
        name.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');

    public static string Entity(string entity) => $"entity:{Normalize(entity)}";

    public static string Field(string entity, string field) => $"field:{Normalize(entity)}.{Normalize(field)}";
}
