namespace Demo.Orchestrations;

/// <summary>
/// Keys used to identify which IChatClient an agent talks to.
///
/// The privacy contract for this demo:
///   - <see cref="Local"/> agents run their inference on-device
///     (Apple Intelligence on iOS / macOS / macCatalyst 26+). They are
///     the ONLY agents allowed to call tools that read private data
///     (calendar, contacts, photos, location, files).
///
///   - <see cref="Cloud"/> agents run on Azure OpenAI. They see only
///     what the workflow explicitly forwards to them — never private
///     data, never local-only tools.
///
/// Use these as <c>chatClientServiceKey</c> when registering agents:
///
/// <code>
/// builder.AddAIAgent("local-helpdesk-dispatcher",
///     instructions: "...",
///     chatClientServiceKey: AIModels.Local);
/// </code>
///
/// And resolve in factory overloads with:
///
/// <code>
/// sp.GetRequiredKeyedService&lt;IChatClient&gt;(AIModels.Local)
/// </code>
/// </summary>
public static class AIModels
{
    /// <summary>On-device model (Apple Intelligence Foundation Models).</summary>
    public const string Local = "local-model";

    /// <summary>Cloud model (Azure OpenAI).</summary>
    public const string Cloud = "cloud-model";
}
