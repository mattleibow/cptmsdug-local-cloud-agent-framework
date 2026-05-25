using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.AI.Agents.DevUI.Controls;
using Microsoft.Maui.AI.Agents.DevUI.Graph;

namespace Microsoft.Maui.AI.Agents.DevUI;

/// <summary>
/// The main Agent DevUI control providing a complete agent testing interface.
/// Auto-discovers agents and workflows from DI when attached to a page.
/// </summary>
public partial class AgentDevUIView : ContentView
{
    private readonly ObservableCollection<DevUIChatMessage> _messages = [];
    private readonly ObservableCollection<DevUIEvent> _events = [];
    private readonly ObservableCollection<DevUITraceSpan> _traces = [];
    private readonly ObservableCollection<DevUIToolCall> _toolCalls = [];
    private readonly ObservableCollection<WorkflowNode> _workflowNodes = [];
    private readonly TimeSpan _uiBufferInterval = TimeSpan.FromMilliseconds(120);
    private const int MaxEventCount = 200;

    private IDevUIEntityRegistry? _registry;
    private IChatClient? _chatClient;
    private bool _isSending;
    private int _totalTokens;
    private AgentInfo? _selectedAgent;
    private WorkflowInfo? _selectedWorkflow;

    #region Bindable Properties

    /// <summary>
    /// The chat client used to communicate with agents.
    /// If not set, resolves from Handler.MauiContext services.
    /// </summary>
    public static readonly BindableProperty ChatClientProperty =
        BindableProperty.Create(nameof(ChatClient), typeof(IChatClient), typeof(AgentDevUIView));

    public IChatClient? ChatClient
    {
        get => (IChatClient?)GetValue(ChatClientProperty);
        set => SetValue(ChatClientProperty, value);
    }

    /// <summary>
    /// Optional external entity registry. If not set, resolves from DI.
    /// </summary>
    public static readonly BindableProperty EntityRegistryProperty =
        BindableProperty.Create(nameof(EntityRegistry), typeof(IDevUIEntityRegistry), typeof(AgentDevUIView),
            propertyChanged: OnEntityRegistryChanged);

    public IDevUIEntityRegistry? EntityRegistry
    {
        get => (IDevUIEntityRegistry?)GetValue(EntityRegistryProperty);
        set => SetValue(EntityRegistryProperty, value);
    }

    #endregion

    public AgentDevUIView()
    {
        Resources.MergedDictionaries.Add(new Resources.DevUIResources());
        InitializeComponent();
        WireControls();
    }

    private void WireControls()
    {
        // Wire chat panel
        ChatPanel.Messages = _messages;
        ChatPanel.IsSending = false;
        ChatPanel.SendCommand = new Command<string>(OnSendMessage);

        // Wire debug panel
        DebugPanel.Events = _events;
        DebugPanel.Traces = _traces;
        DebugPanel.ToolCalls = _toolCalls;

        // Wire selector
        SelectorView.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AgentSelectorView.SelectedEntity))
                OnEntitySelected(SelectorView.SelectedEntity);
        };
    }

    private void OnSendMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        if (_selectedWorkflow is not null)
            _ = RunWorkflowAsync(message, _selectedWorkflow);
        else if (_selectedAgent is not null)
            _ = SendMessageAsync(message, _selectedAgent);
    }

    private void OnEntitySelected(object? entity)
    {
        _selectedAgent = null;
        _selectedWorkflow = null;

        switch (entity)
        {
            case AgentInfo agent:
                _selectedAgent = agent;
                HeaderTitle.Text = agent.Name;
                ChatPanel.EmptyTitle = agent.Name;
                ChatPanel.EmptyDescription = agent.Description;
                ChatPanel.Placeholder = $"Ask {agent.Name}...";
                WorkflowGraphView.IsVisible = false;
                GraphSplitter.IsVisible = false;
                break;

            case WorkflowInfo workflow:
                _selectedWorkflow = workflow;
                HeaderTitle.Text = workflow.Name;
                ChatPanel.EmptyTitle = workflow.Name;
                ChatPanel.EmptyDescription = workflow.Description;
                ChatPanel.Placeholder = $"Start {workflow.Name}...";

                // Show and configure the graph
                WorkflowGraphView.IsVisible = true;
                GraphSplitter.IsVisible = true;
                InitializeWorkflowNodes(workflow);
                RefreshGraph();
                break;
        }

        // Dismiss picker overlay
        PickerOverlay.IsVisible = false;
        ClearConversation();
    }

    private void OnPickerTapped(object? sender, EventArgs e)
    {
        PickerOverlay.IsVisible = !PickerOverlay.IsVisible;
    }

    private void OnPickerDismissed(object? sender, EventArgs e)
    {
        PickerOverlay.IsVisible = false;
    }

    private void RefreshGraph()
    {
        if (_selectedWorkflow is null) return;

        var workflow = _selectedWorkflow;
        var nodes = workflow.Executors
            .Select(e => new GraphNodeDef(e.Id, e.Name))
            .ToList();

        var edges = workflow.EdgeGroups
            .SelectMany(g => g.Edges)
            .Select(e => new GraphEdgeDef(e.SourceId, e.TargetId, e.Condition))
            .ToList();

        // If no edges defined, infer from orchestration kind
        if (edges.Count == 0 && nodes.Count > 0)
        {
            edges = InferEdges(workflow.Kind, workflow.Executors);
        }

        WorkflowGraphView.Graph = new GraphDefinition(nodes, edges);
    }

    private static List<GraphEdgeDef> InferEdges(OrchestrationKind kind, IReadOnlyList<ExecutorInfo> executors)
    {
        var edges = new List<GraphEdgeDef>();
        if (executors.Count < 2) return edges;

        switch (kind)
        {
            case OrchestrationKind.Sequential:
                for (int i = 0; i < executors.Count - 1; i++)
                    edges.Add(new GraphEdgeDef(executors[i].Id, executors[i + 1].Id));
                break;

            case OrchestrationKind.Concurrent:
                // All workers fan into the last node (merger)
                var merger = executors[^1];
                for (int i = 0; i < executors.Count - 1; i++)
                    edges.Add(new GraphEdgeDef(executors[i].Id, merger.Id));
                break;

            case OrchestrationKind.Handoff:
                // First is dispatcher, rest are specialists
                var dispatcher = executors[0];
                for (int i = 1; i < executors.Count; i++)
                    edges.Add(new GraphEdgeDef(dispatcher.Id, executors[i].Id));
                break;

            case OrchestrationKind.GroupChat:
                // Ring: each talks to next, last talks to first
                for (int i = 0; i < executors.Count; i++)
                    edges.Add(new GraphEdgeDef(executors[i].Id, executors[(i + 1) % executors.Count].Id));
                break;
        }

        return edges;
    }

    private void OnClearClicked(object? sender, EventArgs e) => ClearConversation();

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler?.MauiContext?.Services is { } services)
        {
            _registry ??= EntityRegistry ?? services.GetService<IDevUIEntityRegistry>();
            _chatClient ??= ChatClient ?? services.GetService<IChatClient>();
            PopulateEntities();
        }
    }

    private static void OnEntityRegistryChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is AgentDevUIView view && newValue is IDevUIEntityRegistry registry)
        {
            view._registry = registry;
            view.PopulateEntities();
        }
    }

    private void PopulateEntities()
    {
        if (_registry is null) return;
        SelectorView.Agents = _registry.Agents;
        SelectorView.Workflows = _registry.Workflows;
    }

    #region Internal State

    internal bool IsSending
    {
        get => _isSending;
        private set
        {
            _isSending = value;
            OnPropertyChanged(nameof(IsSending));
            MainThread.BeginInvokeOnMainThread(() => ChatPanel.IsSending = value);
        }
    }

    internal int TotalTokens
    {
        get => _totalTokens;
        private set
        {
            _totalTokens = value;
            OnPropertyChanged(nameof(TotalTokens));
            MainThread.BeginInvokeOnMainThread(() => TokenLabel.Text = $"{value} tokens");
        }
    }

    #endregion

    #region Agent Execution

    internal async Task SendMessageAsync(string message, AgentInfo agent)
    {
        if (string.IsNullOrWhiteSpace(message) || IsSending || _chatClient is null)
            return;

        IsSending = true;

        var userMsg = new DevUIChatMessage
        {
            Role = "user",
            Content = message,
            Timestamp = DateTime.Now
        };
        await RunOnUIAsync(() => _messages.Add(userMsg));
        AddEvent("user.message", $"User: {message}");

        try
        {
            var chatMessages = new List<ChatMessage>
            {
                new(ChatRole.System, agent.Instructions ?? "You are a helpful assistant."),
                new(ChatRole.User, message)
            };

            await StreamResponseAsync(agent.Name, chatMessages, null);
        }
        catch (Exception ex)
        {
            AddEvent("error", $"Error: {ex.Message}");
            await RunOnUIAsync(() => _messages.Add(new DevUIChatMessage
            {
                Role = "assistant",
                Content = $"\u2717 Error: {ex.Message}"
            }));
        }
        finally
        {
            IsSending = false;
        }
    }

    internal async Task RunWorkflowAsync(string message, WorkflowInfo workflow)
    {
        if (string.IsNullOrWhiteSpace(message) || IsSending || _chatClient is null)
            return;

        IsSending = true;

        var userMsg = new DevUIChatMessage
        {
            Role = "user",
            Content = message,
            Timestamp = DateTime.Now
        };
        await RunOnUIAsync(() => _messages.Add(userMsg));
        AddEvent("user.message", $"User: {message}");

        try
        {
            // Only reinitialize if nodes don't match (e.g., different workflow selected)
            if (_workflowNodes.Count != workflow.Executors.Count ||
                (_workflowNodes.Count > 0 && _workflowNodes[0].Id != workflow.Executors[0].Id))
            {
                InitializeWorkflowNodes(workflow);
                RefreshGraph();
            }
            else
            {
                // Reset existing nodes to pending
                foreach (var node in _workflowNodes)
                    node.Status = "pending";
            }
            AddEvent("workflow.started", $"{workflow.Kind} orchestration started");
            AddTrace("Agent", $"Orchestration: {workflow.Kind}");

            switch (workflow.Kind)
            {
                case OrchestrationKind.Sequential:
                    await RunSequentialAsync(message, workflow);
                    break;
                case OrchestrationKind.Concurrent:
                    await RunConcurrentAsync(message, workflow);
                    break;
                case OrchestrationKind.Handoff:
                    await RunHandoffAsync(message, workflow);
                    break;
                case OrchestrationKind.GroupChat:
                    await RunGroupChatAsync(message, workflow);
                    break;
            }

            AddEvent("workflow.completed", $"{workflow.Kind} orchestration completed");
        }
        catch (Exception ex)
        {
            AddEvent("error", $"Error: {ex.Message}");
            await RunOnUIAsync(() => _messages.Add(new DevUIChatMessage
            {
                Role = "assistant",
                Content = $"\u2717 Error: {ex.Message}"
            }));
        }
        finally
        {
            IsSending = false;
        }
    }

    private void InitializeWorkflowNodes(WorkflowInfo workflow)
    {
        _workflowNodes.Clear();
        foreach (var exec in workflow.Executors)
        {
            _workflowNodes.Add(new WorkflowNode
            {
                Id = exec.Id,
                Name = exec.Name,
                Status = "pending"
            });
        }
    }

    #endregion

    #region Workflow Orchestrations

    private async Task RunSequentialAsync(string input, WorkflowInfo workflow)
    {
        var current = input;
        for (var i = 0; i < workflow.Executors.Count; i++)
        {
            var exec = workflow.Executors[i];
            var node = _workflowNodes[i];

            await SetNodeRunning(node, exec.Name);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, exec.SystemPrompt ?? "Process the input."),
                new(ChatRole.User, current)
            };

            current = await StreamResponseAsync(exec.Name, messages, null);
            await SetNodeCompleted(node);
        }
    }

    private async Task RunConcurrentAsync(string input, WorkflowInfo workflow)
    {
        var parallelExecutors = workflow.Executors.Take(workflow.Executors.Count - 1).ToList();
        var merger = workflow.Executors[^1];

        // Mark all parallel nodes running
        await RunOnUIAsync(() =>
        {
            for (var i = 0; i < parallelExecutors.Count; i++)
            {
                _workflowNodes[i].Status = "running";
                _workflowNodes[i].StartTime = DateTime.Now;
            }
        });

        // Run in parallel
        var tasks = parallelExecutors.Select((exec, i) => Task.Run(async () =>
        {
            AddEvent("workflow_event.started", $"Parallel: {exec.Name}");
            AddTrace("LLM", $"chat.completion ({exec.Name})");

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, exec.SystemPrompt ?? "Analyze the topic."),
                new(ChatRole.User, input)
            };

            var result = await StreamResponseAsync(exec.Name, messages, null);
            await SetNodeCompleted(_workflowNodes[i]);
            AddEvent("workflow_event.completed", $"Parallel: {exec.Name}");
            return (exec.Name, result);
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        // Run merger
        var mergerNode = _workflowNodes[^1];
        await SetNodeRunning(mergerNode, merger.Name);

        var combined = string.Join("\n\n", results.Select(r => $"--- {r.Name} ---\n{r.result}"));
        var mergerMessages = new List<ChatMessage>
        {
            new(ChatRole.System, merger.SystemPrompt ?? "Synthesize the findings."),
            new(ChatRole.User, combined)
        };

        await StreamResponseAsync(merger.Name, mergerMessages, null);
        await SetNodeCompleted(mergerNode);
    }

    private async Task RunHandoffAsync(string input, WorkflowInfo workflow)
    {
        var triage = workflow.Executors[0];
        var triageNode = _workflowNodes[0];

        await SetNodeRunning(triageNode, triage.Name);

        var triageMessages = new List<ChatMessage>
        {
            new(ChatRole.System, triage.SystemPrompt ?? "Route the request."),
            new(ChatRole.User, input)
        };

        var triageResult = await StreamResponseAsync(triage.Name, triageMessages, null);
        await SetNodeCompleted(triageNode);

        // Determine route from triage result — look for specialist name anywhere in response
        var targetIdx = 1; // default to first specialist
        for (var i = 1; i < workflow.Executors.Count; i++)
        {
            if (triageResult.Contains(workflow.Executors[i].Name, StringComparison.OrdinalIgnoreCase))
            {
                targetIdx = i;
                break;
            }
        }

        AddEvent("handoff", $"Routing \u2192 {workflow.Executors[targetIdx].Name}");

        var specialist = workflow.Executors[targetIdx];
        var specialistNode = _workflowNodes[targetIdx];
        await SetNodeRunning(specialistNode, specialist.Name);

        var specMessages = new List<ChatMessage>
        {
            new(ChatRole.System, specialist.SystemPrompt ?? "Handle the customer issue."),
            new(ChatRole.User, $"Customer issue: {input}\n\nTriage notes: {triageResult}")
        };

        await StreamResponseAsync(specialist.Name, specMessages, null);
        await SetNodeCompleted(specialistNode);

        // Mark others as skipped
        await RunOnUIAsync(() =>
        {
            for (var i = 1; i < _workflowNodes.Count; i++)
            {
                if (_workflowNodes[i].Status == "pending")
                    _workflowNodes[i].Status = "skipped";
            }
        });
    }

    private async Task RunGroupChatAsync(string input, WorkflowInfo workflow)
    {
        var maxRounds = 3;
        var history = new List<ChatMessage>();

        AddEvent("group_chat.started", $"Discussion: {maxRounds} rounds, {workflow.Executors.Count} participants");

        for (var round = 0; round < maxRounds; round++)
        {
            AddEvent("group_chat.round", $"Round {round + 1}/{maxRounds}");

            for (var i = 0; i < workflow.Executors.Count; i++)
            {
                var exec = workflow.Executors[i];
                var node = _workflowNodes[i];

                await SetNodeRunning(node, $"{exec.Name} (round {round + 1})");

                var messages = new List<ChatMessage>
                {
                    new(ChatRole.System, exec.SystemPrompt ?? "Contribute to the discussion.")
                };

                if (history.Count == 0)
                    messages.Add(new ChatMessage(ChatRole.User, $"Topic: {input}"));
                else
                {
                    messages.Add(new ChatMessage(ChatRole.User,
                        $"Topic: {input}\n\nDiscussion so far:\n{string.Join("\n", history.Select(m => m.Text))}"));
                    messages.Add(new ChatMessage(ChatRole.User, "Provide your contribution for this round."));
                }

                var response = await StreamResponseAsync(exec.Name, messages, null);
                history.Add(new ChatMessage(ChatRole.Assistant, $"{exec.Name}: {response}"));

                await RunOnUIAsync(() => node.Status = "completed");
            }
        }

        await RunOnUIAsync(() =>
        {
            foreach (var node in _workflowNodes)
            {
                node.Status = "completed";
                node.EndTime ??= DateTime.Now;
            }
        });
    }

    #endregion

    #region Streaming

    private async Task<string> StreamResponseAsync(string agentName, List<ChatMessage> messages, ChatOptions? options)
    {
        var msg = new DevUIChatMessage
        {
            Role = "assistant",
            Content = "",
            AgentLabel = agentName,
            Timestamp = DateTime.Now,
            IsStreaming = true
        };
        await RunOnUIAsync(() => _messages.Add(msg));

        var responseBuilder = new System.Text.StringBuilder();
        var lastUIUpdate = DateTime.UtcNow;
        var traceStart = DateTime.UtcNow;

        await foreach (var update in _chatClient!.GetStreamingResponseAsync(messages, options))
        {
            if (update.Text is { Length: > 0 } text)
            {
                responseBuilder.Append(text);
                if (DateTime.UtcNow - lastUIUpdate >= _uiBufferInterval)
                {
                    var snapshot = responseBuilder.ToString() + " \u258D"; // ▍ cursor
                    await RunOnUIAsync(() => msg.Content = snapshot);
                    lastUIUpdate = DateTime.UtcNow;
                }
            }

            if (update.Contents?.OfType<FunctionCallContent>().Any() == true)
            {
                foreach (var fc in update.Contents.OfType<FunctionCallContent>())
                {
                    var argsJson = FormatArguments(fc.Arguments);
                    AddEvent("function_call", $"fn: {fc.Name}({argsJson})");
                    AddTrace("Tool", fc.Name);
                    var toolCall = new DevUIToolCall
                    {
                        Name = fc.Name,
                        Arguments = argsJson,
                        Timestamp = DateTime.Now
                    };
                    await RunOnUIAsync(() => _toolCalls.Add(toolCall));
                }
            }
        }

        var response = responseBuilder.ToString();
        var tokenEst = response.Length > 0 ? response.Split(' ').Length * 2 : 0;
        var traceDuration = (int)(DateTime.UtcNow - traceStart).TotalMilliseconds;
        await RunOnUIAsync(() =>
        {
            msg.Content = response;
            msg.IsStreaming = false;
            msg.TokenCount = tokenEst;
            TotalTokens += tokenEst;
        });

        AddTrace("LLM", $"completion ({agentName})", traceDuration);
        return response;
    }

    #endregion

    #region Helpers

    private async Task SetNodeRunning(WorkflowNode node, string label)
    {
        AddEvent("workflow_event.started", $"\u25B6 {label}");
        AddTrace("Agent", $"execute ({label})");
        await RunOnUIAsync(() =>
        {
            node.Status = "running";
            node.StartTime = DateTime.Now;
        });
    }

    private async Task SetNodeCompleted(WorkflowNode node)
    {
        await RunOnUIAsync(() =>
        {
            node.Status = "completed";
            node.EndTime = DateTime.Now;
        });
    }

    internal void AddEvent(string type, string description)
    {
        var evt = new DevUIEvent { Type = type, Description = description };
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _events.Insert(0, evt);
            if (_events.Count > MaxEventCount)
                _events.RemoveAt(_events.Count - 1);
        });
    }

    internal void AddTrace(string kind, string name, int? durationMs = null)
    {
        var trace = new DevUITraceSpan
        {
            Name = name,
            OperationKind = kind,
            Duration = durationMs ?? 0
        };
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _traces.Insert(0, trace);
            if (_traces.Count > MaxEventCount)
                _traces.RemoveAt(_traces.Count - 1);
        });
    }

    internal void ClearConversation()
    {
        _messages.Clear();
        _events.Clear();
        _toolCalls.Clear();
        _traces.Clear();
        _workflowNodes.Clear();
        TotalTokens = 0;
    }

    private static Task RunOnUIAsync(Action action)
    {
        var tcs = new TaskCompletionSource();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try { action(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    private static string FormatArguments(IDictionary<string, object?>? args)
    {
        if (args is null || args.Count == 0) return "";
        try { return JsonSerializer.Serialize(args, new JsonSerializerOptions { WriteIndented = false }); }
        catch { return string.Join(", ", args.Select(kv => $"{kv.Key}: {kv.Value}")); }
    }

    #endregion
}
