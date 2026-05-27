using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace Demo.MauiAgentApp.Orchestrations;

/// <summary>
/// Helpers for setting init-only properties on Workflow via reflection.
/// Used only for workflow shapes whose builder (e.g. BuildSequential,
/// BuildConcurrent) doesn't expose a fluent WithDescription option.
/// </summary>
internal static class WorkflowExtensions
{
    /// <summary>
    /// Sets the Description on a Workflow and returns it for chaining.
    /// </summary>
    public static Workflow SetDescription(this Workflow workflow, string description)
    {
        typeof(Workflow)
            .GetProperty(nameof(Workflow.Description))!
            .SetValue(workflow, description);
        return workflow;
    }

    /// <summary>
    /// Wraps an <see cref="AIAgent"/> in <c>UseOpenTelemetry</c> with
    /// <c>EnableSensitiveData = true</c> so the demo dashboard shows
    /// per-agent <c>invoke_agent &lt;name&gt;</c> spans with prompt + response
    /// content. <strong>Dev only</strong> — never ship sensitive-data export
    /// with real user data flowing through the pipeline.
    ///
    /// Apply this once per <c>(sp, key) =&gt; new ChatClientAgent(...)</c>
    /// factory passed to <c>AddAIAgent</c>.
    /// </summary>
    public static AIAgent WithTelemetry(this AIAgent agent)
        => agent
            .AsBuilder()
            .UseOpenTelemetry(
                sourceName: "Demo.MauiAgentApp",
                configure: o => o.EnableSensitiveData = true)
            .Build();
}

