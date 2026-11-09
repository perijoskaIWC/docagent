using System.Diagnostics;

namespace DocAgent.Api;

/// <summary>
/// The API's own spans. ServiceDefaults exports the source named after the app ("DocAgent.Api"); guide 2 widens that to
/// "DocAgent.*" once the other projects add sources of their own.
/// </summary>
internal static class Telemetry
{
    public static readonly ActivitySource Source = new("DocAgent.Api");
}
