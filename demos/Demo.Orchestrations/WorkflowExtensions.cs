using Microsoft.Agents.AI.Workflows;

namespace Demo.Orchestrations;

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
}
