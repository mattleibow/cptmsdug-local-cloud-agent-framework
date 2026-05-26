using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
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
                LeftArea.ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Star),
                };
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
                LeftArea.ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(new GridLength(1, GridUnitType.Absolute)),
                    new ColumnDefinition(GridLength.Star),
                };
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
            .Select(e => new GraphNodeDef(e.Id, e.Name, e.Description))
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
        if (string.IsNullOrWhiteSpace(message) || IsSending)
            return;

        // We resolve the Workflow (not AIAgent) because InProcessExecution provides granular
        // per-executor events (invoked, completed, streaming updates) needed for live graph
        // visualization. The web DevUI server uses agent.RunStreamingAsync() which only yields
        // the final output — suitable for HTTP/SSE but not for live graph status updates.
        var services = Handler?.MauiContext?.Services;
        var workflowInstance = services?.GetKeyedService<Workflow>(workflow.Id);
        if (workflowInstance is null)
        {
            AddEvent("error", $"No Workflow registered for key '{workflow.Id}'");
            return;
        }

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
                {
                    node.Status = "pending";
                    WorkflowGraphView.UpdateNodeStatus(node.Id, "pending");
                }
            }
            AddEvent("workflow.started", $"{workflow.Kind} orchestration started");
            AddTrace("Agent", $"Orchestration: {workflow.Kind}");

            // Execute via InProcessExecution for per-executor event visibility
            await RunWorkflowStreamingAsync(workflowInstance, message);

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

    #region Workflow Execution (Agent Framework)

    /// <summary>
    /// Runs a workflow via InProcessExecution.RunStreamingAsync which provides granular per-executor
    /// events (ExecutorInvoked, AgentResponseUpdate, ExecutorCompleted) needed for live graph status.
    /// 
    /// The web DevUI server uses a different approach: it resolves the workflow-as-AIAgent
    /// (via AddAsAIAgent/WorkflowHostAgent) and calls agent.RunStreamingAsync(). That approach
    /// only yields the final aggregated output — suitable for HTTP/SSE clients but insufficient
    /// for our live per-node graph visualization.
    /// </summary>
    private async Task RunWorkflowStreamingAsync(Workflow workflow, string input)
    {
        // Pass input as ChatMessage list — ChatForwardingExecutor (used in concurrent workflows)
        // only handles ChatMessage types, not raw strings.
        var inputMessages = new List<ChatMessage> { new(ChatRole.User, input) };
        await using var run = await InProcessExecution.RunStreamingAsync(workflow, inputMessages);

        // Chat-protocol executors require a TurnToken to begin processing accumulated messages.
        await run.TrySendMessageAsync(new TurnToken(true));

        // Track the current streaming message per executor
        var activeMessages = new Dictionary<string, DevUIChatMessage>();
        var responseBuilders = new Dictionary<string, System.Text.StringBuilder>();
        var lastUIUpdate = DateTime.UtcNow;

        await foreach (var evt in run.WatchStreamAsync())
        {
            switch (evt)
            {
                case ExecutorInvokedEvent invoked:
                {
                    var executorId = invoked.ExecutorId;
                    var node = FindNodeByExecutorId(executorId);
                    if (node is not null)
                    {
                        AddEvent("executor.invoked", $"\u25B6 {node.Name}");
                        AddTrace("Agent", $"execute ({node.Name})");
                        await RunOnUIAsync(() =>
                        {
                            node.Status = "running";
                            node.StartTime = DateTime.Now;
                            WorkflowGraphView.UpdateNodeStatus(node.Id, "running");
                        });
                    }
                    // A previous invocation of THIS executor may have left a finalized
                    // message in our maps. Drop it so a new bubble is created on the next
                    // streaming update (rather than appending to / duplicating the old one).
                    activeMessages.Remove(executorId);
                    responseBuilders.Remove(executorId);
                    break;
                }

                case ExecutorCompletedEvent completed:
                {
                    var executorId = completed.ExecutorId;
                    var node = FindNodeByExecutorId(executorId);
                    if (node is not null)
                    {
                        AddEvent("executor.completed", $"\u2714 {node.Name}");
                        await RunOnUIAsync(() =>
                        {
                            node.Status = "completed";
                            node.EndTime = DateTime.Now;
                            WorkflowGraphView.UpdateNodeStatus(node.Id, "completed");
                        });
                    }

                    // Finalize any active streaming message for this executor
                    if (activeMessages.TryGetValue(executorId, out var msg))
                    {
                        var finalText = responseBuilders.TryGetValue(executorId, out var sb)
                            ? sb.ToString() : msg.Content;
                        var tokenEst = finalText.Length > 0 ? finalText.Split(' ').Length * 2 : 0;
                        await RunOnUIAsync(() =>
                        {
                            msg.Content = finalText;
                            msg.IsStreaming = false;
                            msg.TokenCount = tokenEst;
                            TotalTokens += tokenEst;
                        });
                        activeMessages.Remove(executorId);
                        responseBuilders.Remove(executorId);
                    }
                    break;
                }

                case AgentResponseUpdateEvent responseUpdate:
                {
                    var executorId = responseUpdate.ExecutorId;
                    var update = responseUpdate.Update;
                    var text = update.Text;

                    // Create a chat message for this executor if we don't have one
                    if (!activeMessages.TryGetValue(executorId, out var msg))
                    {
                        var node = FindNodeByExecutorId(executorId);
                        var label = node?.Name ?? executorId;
                        msg = new DevUIChatMessage
                        {
                            Role = "assistant",
                            Content = "",
                            AgentLabel = label,
                            Timestamp = DateTime.Now,
                            IsStreaming = true
                        };
                        activeMessages[executorId] = msg;
                        responseBuilders[executorId] = new System.Text.StringBuilder();
                        await RunOnUIAsync(() => _messages.Add(msg));
                    }

                    // Append streaming text
                    if (text is { Length: > 0 })
                    {
                        var sb = responseBuilders[executorId];
                        sb.Append(text);
                        if (DateTime.UtcNow - lastUIUpdate >= _uiBufferInterval)
                        {
                            var snapshot = sb.ToString() + " \u258D";
                            await RunOnUIAsync(() => msg.Content = snapshot);
                            lastUIUpdate = DateTime.UtcNow;
                        }
                    }

                    // Handle function calls
                    if (update.Contents?.OfType<FunctionCallContent>().Any() == true)
                    {
                        foreach (var fc in update.Contents.OfType<FunctionCallContent>())
                        {
                            var argsJson = FormatArguments(fc.Arguments);
                            AddEvent("function_call", $"fn: {fc.Name}({argsJson})");
                            AddTrace("Tool", fc.Name);
                            await RunOnUIAsync(() => _toolCalls.Add(new DevUIToolCall
                            {
                                Name = fc.Name,
                                Arguments = argsJson,
                                CallId = fc.CallId,
                                Timestamp = DateTime.Now
                            }));
                        }
                    }

                    // Handle function results
                    if (update.Contents?.OfType<FunctionResultContent>().Any() == true)
                    {
                        foreach (var fr in update.Contents.OfType<FunctionResultContent>())
                        {
                            var resultText = fr.Result?.ToString() ?? "";
                            var truncated = resultText.Length > 200 ? resultText[..200] + "..." : resultText;
                            AddEvent("function_result", $"fn: {fr.CallId} → {truncated}");

                            // Match result to existing tool call by CallId (store FULL result)
                            await RunOnUIAsync(() =>
                            {
                                var match = _toolCalls.FirstOrDefault(tc => tc.CallId == fr.CallId);
                                if (match != null)
                                    match.Result = resultText;
                            });
                        }
                    }
                    break;
                }

                case AgentResponseEvent responseComplete:
                {
                    var executorId = responseComplete.ExecutorId;
                    var node = FindNodeByExecutorId(executorId);
                    var label = node?.Name ?? executorId;

                    // If no streaming message was created, use the complete response
                    if (!activeMessages.TryGetValue(executorId, out var msg))
                    {
                        var responseText = responseComplete.Response?.Messages
                            ?.SelectMany(m => m.Contents?.OfType<TextContent>() ?? [])
                            .Select(t => t.Text)
                            .Where(t => !string.IsNullOrEmpty(t))
                            .FirstOrDefault() ?? "";

                        if (!string.IsNullOrEmpty(responseText))
                        {
                            var tokenEst = responseText.Split(' ').Length * 2;
                            var finalMsg = new DevUIChatMessage
                            {
                                Role = "assistant",
                                Content = responseText,
                                AgentLabel = label,
                                Timestamp = DateTime.Now,
                                IsStreaming = false,
                                TokenCount = tokenEst
                            };
                            TotalTokens += tokenEst;
                            await RunOnUIAsync(() => _messages.Add(finalMsg));
                        }
                    }
                    else
                    {
                        var finalText = responseBuilders.TryGetValue(executorId, out var sb)
                            ? sb.ToString() : msg.Content;
                        var tokenEst = finalText.Length > 0 ? finalText.Split(' ').Length * 2 : 0;
                        await RunOnUIAsync(() =>
                        {
                            msg.Content = finalText;
                            msg.IsStreaming = false;
                            msg.TokenCount = tokenEst;
                            TotalTokens += tokenEst;
                        });
                        activeMessages.Remove(executorId);
                        responseBuilders.Remove(executorId);
                    }
                    break;
                }

                case ExecutorFailedEvent failed:
                {
                    var node = FindNodeByExecutorId(failed.ExecutorId);
                    var label = node?.Name ?? failed.ExecutorId;
                    var details = failed.Data?.ToString() ?? "(no details)";
                    AddEvent("executor.failed", $"\u2717 {label}: {details}");
                    if (node is not null)
                    {
                        await RunOnUIAsync(() =>
                        {
                            node.Status = "failed";
                            node.EndTime = DateTime.Now;
                            WorkflowGraphView.UpdateNodeStatus(node.Id, "failed");
                        });
                    }
                    // Surface the failure in the chat so the user can see what went wrong
                    await RunOnUIAsync(() => _messages.Add(new DevUIChatMessage
                    {
                        Role = "assistant",
                        Content = $"\u2717 **{label} failed**\n\n```\n{details}\n```",
                        AgentLabel = label,
                        Timestamp = DateTime.Now
                    }));
                    break;
                }

                case WorkflowErrorEvent errorEvt:
                {
                    var details = errorEvt.Data?.ToString() ?? "(no details)";
                    AddEvent("workflow.error", $"Error: {details}");
                    await RunOnUIAsync(() => _messages.Add(new DevUIChatMessage
                    {
                        Role = "assistant",
                        Content = $"\u2717 **Workflow error**\n\n```\n{details}\n```",
                        AgentLabel = "Workflow",
                        Timestamp = DateTime.Now
                    }));
                    break;
                }

                case WorkflowStartedEvent:
                    AddEvent("workflow.running", "Workflow execution started");
                    break;

                case WorkflowOutputEvent output:
                {
                    // The workflow's final aggregated result.
                    // We log it as an event but DO NOT add a chat bubble — for every workflow
                    // shape we currently support, the per-executor AgentResponseEvent chat
                    // bubbles already show what the user needs. Re-adding the final output
                    // would be a duplicate (the synthesizer's bubble in sequential, the
                    // specialist's bubble in handoff, or the concatenated specialist content
                    // in concurrent).
                    string? consolidatedText = null;
                    if (output.Data is List<ChatMessage> msgs)
                    {
                        var lastAssistant = msgs.LastOrDefault(m => m.Role == ChatRole.Assistant);
                        consolidatedText = lastAssistant?.Text;
                    }
                    else if (output.Data is ChatMessage singleMsg)
                    {
                        consolidatedText = singleMsg.Text;
                    }
                    else if (output.Data is string s)
                    {
                        consolidatedText = s;
                    }

                    if (!string.IsNullOrWhiteSpace(consolidatedText))
                        AddEvent("workflow.output", $"Final result ({consolidatedText.Length} chars)");
                    break;
                }
            }
        }

        // Mark any remaining pending nodes as skipped
        await RunOnUIAsync(() =>
        {
            foreach (var node in _workflowNodes)
            {
                if (node.Status == "pending")
                {
                    node.Status = "skipped";
                    WorkflowGraphView.UpdateNodeStatus(node.Id, "skipped");
                }
            }
        });
    }

    private WorkflowNode? FindNodeByExecutorId(string executorId)
    {
        // Direct match first
        foreach (var node in _workflowNodes)
        {
            if (node.Id == executorId)
                return node;
        }

        // MAF executor IDs use underscores and GUIDs: e.g. "handoff_helpdesk_network_293b..."
        // Our registered keys use dashes: "handoff-helpdesk-network"
        foreach (var node in _workflowNodes)
        {
            var normalized = node.Id.Replace("-", "_");
            if (executorId.StartsWith(normalized, StringComparison.OrdinalIgnoreCase) &&
                (executorId.Length == normalized.Length || executorId[normalized.Length] == '_'))
                return node;
        }

        return null;
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
                        CallId = fc.CallId,
                        Timestamp = DateTime.Now
                    };
                    await RunOnUIAsync(() => _toolCalls.Add(toolCall));
                }
            }

            if (update.Contents?.OfType<FunctionResultContent>().Any() == true)
            {
                foreach (var fr in update.Contents.OfType<FunctionResultContent>())
                {
                    var resultText = fr.Result?.ToString() ?? "";
                    var truncated = resultText.Length > 200 ? resultText[..200] + "..." : resultText;
                    AddEvent("function_result", $"fn: {fr.CallId} → {truncated}");
                    await RunOnUIAsync(() =>
                    {
                        var match = _toolCalls.FirstOrDefault(tc => tc.CallId == fr.CallId);
                        if (match != null)
                            match.Result = resultText;
                    });
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
