using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Maui.AI.Agents.DevUI;

/// <summary>
/// Extension methods for registering the MAUI Agent DevUI components.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the MAUI Agent DevUI services including agent and workflow discovery.
    /// </summary>
    public static IServiceCollection AddMauiAgentDevUI(this IServiceCollection services)
    {
        services.AddSingleton<IDevUIEntityRegistry, DevUIEntityRegistry>();
        return services;
    }

    /// <summary>
    /// Registers a single agent with the DevUI.
    /// </summary>
    public static IServiceCollection AddDevUIAgent(this IServiceCollection services, AgentInfo agent)
    {
        services.AddSingleton<IDevUIEntityRegistration>(new AgentRegistration(agent));
        return services;
    }

    /// <summary>
    /// Registers a workflow with the DevUI.
    /// </summary>
    public static IServiceCollection AddDevUIWorkflow(this IServiceCollection services, WorkflowInfo workflow)
    {
        services.AddSingleton<IDevUIEntityRegistration>(new WorkflowRegistration(workflow));
        return services;
    }
}

/// <summary>
/// Provides access to all registered agents and workflows.
/// </summary>
public interface IDevUIEntityRegistry
{
    /// <summary>Gets all registered agents.</summary>
    IReadOnlyList<AgentInfo> Agents { get; }

    /// <summary>Gets all registered workflows.</summary>
    IReadOnlyList<WorkflowInfo> Workflows { get; }
}

/// <summary>
/// Marker interface for entity registrations resolved from DI.
/// </summary>
public interface IDevUIEntityRegistration;

internal sealed class AgentRegistration(AgentInfo agent) : IDevUIEntityRegistration
{
    public AgentInfo Agent => agent;
}

internal sealed class WorkflowRegistration(WorkflowInfo workflow) : IDevUIEntityRegistration
{
    public WorkflowInfo Workflow => workflow;
}

internal sealed class DevUIEntityRegistry : IDevUIEntityRegistry
{
    public IReadOnlyList<AgentInfo> Agents { get; }
    public IReadOnlyList<WorkflowInfo> Workflows { get; }

    public DevUIEntityRegistry(IEnumerable<IDevUIEntityRegistration> registrations)
    {
        var agents = new List<AgentInfo>();
        var workflows = new List<WorkflowInfo>();

        foreach (var reg in registrations)
        {
            if (reg is AgentRegistration ar)
                agents.Add(ar.Agent);
            else if (reg is WorkflowRegistration wr)
                workflows.Add(wr.Workflow);
        }

        Agents = agents;
        Workflows = workflows;
    }
}
