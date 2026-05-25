using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Maui.AI.Agents.DevUI;

/// <summary>
/// Extension methods for registering the MAUI Agent DevUI components.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the MAUI Agent DevUI services with auto-discovery from DI.
    /// Scans the service collection for AIAgent and Workflow registrations.
    /// </summary>
    public static IServiceCollection AddMauiAgentDevUI(this IServiceCollection services)
    {
        // Scan the service collection NOW to capture all keyed AIAgent and Workflow keys.
        // GetKeyedServices<T>(AnyKey) doesn't enumerate all keyed registrations reliably,
        // so we inspect ServiceDescriptors directly.
        var agentKeys = new List<string>();
        var workflowKeys = new List<string>();

        foreach (var descriptor in services)
        {
            if (descriptor.ServiceKey is string key)
            {
                if (descriptor.ServiceType == typeof(AIAgent))
                    agentKeys.Add(key);
                else if (descriptor.ServiceType == typeof(Workflow))
                    workflowKeys.Add(key);
            }
        }

        services.AddSingleton(new DevUIRegisteredKeys(agentKeys, workflowKeys));
        services.AddSingleton<IDevUIEntityRegistry, DevUIEntityRegistry>();
        return services;
    }
}

/// <summary>
/// Provides access to all registered agents and workflows, auto-discovered from DI.
/// </summary>
public interface IDevUIEntityRegistry
{
    /// <summary>Gets all registered agents (standalone, not workflow executors).</summary>
    IReadOnlyList<AgentInfo> Agents { get; }

    /// <summary>Gets all registered workflows.</summary>
    IReadOnlyList<WorkflowInfo> Workflows { get; }
}

/// <summary>
/// Captured keyed service keys from the IServiceCollection at registration time.
/// </summary>
internal sealed record DevUIRegisteredKeys(IReadOnlyList<string> AgentKeys, IReadOnlyList<string> WorkflowKeys);

/// <summary>
/// Auto-discovers AIAgent and Workflow registrations from the DI container
/// using keys captured at registration time.
/// </summary>
internal sealed class DevUIEntityRegistry : IDevUIEntityRegistry
{
    public IReadOnlyList<AgentInfo> Agents { get; }
    public IReadOnlyList<WorkflowInfo> Workflows { get; }

    public DevUIEntityRegistry(IServiceProvider serviceProvider, DevUIRegisteredKeys registeredKeys)
    {
        // Resolve workflows by their captured keys
        var workflowNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var workflowInfos = new List<WorkflowInfo>();

        foreach (var key in registeredKeys.WorkflowKeys)
        {
            try
            {
                var workflow = serviceProvider.GetRequiredKeyedService<Workflow>(key);
                workflowNames.Add(key);
                var info = MapWorkflow(workflow, key, serviceProvider);
                workflowInfos.Add(info);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DevUI] Failed to resolve workflow '{key}': {ex.Message}");
            }
        }

        // Build set of executor agent names (belong to workflows, not standalone)
        // Workflow executor IDs from ReflectEdges() use internal IDs (with GUIDs) that
        // don't match registered agent keys. Instead, filter by prefix: any agent key
        // that starts with a workflow key prefix (e.g. "sequential-newsdesk-") is an executor.
        var workflowPrefixes = workflowNames.Select(n => n + "-").ToList();

        // Resolve agents by their captured keys, filtering out workflow executors and wrappers
        var agentInfos = new List<AgentInfo>();
        foreach (var key in registeredKeys.AgentKeys)
        {
            // Skip agents that are workflow wrappers (same key as a workflow)
            if (workflowNames.Contains(key))
                continue;

            // Skip agents that are executors within workflows (key starts with workflow prefix)
            if (workflowPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                continue;

            // Skip internal pseudo-agents (e.g. HandoffStart entry point)
            if (key.Equals("HandoffStart", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var agent = serviceProvider.GetRequiredKeyedService<AIAgent>(key);
                agentInfos.Add(MapAgent(agent));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DevUI] Failed to resolve agent '{key}': {ex.Message}");
            }
        }

        Agents = agentInfos;
        Workflows = workflowInfos;
    }

    private static AgentInfo MapAgent(AIAgent agent)
    {
        string? instructions = null;
        if (agent is ChatClientAgent chatAgent)
            instructions = chatAgent.Instructions;

        return new AgentInfo
        {
            Id = agent.Name ?? "unknown",
            Name = agent.Name ?? "unknown",
            Description = agent.Description ?? (instructions is { Length: > 80 }
                ? instructions[..80] + "..."
                : instructions),
            Instructions = instructions
        };
    }

    private static WorkflowInfo MapWorkflow(Workflow workflow, string registeredKey, IServiceProvider serviceProvider)
    {
        var name = registeredKey;

        // Get graph structure from ReflectEdges
        var reflectedEdges = workflow.ReflectEdges();
        var executorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var edgeList = new List<Edge>();

        if (workflow.StartExecutorId is not null)
            executorIds.Add(workflow.StartExecutorId);

        foreach (var (sourceId, edgeInfoSet) in reflectedEdges)
        {
            executorIds.Add(sourceId);
            foreach (var edgeInfo in edgeInfoSet)
            {
                foreach (var sinkId in edgeInfo.Connection.SinkIds)
                {
                    executorIds.Add(sinkId);
                    edgeList.Add(new Edge
                    {
                        SourceId = sourceId,
                        TargetId = sinkId
                    });
                }
            }
        }

        var edgeGroups = new List<EdgeGroup>();
        if (edgeList.Count > 0)
        {
            edgeGroups.Add(new EdgeGroup
            {
                Type = InferEdgeGroupType(edgeList),
                Edges = edgeList
            });
        }

        // Build executor list, attempting to get instructions from DI-registered agents
        var executors = executorIds
            .Select(id =>
            {
                string? prompt = null;
                try
                {
                    var agent = serviceProvider.GetKeyedService<AIAgent>(id);
                    if (agent is ChatClientAgent ca)
                        prompt = ca.Instructions;
                }
                catch { /* agent might not be directly resolvable */ }

                return new ExecutorInfo { Id = id, Name = CleanExecutorName(id, registeredKey), SystemPrompt = prompt };
            })
            .ToList();

        var kind = InferOrchestrationKind(edgeList, executors.Count, workflow.StartExecutorId);

        return new WorkflowInfo
        {
            Id = name,
            Name = workflow.Name ?? name,
            Description = workflow.Description,
            Kind = kind,
            StartExecutorId = workflow.StartExecutorId,
            Executors = executors,
            EdgeGroups = edgeGroups
        };
    }

    private static OrchestrationKind InferOrchestrationKind(
        List<Edge> edges, int nodeCount, string? startId)
    {
        if (edges.Count == 0) return OrchestrationKind.Sequential;

        // Check for fan-out from start node
        var startOutEdges = edges.Where(e =>
            string.Equals(e.SourceId, startId, StringComparison.OrdinalIgnoreCase)).ToList();

        if (startOutEdges.Count > 1)
        {
            // If targets of start also have outgoing edges converging → concurrent
            var targets = startOutEdges.Select(e => e.TargetId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var convergeEdges = edges.Where(e => targets.Contains(e.SourceId)).ToList();
            var mergeTargets = convergeEdges.Select(e => e.TargetId).Distinct().ToList();
            if (mergeTargets.Count == 1)
                return OrchestrationKind.Concurrent;

            // Fan-out without convergence → handoff
            return OrchestrationKind.Handoff;
        }

        // Check for cycles (group chat)
        var sources = edges.Select(e => e.SourceId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var targets2 = edges.Select(e => e.TargetId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (sources.SetEquals(targets2) && nodeCount >= 2)
            return OrchestrationKind.GroupChat;

        // Default: linear chain = sequential
        return OrchestrationKind.Sequential;
    }

    private static EdgeGroupType InferEdgeGroupType(List<Edge> edges)
    {
        var sourceCounts = edges.GroupBy(e => e.SourceId).ToDictionary(g => g.Key, g => g.Count());
        if (sourceCounts.Values.Any(c => c > 1))
            return EdgeGroupType.FanOut;

        var targetCounts = edges.GroupBy(e => e.TargetId).ToDictionary(g => g.Key, g => g.Count());
        if (targetCounts.Values.Any(c => c > 1))
            return EdgeGroupType.FanIn;

        return EdgeGroupType.Single;
    }

    /// <summary>
    /// Cleans internal executor IDs (e.g. "sequential_newsdesk_reporter_c3d7e12be5b4...")
    /// into human-readable names by stripping workflow prefix and GUID suffix,
    /// then replacing underscores with spaces and title-casing.
    /// </summary>
    private static string CleanExecutorName(string executorId, string workflowKey)
    {
        var name = executorId;

        // Strip trailing GUID (32 hex chars, no dashes)
        if (name.Length > 32 && name[^32..].All(c => char.IsAsciiHexDigit(c)))
            name = name[..^32].TrimEnd('_');

        // Strip workflow prefix (with underscores)
        var prefix = workflowKey.Replace('-', '_') + "_";
        if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            name = name[prefix.Length..];

        // Replace underscores with spaces and title-case
        var words = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Select(w =>
            w.Length > 0 ? char.ToUpper(w[0]) + w[1..] : w));
    }
}
