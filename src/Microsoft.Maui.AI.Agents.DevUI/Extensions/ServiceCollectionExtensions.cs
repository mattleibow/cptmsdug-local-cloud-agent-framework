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
    /// Agents and workflows registered via AddAIAgent/AddWorkflow are discovered automatically.
    /// </summary>
    public static IServiceCollection AddMauiAgentDevUI(this IServiceCollection services)
    {
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
/// Auto-discovers AIAgent and Workflow registrations from the DI container,
/// mirroring the pattern used by the web DevUI (EntitiesApiExtensions).
/// </summary>
internal sealed class DevUIEntityRegistry : IDevUIEntityRegistry
{
    public IReadOnlyList<AgentInfo> Agents { get; }
    public IReadOnlyList<WorkflowInfo> Workflows { get; }

    public DevUIEntityRegistry(IServiceProvider serviceProvider)
    {
        // Discover all Workflow instances from DI (keyed + default)
        var workflows = GetRegisteredEntities<Workflow>(serviceProvider).ToList();
        var workflowNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var workflowInfos = new List<WorkflowInfo>();
        foreach (var workflow in workflows)
        {
            var name = workflow.Name ?? workflow.StartExecutorId ?? "unnamed";
            workflowNames.Add(name);

            var info = MapWorkflow(workflow, serviceProvider);
            workflowInfos.Add(info);
        }

        // Discover all AIAgent instances (keyed + default), excluding workflow-wrapped agents
        var agents = GetRegisteredEntities<AIAgent>(serviceProvider)
            .Where(a => a.Name is not null && !workflowNames.Contains(a.Name))
            .ToList();

        // Also exclude agents that are executors within workflows
        var executorNames = workflowInfos
            .SelectMany(w => w.Executors)
            .Select(e => e.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var agentInfos = agents
            .Where(a => !executorNames.Contains(a.Name!))
            .Select(MapAgent)
            .ToList();

        Agents = agentInfos;
        Workflows = workflowInfos;
    }

    private static IEnumerable<T> GetRegisteredEntities<T>(IServiceProvider serviceProvider)
    {
        var keyedEntities = serviceProvider.GetKeyedServices<T>(KeyedService.AnyKey);
        var defaultEntities = serviceProvider.GetServices<T>() ?? [];
        return keyedEntities.Concat(defaultEntities).Where(entity => entity is not null);
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

    private static WorkflowInfo MapWorkflow(Workflow workflow, IServiceProvider serviceProvider)
    {
        var name = workflow.Name ?? workflow.StartExecutorId ?? "unnamed";

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
                var agent = serviceProvider.GetKeyedService<AIAgent>(id);
                if (agent is ChatClientAgent ca)
                    prompt = ca.Instructions;

                return new ExecutorInfo { Id = id, Name = id, SystemPrompt = prompt };
            })
            .ToList();

        var kind = InferOrchestrationKind(edgeList, executors.Count, workflow.StartExecutorId);

        return new WorkflowInfo
        {
            Id = name,
            Name = name,
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
}
