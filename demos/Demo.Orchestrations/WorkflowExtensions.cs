using Microsoft.Agents.AI.Workflows;

namespace Demo.Orchestrations;

/// <summary>
/// Helpers for setting internal-init properties on Workflow
/// via reflection.
/// </summary>
internal static class WorkflowExtensions
{
    /// <summary>
    /// Sets the Description property on a Workflow
    /// (which is internal init-only).
    /// </summary>
    public static void SetDescription(
        this Workflow workflow, string description)
    {
        typeof(Workflow)
            .GetProperty(nameof(Workflow.Description))!
            .SetValue(workflow, description);
    }
}
