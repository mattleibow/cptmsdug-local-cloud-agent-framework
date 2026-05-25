using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Demo2.MauiAgent.Models;
using Demo2.MauiAgent.Orchestrations;
using Demo2.MauiAgent.Services;
using Microsoft.Extensions.AI;

namespace Demo2.MauiAgent.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly AIChatService _aiService;
    private readonly SequentialOrchestration _sequential;
    private readonly ConcurrentOrchestration _concurrent;
    private readonly HandoffOrchestration _handoff;
    private readonly GroupChatOrchestration _groupChat;
    private readonly TimeSpan _uiBufferInterval = TimeSpan.FromMilliseconds(120);
    private DateTime? _sessionStartTime;

    [ObservableProperty]
    private string _messageText = string.Empty;

    [ObservableProperty]
    private string _selectedAgent = "writer";

    [ObservableProperty]
    private bool _isSending;

    [ObservableProperty]
    private int _totalTokens;

    [ObservableProperty]
    private int _eventCount;

    [ObservableProperty]
    private int _traceCount;

    [ObservableProperty]
    private string _workflowTitle = "";

    [ObservableProperty]
    private bool _isWorkflow;

    [ObservableProperty]
    private string _orchestrationKindLabel = "";

    [ObservableProperty]
    private string _elapsedTime = "0.0s";

    // Tab state
    [ObservableProperty]
    private bool _isEventsTab = true;

    [ObservableProperty]
    private bool _isTracesTab;

    [ObservableProperty]
    private bool _isToolsTab;

    // Theme
    [ObservableProperty]
    private string _themeIcon = "☀️";

    public ObservableCollection<UIChatMessage> Messages { get; } = [];
    public ObservableCollection<AgentEvent> Events { get; } = [];
    public ObservableCollection<ToolCall> ToolCalls { get; } = [];
    public ObservableCollection<TraceSpan> Traces { get; } = [];
    public ObservableCollection<WorkflowStep> WorkflowSteps { get; } = [];
    public ObservableCollection<string> Agents { get; } =
    [
        "writer",
        "editor",
        "⚡ Sequential: Story Pipeline",
        "⚡ Concurrent: Research Briefing",
        "⚡ Handoff: Customer Support",
        "⚡ Group Chat: Design Review"
    ];

    private readonly Dictionary<string, string> _agentInstructions = new()
    {
        ["writer"] = "You write short stories (300 words or less) about the specified topic.",
        ["editor"] = "You edit short stories to improve grammar and style, ensuring the stories are less than 300 words. Once finished editing, you select a title and format the story for publishing."
    };

    public MainViewModel(
        AIChatService aiService,
        SequentialOrchestration sequential,
        ConcurrentOrchestration concurrent,
        HandoffOrchestration handoff,
        GroupChatOrchestration groupChat)
    {
        _aiService = aiService;
        _sequential = sequential;
        _concurrent = concurrent;
        _handoff = handoff;
        _groupChat = groupChat;

        // Set initial theme icon
        ThemeIcon = Application.Current?.RequestedTheme == AppTheme.Dark ? "☀️" : "🌙";
    }

    [RelayCommand]
    private void SwitchTab(string tab)
    {
        IsEventsTab = tab == "events";
        IsTracesTab = tab == "traces";
        IsToolsTab = tab == "tools";
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        var app = Application.Current;
        if (app is null) return;

        app.UserAppTheme = app.UserAppTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
        ThemeIcon = app.UserAppTheme == AppTheme.Dark ? "☀️" : "🌙";
    }

    partial void OnSelectedAgentChanged(string value)
    {
        IsWorkflow = value.StartsWith("⚡");
        if (IsWorkflow)
        {
            InitializeWorkflowSteps(value);
        }
        else
        {
            WorkflowSteps.Clear();
            WorkflowTitle = "";
            OrchestrationKindLabel = "";
        }
    }

    private void InitializeWorkflowSteps(string workflow)
    {
        WorkflowSteps.Clear();
        var agents = GetOrchestrationAgents(workflow);
        var kind = GetOrchestrationKind(workflow);
        WorkflowTitle = workflow.Replace("⚡ ", "");
        OrchestrationKindLabel = kind.ToString();

        for (var i = 0; i < agents.Count; i++)
        {
            WorkflowSteps.Add(new WorkflowStep
            {
                Name = agents[i].Name,
                Status = "pending",
                IsFirst = i == 0
            });
        }
    }

    private IReadOnlyList<AgentDefinition> GetOrchestrationAgents(string workflow) => workflow switch
    {
        "⚡ Sequential: Story Pipeline" => _sequential.Agents,
        "⚡ Concurrent: Research Briefing" => _concurrent.Agents,
        "⚡ Handoff: Customer Support" => _handoff.Agents,
        "⚡ Group Chat: Design Review" => _groupChat.Agents,
        _ => []
    };

    private OrchestrationKind GetOrchestrationKind(string workflow) => workflow switch
    {
        "⚡ Sequential: Story Pipeline" => OrchestrationKind.Sequential,
        "⚡ Concurrent: Research Briefing" => OrchestrationKind.Concurrent,
        "⚡ Handoff: Customer Support" => OrchestrationKind.Handoff,
        "⚡ Group Chat: Design Review" => OrchestrationKind.GroupChat,
        _ => OrchestrationKind.Sequential
    };

    private Task RunOnUIAsync(Action action)
    {
        var tcs = new TaskCompletionSource();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try { action(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    [RelayCommand]
    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(MessageText) || IsSending)
            return;

        var userMessage = MessageText.Trim();
        MessageText = string.Empty;
        IsSending = true;
        _sessionStartTime ??= DateTime.UtcNow;

        var userMsg = new UIChatMessage
        {
            Role = "user",
            Content = userMessage,
            Timestamp = DateTime.Now
        };
        Messages.Add(userMsg);
        AddEvent("user.message", $"User: {userMessage}");

        try
        {
            if (IsWorkflow)
            {
                var kind = GetOrchestrationKind(SelectedAgent);
                await Task.Run(() => RunOrchestration(kind, userMessage));
            }
            else
            {
                await Task.Run(() => RunSingleAgent(SelectedAgent, userMessage));
            }
        }
        catch (Exception ex)
        {
            AddEvent("error", $"Error: {ex.Message}");
            await RunOnUIAsync(() => Messages.Add(new UIChatMessage
            {
                Role = "assistant",
                Content = $"❌ Error: {ex.Message}",
                Timestamp = DateTime.Now
            }));
        }
        finally
        {
            IsSending = false;
            UpdateElapsedTime();
        }
    }

    private void UpdateElapsedTime()
    {
        if (_sessionStartTime.HasValue)
        {
            var elapsed = DateTime.UtcNow - _sessionStartTime.Value;
            ElapsedTime = elapsed.TotalMinutes >= 1
                ? $"{elapsed.TotalMinutes:F1}m"
                : $"{elapsed.TotalSeconds:F1}s";
        }
    }

    private async Task RunSingleAgent(string agentName, string userMessage)
    {
        var instructions = _agentInstructions[agentName];

        AddEvent("response.created", "Response created");
        AddTrace("LLM", $"chat.completion ({agentName})");

        var chatMessages = new List<ChatMessage>
        {
            new(ChatRole.System, instructions),
            new(ChatRole.User, userMessage)
        };

        ChatOptions? options = null;
        if (agentName == "editor")
        {
            var formatTool = AIFunctionFactory.Create(FormatStory);
            options = new ChatOptions { Tools = [formatTool] };
        }

        var response = await StreamToUI(agentName, chatMessages, options);

        AddEvent("response.completed", $"Done ({response.Split(' ').Length * 2} tokens est.)");
    }

    private async Task RunOrchestration(OrchestrationKind kind, string userMessage)
    {
        AddEvent("workflow.started", $"{kind} orchestration started");
        AddTrace("Agent", $"Orchestration: {kind}");

        await RunOnUIAsync(() =>
        {
            foreach (var step in WorkflowSteps)
            {
                step.Status = "pending";
                step.StartTime = null;
                step.EndTime = null;
            }
        });

        switch (kind)
        {
            case OrchestrationKind.Sequential:
                await RunSequentialWorkflow(userMessage);
                break;
            case OrchestrationKind.Concurrent:
                await RunConcurrentWorkflow(userMessage);
                break;
            case OrchestrationKind.Handoff:
                await RunHandoffWorkflow(userMessage);
                break;
            case OrchestrationKind.GroupChat:
                await RunGroupChatWorkflow(userMessage);
                break;
        }

        AddEvent("workflow.completed", $"{kind} orchestration completed");
    }

    private async Task RunSequentialWorkflow(string userMessage)
    {
        var agents = _sequential.Agents;
        var currentInput = userMessage;

        for (var i = 0; i < agents.Count; i++)
        {
            var agent = agents[i];
            var step = WorkflowSteps[i];

            await SetStepRunning(step, agent.Name);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, agent.SystemPrompt),
                new(ChatRole.User, currentInput)
            };

            ChatOptions? options = null;
            if (agent.Name == "editor")
            {
                options = new ChatOptions { Tools = [.. _sequential.GetTools()] };
            }

            currentInput = await StreamToUI(agent.Name, messages, options);
            await SetStepCompleted(step);
        }
    }

    private async Task RunConcurrentWorkflow(string userMessage)
    {
        var agents = _concurrent.Agents;
        var analysts = agents.Where(a => a.Name != "synthesizer").ToList();
        var synthesizer = agents.First(a => a.Name == "synthesizer");

        await RunOnUIAsync(() =>
        {
            for (var i = 0; i < analysts.Count; i++)
            {
                WorkflowSteps[i].Status = "running";
                WorkflowSteps[i].StartTime = DateTime.Now;
            }
        });

        var tasks = new List<Task<(string name, string result)>>();
        for (var i = 0; i < analysts.Count; i++)
        {
            var agent = analysts[i];
            var step = WorkflowSteps[i];
            tasks.Add(RunConcurrentAgent(agent, step, userMessage));
        }

        var results = await Task.WhenAll(tasks);

        var synthStep = WorkflowSteps[^1];
        await SetStepRunning(synthStep, synthesizer.Name);

        var combinedInput = string.Join("\n\n", results.Select(r => $"--- {r.name} ---\n{r.result}"));
        var synthMessages = new List<ChatMessage>
        {
            new(ChatRole.System, synthesizer.SystemPrompt),
            new(ChatRole.User, combinedInput)
        };

        await StreamToUI(synthesizer.Name, synthMessages, null);
        await SetStepCompleted(synthStep);
    }

    private async Task<(string name, string result)> RunConcurrentAgent(AgentDefinition agent, WorkflowStep step, string input)
    {
        AddEvent("workflow_event.started", $"Parallel: {agent.Name}");
        AddTrace("LLM", $"chat.completion ({agent.Name})");

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, agent.SystemPrompt),
            new(ChatRole.User, input)
        };

        var result = await StreamToUI(agent.Name, messages, null);
        await SetStepCompleted(step);
        AddEvent("workflow_event.completed", $"Parallel: {agent.Name}");
        return (agent.Name, result);
    }

    private async Task RunHandoffWorkflow(string userMessage)
    {
        var agents = _handoff.Agents;
        var triageAgent = agents[0];
        var triageStep = WorkflowSteps[0];

        await SetStepRunning(triageStep, triageAgent.Name);

        var triageMessages = new List<ChatMessage>
        {
            new(ChatRole.System, triageAgent.SystemPrompt),
            new(ChatRole.User, userMessage)
        };

        var triageResult = await StreamToUI(triageAgent.Name, triageMessages, null);
        await SetStepCompleted(triageStep);

        var route = "technical";
        if (triageResult.Contains("ROUTE:billing", StringComparison.OrdinalIgnoreCase))
            route = "billing";
        else if (triageResult.Contains("ROUTE:technical", StringComparison.OrdinalIgnoreCase))
            route = "technical";
        else if (triageResult.Contains("ROUTE:account", StringComparison.OrdinalIgnoreCase))
            route = "account";

        AddEvent("handoff", $"Routing → {route}");

        var specialist = agents.First(a => a.Name == route);
        var specialistIdx = agents.ToList().IndexOf(specialist);
        var specialistStep = WorkflowSteps[specialistIdx];

        await SetStepRunning(specialistStep, specialist.Name);

        var specialistMessages = new List<ChatMessage>
        {
            new(ChatRole.System, specialist.SystemPrompt),
            new(ChatRole.User, $"Customer issue: {userMessage}\n\nTriage notes: {triageResult}")
        };

        await StreamToUI(specialist.Name, specialistMessages, null);
        await SetStepCompleted(specialistStep);

        await RunOnUIAsync(() =>
        {
            for (var i = 1; i < WorkflowSteps.Count; i++)
            {
                if (WorkflowSteps[i].Status == "pending")
                    WorkflowSteps[i].Status = "skipped";
            }
        });
    }

    private async Task RunGroupChatWorkflow(string userMessage)
    {
        var agents = _groupChat.Agents;
        var maxRounds = _groupChat.MaxRounds;
        var conversationHistory = new List<ChatMessage>();

        AddEvent("group_chat.started", $"Design review: {maxRounds} rounds, {agents.Count} participants");

        for (var round = 0; round < maxRounds; round++)
        {
            AddEvent("group_chat.round", $"Round {round + 1}/{maxRounds}");

            for (var i = 0; i < agents.Count; i++)
            {
                var agent = agents[i];
                var step = WorkflowSteps[i];

                await SetStepRunning(step, $"{agent.Name} (round {round + 1})");

                var messages = new List<ChatMessage>
                {
                    new(ChatRole.System, agent.SystemPrompt)
                };

                if (conversationHistory.Count == 0)
                {
                    messages.Add(new ChatMessage(ChatRole.User, $"Topic for discussion: {userMessage}"));
                }
                else
                {
                    messages.Add(new ChatMessage(ChatRole.User, $"Topic: {userMessage}\n\nDiscussion so far:\n" +
                        string.Join("\n", conversationHistory.Select(m => m.Text))));
                    messages.Add(new ChatMessage(ChatRole.User, "Please provide your contribution for this round."));
                }

                var response = await StreamToUI(agent.Name, messages, null);
                conversationHistory.Add(new ChatMessage(ChatRole.Assistant, $"{agent.Name}: {response}"));

                await RunOnUIAsync(() => step.Status = "completed");
            }
        }

        await RunOnUIAsync(() =>
        {
            foreach (var step in WorkflowSteps)
            {
                step.Status = "completed";
                step.EndTime ??= DateTime.Now;
            }
        });
    }

    private async Task<string> StreamToUI(string agentName, List<ChatMessage> messages, ChatOptions? options)
    {
        var isLabeled = IsWorkflow;
        var msg = new UIChatMessage
        {
            Role = "assistant",
            Content = isLabeled ? "" : "",
            AgentLabel = isLabeled ? agentName : null,
            Timestamp = DateTime.Now,
            IsStreaming = true
        };
        await RunOnUIAsync(() => Messages.Add(msg));

        var response = "";
        var lastUIUpdate = DateTime.UtcNow;
        var traceStart = DateTime.UtcNow;

        await foreach (var update in _aiService.ChatClient.GetStreamingResponseAsync(messages, options))
        {
            if (update.Text is { Length: > 0 } text)
            {
                response += text;

                if (DateTime.UtcNow - lastUIUpdate >= _uiBufferInterval)
                {
                    var snapshot = response + " ▍";
                    await RunOnUIAsync(() => msg.Content = snapshot);
                    lastUIUpdate = DateTime.UtcNow;
                }
            }

            if (update.Contents?.OfType<FunctionCallContent>().Any() == true)
            {
                foreach (var fc in update.Contents.OfType<FunctionCallContent>())
                {
                    var argsJson = FormatArguments(fc.Arguments);
                    AddEvent("function_call", $"⚡ {fc.Name}({argsJson})");
                    AddTrace("Tool", $"{fc.Name}");
                    var toolCall = new ToolCall
                    {
                        Name = fc.Name,
                        Arguments = argsJson,
                        Timestamp = DateTime.Now
                    };
                    await RunOnUIAsync(() => ToolCalls.Add(toolCall));
                }
            }
        }

        // Final flush — remove cursor
        var tokenEst = response.Split(' ').Length * 2;
        var traceDuration = (int)(DateTime.UtcNow - traceStart).TotalMilliseconds;
        await RunOnUIAsync(() =>
        {
            msg.Content = response;
            msg.IsStreaming = false;
            msg.TokenCount = tokenEst;
            TotalTokens += tokenEst;
        });

        // Update trace with duration
        AddTrace("LLM", $"completion ({agentName})", traceDuration);

        return response;
    }

    private async Task SetStepRunning(WorkflowStep step, string agentName)
    {
        AddEvent("workflow_event.started", $"▶ {agentName}");
        AddTrace("Agent", $"execute ({agentName})");
        await RunOnUIAsync(() =>
        {
            step.Status = "running";
            step.StartTime = DateTime.Now;
        });
    }

    private async Task SetStepCompleted(WorkflowStep step)
    {
        await RunOnUIAsync(() =>
        {
            step.Status = "completed";
            step.EndTime = DateTime.Now;
        });
    }

    private void AddEvent(string type, string description)
    {
        var evt = new AgentEvent
        {
            Timestamp = DateTime.Now,
            Type = type,
            Description = description
        };
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Events.Insert(0, evt);
            if (Events.Count > 200)
                Events.RemoveAt(Events.Count - 1);
            EventCount = Events.Count;
        });
    }

    private void AddTrace(string kind, string name, int? durationMs = null)
    {
        var trace = new TraceSpan
        {
            Name = name,
            OperationKind = kind,
            Duration = durationMs ?? 0,
            Timestamp = DateTime.Now
        };
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Traces.Insert(0, trace);
            if (Traces.Count > 200)
                Traces.RemoveAt(Traces.Count - 1);
            TraceCount = Traces.Count;
        });
    }

    private static string FormatArguments(IDictionary<string, object?>? args)
    {
        if (args is null || args.Count == 0)
            return "";

        try
        {
            return JsonSerializer.Serialize(args, new JsonSerializerOptions { WriteIndented = false });
        }
        catch
        {
            return string.Join(", ", args.Select(kv => $"{kv.Key}: {kv.Value}"));
        }
    }

    [RelayCommand]
    private void ClearConversation()
    {
        Messages.Clear();
        Events.Clear();
        ToolCalls.Clear();
        Traces.Clear();
        TotalTokens = 0;
        EventCount = 0;
        TraceCount = 0;
        ElapsedTime = "0.0s";
        _sessionStartTime = null;
        foreach (var step in WorkflowSteps)
        {
            step.Status = "pending";
            step.StartTime = null;
            step.EndTime = null;
        }
    }

    [Description("Formats the story for publication, revealing its title.")]
    private static string FormatStory(string title, string story) => $"""
        **Title**: {title}

        {story}
        """;
}
