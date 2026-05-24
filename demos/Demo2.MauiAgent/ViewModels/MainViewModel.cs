using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Demo2.MauiAgent.Models;
using Demo2.MauiAgent.Services;
using Microsoft.Extensions.AI;

namespace Demo2.MauiAgent.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly AIChatService _aiService;
    private readonly List<ChatMessage> _conversationHistory = [];

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

    public ObservableCollection<UIChatMessage> Messages { get; } = [];
    public ObservableCollection<AgentEvent> Events { get; } = [];
    public ObservableCollection<ToolCall> ToolCalls { get; } = [];
    public ObservableCollection<WorkflowStep> WorkflowSteps { get; } = [];
    public ObservableCollection<string> Agents { get; } = ["writer", "editor", "publisher"];

    private readonly Dictionary<string, string> _agentInstructions = new()
    {
        ["writer"] = "You write short stories (300 words or less) about the specified topic.",
        ["editor"] = "You edit short stories to improve grammar and style, ensuring the stories are less than 300 words. Once finished editing, you select a title and format the story for publishing.",
        ["publisher"] = "publisher-workflow"
    };

    public MainViewModel(AIChatService aiService)
    {
        _aiService = aiService;
        InitializeWorkflowSteps();
    }

    private void InitializeWorkflowSteps()
    {
        WorkflowSteps.Add(new WorkflowStep { Name = "writer", Status = "pending" });
        WorkflowSteps.Add(new WorkflowStep { Name = "editor", Status = "pending" });
        WorkflowSteps.Add(new WorkflowStep { Name = "output", Status = "pending" });
    }

    [RelayCommand]
    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(MessageText) || IsSending)
            return;

        var userMessage = MessageText.Trim();
        MessageText = string.Empty;
        IsSending = true;

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
            if (SelectedAgent == "publisher")
            {
                await RunWorkflow(userMessage);
            }
            else
            {
                await RunSingleAgent(SelectedAgent, userMessage);
            }
        }
        catch (Exception ex)
        {
            AddEvent("error", $"Error: {ex.Message}");
            Messages.Add(new UIChatMessage
            {
                Role = "assistant",
                Content = $"Error: {ex.Message}",
                Timestamp = DateTime.Now
            });
        }
        finally
        {
            IsSending = false;
        }
    }

    private async Task RunSingleAgent(string agentName, string userMessage)
    {
        var instructions = _agentInstructions[agentName];

        AddEvent("response.created", "Response created");
        AddEvent("response.in_progress", "Processing...");

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

        var assistantMsg = new UIChatMessage
        {
            Role = "assistant",
            Content = "",
            Timestamp = DateTime.Now,
            IsStreaming = true
        };
        Messages.Add(assistantMsg);

        var fullResponse = "";
        await foreach (var update in _aiService.ChatClient.GetStreamingResponseAsync(chatMessages, options))
        {
            if (update.Text is { Length: > 0 } text)
            {
                fullResponse += text;
                assistantMsg.Content = fullResponse;
                OnPropertyChanged(nameof(Messages));
                AddEvent("output_text.delta", text.Length > 60 ? text[..60] + "..." : text);
            }

            if (update.Contents?.OfType<FunctionCallContent>().Any() == true)
            {
                foreach (var fc in update.Contents.OfType<FunctionCallContent>())
                {
                    AddEvent("function_call", $"Tool call: {fc.Name}");
                    ToolCalls.Add(new ToolCall
                    {
                        Name = fc.Name,
                        Arguments = fc.Arguments?.ToString() ?? "",
                        Timestamp = DateTime.Now
                    });
                }
            }
        }

        assistantMsg.IsStreaming = false;
        assistantMsg.TokenCount = fullResponse.Split(' ').Length * 2;
        TotalTokens += assistantMsg.TokenCount ?? 0;
        AddEvent("response.completed", $"Response complete ({assistantMsg.TokenCount} tokens est.)");
    }

    private async Task RunWorkflow(string userMessage)
    {
        foreach (var step in WorkflowSteps)
        {
            step.Status = "pending";
            step.StartTime = null;
            step.EndTime = null;
        }
        OnPropertyChanged(nameof(WorkflowSteps));

        AddEvent("workflow.started", "Publisher workflow started");

        // Step 1: Writer
        var writerStep = WorkflowSteps[0];
        writerStep.Status = "running";
        writerStep.StartTime = DateTime.Now;
        OnPropertyChanged(nameof(WorkflowSteps));
        AddEvent("workflow_event.started", "Executor: writer");

        var writerMessages = new List<ChatMessage>
        {
            new(ChatRole.System, _agentInstructions["writer"]),
            new(ChatRole.User, userMessage)
        };

        var writerMsg = new UIChatMessage
        {
            Role = "assistant",
            Content = "[writer] ",
            Timestamp = DateTime.Now,
            IsStreaming = true
        };
        Messages.Add(writerMsg);

        var writerResponse = "";
        await foreach (var update in _aiService.ChatClient.GetStreamingResponseAsync(writerMessages))
        {
            if (update.Text is { Length: > 0 } text)
            {
                writerResponse += text;
                writerMsg.Content = "[writer] " + writerResponse;
                OnPropertyChanged(nameof(Messages));
                AddEvent("output_text.delta", text.Length > 60 ? text[..60] + "..." : text);
            }
        }
        writerMsg.IsStreaming = false;
        writerStep.Status = "completed";
        writerStep.EndTime = DateTime.Now;
        OnPropertyChanged(nameof(WorkflowSteps));
        AddEvent("workflow_event.completed", "Executor: writer");

        // Step 2: Editor
        var editorStep = WorkflowSteps[1];
        editorStep.Status = "running";
        editorStep.StartTime = DateTime.Now;
        OnPropertyChanged(nameof(WorkflowSteps));
        AddEvent("workflow_event.started", "Executor: editor");

        var formatTool = AIFunctionFactory.Create(FormatStory);
        var editorMessages = new List<ChatMessage>
        {
            new(ChatRole.System, _agentInstructions["editor"]),
            new(ChatRole.User, writerResponse)
        };
        var editorOptions = new ChatOptions { Tools = [formatTool] };

        var editorMsg = new UIChatMessage
        {
            Role = "assistant",
            Content = "[editor] ",
            Timestamp = DateTime.Now,
            IsStreaming = true
        };
        Messages.Add(editorMsg);

        var editorResponse = "";
        var toolCalled = false;
        await foreach (var update in _aiService.ChatClient.GetStreamingResponseAsync(editorMessages, editorOptions))
        {
            if (update.Text is { Length: > 0 } text)
            {
                editorResponse += text;
                editorMsg.Content = "[editor] " + editorResponse;
                OnPropertyChanged(nameof(Messages));
                AddEvent("output_text.delta", text.Length > 60 ? text[..60] + "..." : text);
            }

            if (!toolCalled && update.Contents?.OfType<FunctionCallContent>().Any() == true)
            {
                foreach (var fc in update.Contents.OfType<FunctionCallContent>())
                {
                    toolCalled = true;
                    AddEvent("function_call", $"Calling {fc.Name}({fc.Arguments})");
                    ToolCalls.Add(new ToolCall
                    {
                        Name = fc.Name,
                        Arguments = fc.Arguments?.ToString() ?? "",
                        Timestamp = DateTime.Now
                    });
                }
            }
        }
        editorMsg.IsStreaming = false;
        editorStep.Status = "completed";
        editorStep.EndTime = DateTime.Now;
        OnPropertyChanged(nameof(WorkflowSteps));
        AddEvent("workflow_event.completed", "Executor: editor");

        // Step 3: Output
        var outputStep = WorkflowSteps[2];
        outputStep.Status = "running";
        outputStep.StartTime = DateTime.Now;
        OnPropertyChanged(nameof(WorkflowSteps));

        var finalContent = string.IsNullOrEmpty(editorResponse) ? writerResponse : editorResponse;
        var outputMsg = new UIChatMessage
        {
            Role = "assistant",
            Content = "[output] " + finalContent,
            Timestamp = DateTime.Now,
            TokenCount = finalContent.Split(' ').Length * 2
        };
        Messages.Add(outputMsg);
        TotalTokens += outputMsg.TokenCount ?? 0;

        outputStep.Status = "completed";
        outputStep.EndTime = DateTime.Now;
        OnPropertyChanged(nameof(WorkflowSteps));
        AddEvent("workflow.completed", "Publisher workflow completed");
    }

    private void AddEvent(string type, string description)
    {
        var evt = new AgentEvent
        {
            Timestamp = DateTime.Now,
            Type = type,
            Description = description
        };
        Events.Insert(0, evt);
        EventCount = Events.Count;
    }

    [RelayCommand]
    private void ClearConversation()
    {
        Messages.Clear();
        Events.Clear();
        ToolCalls.Clear();
        TotalTokens = 0;
        EventCount = 0;
        foreach (var step in WorkflowSteps)
        {
            step.Status = "pending";
            step.StartTime = null;
            step.EndTime = null;
        }
        OnPropertyChanged(nameof(WorkflowSteps));
    }

    [System.ComponentModel.Description("Formats the story for publication, revealing its title.")]
    private static string FormatStory(string title, string story) => $"""
        **Title**: {title}

        {story}
        """;
}
