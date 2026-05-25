using System.Collections.ObjectModel;

namespace Microsoft.Maui.AI.Agents.DevUI;

/// <summary>
/// UI construction for AgentDevUIView - builds the visual tree programmatically.
/// Layout: [Left Sidebar (agents/graph)] [Chat Panel] [Right Panel (events/traces/tools)]
/// </summary>
public partial class AgentDevUIView
{
    // Internal references for dynamic updates
    private CollectionView? _messagesView;
    private CollectionView? _eventsView;
    private CollectionView? _tracesView;
    private CollectionView? _toolsView;
    private Entry? _messageEntry;
    private Grid? _workflowGraphContainer;
    private VerticalStackLayout? _selectorList;
    private Label? _headerTitle;
    private Label? _workflowDescription;
    private Button? _sendButton;
    private Button? _demoButton;
    private Grid? _leftPanel;
    private readonly Dictionary<string, (Border border, Label statusIcon, Label statusLabel)> _graphNodeViews = [];

    // State
    private AgentInfo? _selectedAgent;
    private WorkflowInfo? _selectedWorkflow;
    private bool _isEventsTab = true;
    private bool _isTracesTab;
    private bool _isToolsTab;

    partial void BuildUI()
    {
        var root = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(new GridLength(240)),  // Left sidebar
                new(new GridLength(1)),    // Splitter
                new(GridLength.Star),      // Chat
                new(new GridLength(1)),    // Splitter
                new(new GridLength(360))   // Debug panel
            },
            RowDefinitions = new RowDefinitionCollection
            {
                new(new GridLength(48)),   // Header
                new(GridLength.Star)       // Content
            }
        };

        // Header
        var header = BuildHeader();
        root.Add(header, 0, 0);
        root.SetColumnSpan(header, 5);

        // Left sidebar
        _leftPanel = BuildLeftPanel();
        root.Add(_leftPanel, 0, 1);

        // Splitter
        root.Add(new BoxView { Color = GetThemedColor("#e8e0f0", "#2a2040") }, 1, 1);

        // Chat panel
        root.Add(BuildChatPanel(), 2, 1);

        // Splitter
        root.Add(new BoxView { Color = GetThemedColor("#e8e0f0", "#2a2040") }, 3, 1);

        // Debug panel
        root.Add(BuildDebugPanel(), 4, 1);

        Content = root;
    }

    private View BuildHeader()
    {
        var header = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = GetThemedColor("#faf8ff", "#1a1025"),
            Content = new Grid
            {
                Padding = new Thickness(16, 0),
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new(GridLength.Auto),
                    new(GridLength.Star),
                    new(GridLength.Auto),
                    new(GridLength.Auto)
                },
                Children =
                {
                    // Brand
                    BuildBrandSection(),
                    // Title (shows selected entity)
                    BuildHeaderTitle(),
                    // Theme toggle
                    BuildThemeToggle(),
                    // New chat
                    BuildNewChatButton()
                }
            }
        };

        return header;
    }

    private View BuildBrandSection()
    {
        var brand = new HorizontalStackLayout
        {
            Spacing = 10,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Border
                {
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
                    BackgroundColor = Color.FromArgb("#643FB2"),
                    Padding = new Thickness(6, 4),
                    StrokeThickness = 0,
                    Content = new Label
                    {
                        Text = "AG",
                        FontSize = 12,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Colors.White
                    }
                },
                new Label
                {
                    Text = "Agent Framework",
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = GetThemedColor("#1a1a2e", "#f0f0f0"),
                    VerticalOptions = LayoutOptions.Center
                },
                new Border
                {
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
                    Stroke = new SolidColorBrush(Color.FromArgb("#643FB2")),
                    BackgroundColor = Color.FromArgb("#643FB215"),
                    Padding = new Thickness(6, 2),
                    StrokeThickness = 1,
                    Content = new Label
                    {
                        Text = "Dev UI",
                        FontSize = 10,
                        TextColor = Color.FromArgb("#643FB2"),
                        FontAttributes = FontAttributes.Bold
                    }
                }
            }
        };
        return brand;
    }

    private View BuildHeaderTitle()
    {
        _headerTitle = new Label
        {
            Text = "Select an agent or workflow",
            FontSize = 13,
            TextColor = Colors.Gray,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(_headerTitle, 1);
        return _headerTitle;
    }

    private View BuildThemeToggle()
    {
        var btn = new Button
        {
            Text = "\u263D", // ☽ crescent moon
            BackgroundColor = Colors.Transparent,
            TextColor = GetThemedColor("#666", "#aaa"),
            FontSize = 18,
            WidthRequest = 40,
            HeightRequest = 40,
            VerticalOptions = LayoutOptions.Center
        };
        btn.Clicked += (s, e) =>
        {
            var app = Application.Current;
            if (app is null) return;
            app.UserAppTheme = app.UserAppTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
            btn.Text = app.UserAppTheme == AppTheme.Dark ? "\u2600" : "\u263D"; // ☀ / ☽
        };
        Grid.SetColumn(btn, 2);
        return btn;
    }

    private View BuildNewChatButton()
    {
        var btn = new Button
        {
            Text = "+ New Chat",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#643FB2"),
            FontSize = 12,
            VerticalOptions = LayoutOptions.Center,
            Padding = new Thickness(8, 4)
        };
        btn.Clicked += (s, e) =>
        {
            ClearConversation();
            if (_selectedWorkflow is not null)
                InitializeWorkflowNodes(_selectedWorkflow);
        };
        Grid.SetColumn(btn, 3);
        return btn;
    }

    private Grid BuildLeftPanel()
    {
        var panel = new Grid
        {
            RowDefinitions = new RowDefinitionCollection
            {
                new(new GridLength(1, GridUnitType.Star)),  // Selector (scrollable)
                new(new GridLength(1, GridUnitType.Star))   // Graph (when workflow selected)
            },
            BackgroundColor = GetThemedColor("#f8f7fc", "#12101e")
        };

        // Agent/Workflow selector
        _selectorList = new VerticalStackLayout { Spacing = 2, Padding = new Thickness(8) };
        var selectorScroll = new ScrollView { Content = _selectorList };
        panel.Add(selectorScroll, 0, 0);

        // Workflow graph area
        _workflowGraphContainer = new Grid
        {
            Padding = new Thickness(4),
            IsVisible = false
        };
        panel.Add(_workflowGraphContainer, 0, 1);

        return panel;
    }

    private Grid BuildChatPanel()
    {
        var panel = new Grid
        {
            RowDefinitions = new RowDefinitionCollection
            {
                new(GridLength.Auto),  // Workflow info / empty state
                new(GridLength.Star),  // Messages
                new(GridLength.Auto)   // Input
            },
            BackgroundColor = GetThemedColor("#ffffff", "#0f0f1a")
        };

        // Empty state / workflow description
        var emptyState = BuildEmptyState();
        panel.Add(emptyState, 0, 0);

        // Messages
        _messagesView = new CollectionView
        {
            ItemsSource = _messages,
            Margin = new Thickness(16, 8),
            ItemTemplate = BuildMessageTemplate()
        };
        panel.Add(_messagesView, 0, 1);

        // Input area
        panel.Add(BuildInputArea(), 0, 2);

        return panel;
    }

    private View BuildEmptyState()
    {
        var container = new VerticalStackLayout
        {
            Spacing = 12,
            Padding = new Thickness(16, 12),
            HorizontalOptions = LayoutOptions.Center
        };

        _workflowDescription = new Label
        {
            FontSize = 13,
            TextColor = Colors.Gray,
            HorizontalOptions = LayoutOptions.Center
        };
        container.Add(_workflowDescription);

        _demoButton = new Button
        {
            Text = "\u25B6 Try Demo",
            BackgroundColor = Color.FromArgb("#643FB2"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 12,
            Padding = new Thickness(16, 8),
            HorizontalOptions = LayoutOptions.Center,
            IsVisible = false
        };
        _demoButton.Clicked += OnDemoButtonClicked;
        container.Add(_demoButton);

        return container;
    }

    private View BuildInputArea()
    {
        var border = new Border
        {
            Margin = new Thickness(16, 8, 16, 16),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Stroke = new SolidColorBrush(GetThemedColor("#d0c8e0", "#3a3050")),
            BackgroundColor = GetThemedColor("#faf8ff", "#1a1025"),
            Padding = new Thickness(8, 0)
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(GridLength.Star),
                new(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        _messageEntry = new Entry
        {
            Placeholder = "Type a message...",
            FontSize = 14,
            VerticalOptions = LayoutOptions.Center
        };
        _messageEntry.Completed += OnMessageEntryCompleted;
        grid.Add(_messageEntry, 0, 0);

        _sendButton = new Button
        {
            Text = "\u27A4", // ➤
            BackgroundColor = Color.FromArgb("#643FB2"),
            TextColor = Colors.White,
            CornerRadius = 10,
            WidthRequest = 40,
            HeightRequest = 40,
            FontSize = 16,
            VerticalOptions = LayoutOptions.Center
        };
        _sendButton.Clicked += OnSendClicked;
        grid.Add(_sendButton, 1, 0);

        border.Content = grid;
        return border;
    }

    private Grid BuildDebugPanel()
    {
        var panel = new Grid
        {
            RowDefinitions = new RowDefinitionCollection
            {
                new(new GridLength(40)),  // Tab bar
                new(GridLength.Star)      // Tab content
            },
            BackgroundColor = GetThemedColor("#fafafa", "#12121e")
        };

        // Tab bar
        panel.Add(BuildTabBar(), 0, 0);

        // Events
        _eventsView = new CollectionView
        {
            ItemsSource = _events,
            Margin = new Thickness(8, 4),
            ItemTemplate = BuildEventTemplate()
        };
        panel.Add(_eventsView, 0, 1);

        // Traces
        _tracesView = new CollectionView
        {
            ItemsSource = _traces,
            Margin = new Thickness(8, 4),
            IsVisible = false,
            ItemTemplate = BuildTraceTemplate()
        };
        panel.Add(_tracesView, 0, 1);

        // Tools
        _toolsView = new CollectionView
        {
            ItemsSource = _toolCalls,
            Margin = new Thickness(8, 4),
            IsVisible = false,
            ItemTemplate = BuildToolTemplate()
        };
        panel.Add(_toolsView, 0, 1);

        return panel;
    }

    private View BuildTabBar()
    {
        var tabGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(GridLength.Star),
                new(GridLength.Star),
                new(GridLength.Star)
            },
            Padding = new Thickness(4, 0)
        };

        var eventsBtn = CreateTabButton("Events", true);
        eventsBtn.Clicked += (s, e) => SwitchTab("events");
        tabGrid.Add(eventsBtn, 0, 0);

        var tracesBtn = CreateTabButton("Traces", false);
        tracesBtn.Clicked += (s, e) => SwitchTab("traces");
        tabGrid.Add(tracesBtn, 1, 0);

        var toolsBtn = CreateTabButton("Tools", false);
        toolsBtn.Clicked += (s, e) => SwitchTab("tools");
        tabGrid.Add(toolsBtn, 2, 0);

        return tabGrid;
    }

    private static Button CreateTabButton(string text, bool active)
    {
        return new Button
        {
            Text = text,
            BackgroundColor = Colors.Transparent,
            FontSize = 11,
            Padding = new Thickness(0),
            TextColor = active ? Color.FromArgb("#643FB2") : Colors.Gray,
            FontAttributes = active ? FontAttributes.Bold : FontAttributes.None
        };
    }

    #region Data Templates

    private DataTemplate BuildMessageTemplate()
    {
        return new DataTemplate(() =>
        {
            var grid = new Grid
            {
                Padding = new Thickness(0, 4),
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new(GridLength.Auto),
                    new(GridLength.Star)
                },
                ColumnSpacing = 8
            };

            // Avatar for assistant
            var avatar = new Border
            {
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
                WidthRequest = 28,
                HeightRequest = 28,
                StrokeThickness = 1,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, 4, 0, 0),
                Stroke = new SolidColorBrush(GetThemedColor("#e0e0e0", "#333")),
                BackgroundColor = GetThemedColor("#f0eef5", "#2a2040"),
                Content = new Label
                {
                    Text = "AG",
                    FontSize = 9,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#643FB2"),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };
            avatar.SetBinding(IsVisibleProperty, new Binding("IsUser",
                converter: new InvertBooleanConverter()));
            grid.Add(avatar, 0, 0);

            // Message bubble
            var bubble = new Border
            {
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Padding = new Thickness(14, 10),
                MaximumWidthRequest = 650,
                Stroke = new SolidColorBrush(Colors.Transparent)
            };
            bubble.SetBinding(Border.BackgroundColorProperty, new Binding("Role",
                converter: new MessageBackgroundConverter()));
            bubble.SetBinding(View.HorizontalOptionsProperty, new Binding("Role",
                converter: new MessageAlignConverter()));

            var content = new VerticalStackLayout { Spacing = 4 };

            // Agent label
            var agentLabel = new Label
            {
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#643FB2")
            };
            agentLabel.SetBinding(Label.TextProperty, "AgentLabel");
            agentLabel.SetBinding(IsVisibleProperty, new Binding("AgentLabel",
                converter: new NotNullOrEmptyConverter()));
            content.Add(agentLabel);

            // Content text
            var contentLabel = new Label
            {
                FontSize = 14,
                TextColor = GetThemedColor("#1a1a2e", "#e8e8e8")
            };
            contentLabel.SetBinding(Label.TextProperty, new Binding("Content",
                converter: new MarkdownCleanConverter()));
            content.Add(contentLabel);

            // Timestamp row
            var timeRow = new HorizontalStackLayout { Spacing = 8 };
            var timeLabel = new Label { FontSize = 9, TextColor = Colors.Gray };
            timeLabel.SetBinding(Label.TextProperty, new Binding("Timestamp",
                stringFormat: "{0:h:mm:ss tt}"));
            timeRow.Add(timeLabel);

            var tokenLabel = new Label { FontSize = 9, TextColor = Colors.Gray };
            tokenLabel.SetBinding(Label.TextProperty, new Binding("TokenCount",
                stringFormat: "\u2022 {0} tokens"));
            tokenLabel.SetBinding(IsVisibleProperty, new Binding("TokenCount",
                converter: new NotNullOrEmptyConverter()));
            timeRow.Add(tokenLabel);

            // Streaming dot
            var streamDot = new Label
            {
                Text = "\u25CF", // ●
                FontSize = 8,
                TextColor = Color.FromArgb("#643FB2")
            };
            streamDot.SetBinding(IsVisibleProperty, "IsStreaming");
            timeRow.Add(streamDot);

            content.Add(timeRow);
            bubble.Content = content;
            grid.Add(bubble, 1, 0);

            return grid;
        });
    }

    private DataTemplate BuildEventTemplate()
    {
        return new DataTemplate(() =>
        {
            var border = new Border
            {
                Margin = new Thickness(2),
                Padding = new Thickness(8, 6),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
                Stroke = new SolidColorBrush(Colors.Transparent),
                BackgroundColor = GetThemedColor("#f8f8fc", "#1a1a2e")
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new(new GridLength(50)),
                    new(GridLength.Star)
                },
                ColumnSpacing = 8
            };

            var timeLabel = new Label
            {
                FontSize = 9,
                TextColor = Colors.Gray,
                VerticalOptions = LayoutOptions.Start,
                FontFamily = "Courier New"
            };
            timeLabel.SetBinding(Label.TextProperty, new Binding("Timestamp",
                stringFormat: "{0:mm:ss.f}"));
            grid.Add(timeLabel, 0, 0);

            var stack = new VerticalStackLayout { Spacing = 2 };

            var typeLabel = new Label
            {
                FontSize = 11,
                FontAttributes = FontAttributes.Bold
            };
            typeLabel.SetBinding(Label.TextProperty, "Type");
            typeLabel.SetBinding(Label.TextColorProperty, new Binding("Type",
                converter: new EventTypeColorConverter()));
            stack.Add(typeLabel);

            var descLabel = new Label
            {
                FontSize = 10,
                TextColor = GetThemedColor("#555", "#999"),
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 2
            };
            descLabel.SetBinding(Label.TextProperty, "Description");
            stack.Add(descLabel);

            grid.Add(stack, 1, 0);
            border.Content = grid;
            return border;
        });
    }

    private DataTemplate BuildTraceTemplate()
    {
        return new DataTemplate(() =>
        {
            var border = new Border
            {
                Margin = new Thickness(2),
                Padding = new Thickness(8, 6),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
                Stroke = new SolidColorBrush(Colors.Transparent),
                BackgroundColor = GetThemedColor("#f8f8fc", "#1a1a2e")
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new(GridLength.Auto),
                    new(GridLength.Star),
                    new(GridLength.Auto)
                },
                ColumnSpacing = 8
            };

            // Badge
            var badge = new Border
            {
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 3 },
                Padding = new Thickness(4, 2),
                StrokeThickness = 0
            };
            badge.SetBinding(Border.BackgroundColorProperty, "OperationColor");
            var badgeLabel = new Label { FontSize = 8, TextColor = Colors.White };
            badgeLabel.SetBinding(Label.TextProperty, "OperationKind");
            badge.Content = badgeLabel;
            grid.Add(badge, 0, 0);

            // Name
            var nameLabel = new Label
            {
                FontSize = 11,
                TextColor = GetThemedColor("#333", "#ccc"),
                LineBreakMode = LineBreakMode.TailTruncation,
                VerticalOptions = LayoutOptions.Center
            };
            nameLabel.SetBinding(Label.TextProperty, "Name");
            grid.Add(nameLabel, 1, 0);

            // Duration
            var durLabel = new Label
            {
                FontSize = 9,
                TextColor = Colors.Gray,
                VerticalOptions = LayoutOptions.Center
            };
            durLabel.SetBinding(Label.TextProperty, new Binding("Duration",
                stringFormat: "{0}ms"));
            grid.Add(durLabel, 2, 0);

            border.Content = grid;
            return border;
        });
    }

    private DataTemplate BuildToolTemplate()
    {
        return new DataTemplate(() =>
        {
            var border = new Border
            {
                Margin = new Thickness(2),
                Padding = new Thickness(8, 6),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
                Stroke = new SolidColorBrush(Colors.Transparent),
                BackgroundColor = GetThemedColor("#f8f8fc", "#1a1a2e")
            };

            var stack = new VerticalStackLayout { Spacing = 4 };

            var header = new HorizontalStackLayout { Spacing = 8 };
            var icon = new Label
            {
                Text = "\u26A1", // ⚡
                FontSize = 12,
                TextColor = Color.FromArgb("#3B82F6")
            };
            header.Add(icon);

            var nameLabel = new Label
            {
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = GetThemedColor("#1a1a2e", "#f0f0f0")
            };
            nameLabel.SetBinding(Label.TextProperty, "Name");
            header.Add(nameLabel);

            var timeLabel = new Label
            {
                FontSize = 9,
                TextColor = Colors.Gray,
                VerticalOptions = LayoutOptions.Center
            };
            timeLabel.SetBinding(Label.TextProperty, new Binding("Timestamp",
                stringFormat: "{0:h:mm:ss tt}"));
            header.Add(timeLabel);

            stack.Add(header);

            var argsLabel = new Label
            {
                FontSize = 10,
                TextColor = GetThemedColor("#555", "#aaa"),
                FontFamily = "Courier New",
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 3
            };
            argsLabel.SetBinding(Label.TextProperty, "Arguments");
            stack.Add(argsLabel);

            border.Content = stack;
            return border;
        });
    }

    #endregion

    #region Event Handlers

    private async void OnSendClicked(object? sender, EventArgs e) => await SendCurrentMessage();
    private async void OnMessageEntryCompleted(object? sender, EventArgs e) => await SendCurrentMessage();

    private async Task SendCurrentMessage()
    {
        var text = _messageEntry?.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        if (_messageEntry is not null)
            _messageEntry.Text = string.Empty;

        if (_selectedWorkflow is not null)
            await RunWorkflowAsync(text, _selectedWorkflow);
        else if (_selectedAgent is not null)
            await SendMessageAsync(text, _selectedAgent);
    }

    private async void OnDemoButtonClicked(object? sender, EventArgs e)
    {
        var prompt = _selectedWorkflow?.DemoPrompt ?? _selectedAgent?.Description;
        if (string.IsNullOrEmpty(prompt)) return;

        if (_messageEntry is not null)
            _messageEntry.Text = string.Empty;

        if (_selectedWorkflow is not null)
            await RunWorkflowAsync(prompt, _selectedWorkflow);
        else if (_selectedAgent is not null)
            await SendMessageAsync(prompt, _selectedAgent);
    }

    private void SwitchTab(string tab)
    {
        _isEventsTab = tab == "events";
        _isTracesTab = tab == "traces";
        _isToolsTab = tab == "tools";

        if (_eventsView is not null) _eventsView.IsVisible = _isEventsTab;
        if (_tracesView is not null) _tracesView.IsVisible = _isTracesTab;
        if (_toolsView is not null) _toolsView.IsVisible = _isToolsTab;
    }

    #endregion

    #region Entity Selection

    partial void OnEntitiesDiscovered(IReadOnlyList<AgentInfo> agents, IReadOnlyList<WorkflowInfo> workflows)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_selectorList is null) return;
            _selectorList.Children.Clear();

            // Agents section
            if (agents.Count > 0)
            {
                _selectorList.Add(new Label
                {
                    Text = "AGENTS",
                    FontSize = 10,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.Gray,
                    Margin = new Thickness(8, 8, 0, 4)
                });

                foreach (var agent in agents)
                {
                    _selectorList.Add(CreateEntityButton(agent.Name, agent.Description, () =>
                    {
                        SelectAgent(agent);
                    }));
                }
            }

            // Workflows section
            if (workflows.Count > 0)
            {
                _selectorList.Add(new Label
                {
                    Text = "WORKFLOWS",
                    FontSize = 10,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.Gray,
                    Margin = new Thickness(8, 16, 0, 4)
                });

                foreach (var workflow in workflows)
                {
                    var kindIcon = workflow.Kind switch
                    {
                        OrchestrationKind.Sequential => "\u2192", // →
                        OrchestrationKind.Concurrent => "\u2261", // ≡
                        OrchestrationKind.Handoff => "\u21C4",    // ⇄
                        OrchestrationKind.GroupChat => "\u25CB",   // ○
                        _ => "\u2022"                              // •
                    };
                    _selectorList.Add(CreateEntityButton(
                        $"{kindIcon} {workflow.Name}",
                        workflow.Description,
                        () => SelectWorkflow(workflow)));
                }
            }
        });
    }

    private View CreateEntityButton(string name, string? description, Action onTap)
    {
        var border = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Stroke = new SolidColorBrush(Colors.Transparent),
            BackgroundColor = Colors.Transparent,
            Padding = new Thickness(10, 8),
            Margin = new Thickness(0, 1)
        };

        var stack = new VerticalStackLayout { Spacing = 2 };
        stack.Add(new Label
        {
            Text = name,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = GetThemedColor("#1a1a2e", "#f0f0f0")
        });
        if (!string.IsNullOrEmpty(description))
        {
            stack.Add(new Label
            {
                Text = description,
                FontSize = 10,
                TextColor = Colors.Gray,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 2
            });
        }

        border.Content = stack;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (s, e) =>
        {
            border.BackgroundColor = Color.FromArgb("#643FB220");
            onTap();
        };
        border.GestureRecognizers.Add(tap);

        return border;
    }

    private void SelectAgent(AgentInfo agent)
    {
        _selectedAgent = agent;
        _selectedWorkflow = null;
        ClearConversation();

        if (_headerTitle is not null)
            _headerTitle.Text = agent.Name;
        if (_workflowDescription is not null)
            _workflowDescription.Text = agent.Description ?? $"Chat with {agent.Name}";
        if (_demoButton is not null)
            _demoButton.IsVisible = !string.IsNullOrEmpty(agent.Description);
        if (_workflowGraphContainer is not null)
            _workflowGraphContainer.IsVisible = false;
        if (_messageEntry is not null)
            _messageEntry.Placeholder = $"Message {agent.Name}...";

        // Hide graph in left panel
        UpdateLeftPanelLayout(showGraph: false);
    }

    private void SelectWorkflow(WorkflowInfo workflow)
    {
        _selectedAgent = null;
        _selectedWorkflow = workflow;
        ClearConversation();

        if (_headerTitle is not null)
            _headerTitle.Text = workflow.Name;
        if (_workflowDescription is not null)
            _workflowDescription.Text = workflow.Description ?? $"{workflow.Kind} workflow";
        if (_demoButton is not null)
            _demoButton.IsVisible = !string.IsNullOrEmpty(workflow.DemoPrompt);
        if (_messageEntry is not null)
            _messageEntry.Placeholder = $"Message {workflow.Name}...";

        // Show graph
        BuildWorkflowGraph(workflow);
        UpdateLeftPanelLayout(showGraph: true);
    }

    private void UpdateLeftPanelLayout(bool showGraph)
    {
        if (_workflowGraphContainer is not null)
            _workflowGraphContainer.IsVisible = showGraph;
    }

    private void BuildWorkflowGraph(WorkflowInfo workflow)
    {
        if (_workflowGraphContainer is null) return;

        _workflowGraphContainer.Children.Clear();
        InitializeWorkflowNodes(workflow);

        var layout = GraphLayoutEngine.ComputeLayout(workflow);

        // Create an AbsoluteLayout for positioned nodes
        var canvas = new AbsoluteLayout
        {
            WidthRequest = layout.Width + 20,
            HeightRequest = layout.Height + 20
        };

        // Add edge connectors (arrows between nodes)
        foreach (var edge in layout.Edges)
        {
            var source = layout.Nodes.First(n => n.Id == edge.SourceId);
            var target = layout.Nodes.First(n => n.Id == edge.TargetId);

            var midX = (source.X + target.X) / 2 + 70;
            var midY = (source.Y + target.Y) / 2 + 24;

            var isVertical = Math.Abs(target.Y - source.Y) > Math.Abs(target.X - source.X);
            var connector = new Label
            {
                Text = edge.IsBidirectional ? "\u21C4" : (isVertical ? "\u2193" : "\u2192"), // ⇄, ↓, or →
                FontSize = 14,
                TextColor = Color.FromArgb("#9CA3AF"),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
            AbsoluteLayout.SetLayoutBounds(connector, new Rect(midX, midY, 20, 20));
            canvas.Add(connector);
        }

        // Add nodes
        for (var i = 0; i < layout.Nodes.Count && i < _workflowNodes.Count; i++)
        {
            var layoutNode = layout.Nodes[i];
            var dataNode = _workflowNodes[i];
            var nodeView = BuildGraphNode(dataNode);
            AbsoluteLayout.SetLayoutBounds(nodeView, new Rect(layoutNode.X, layoutNode.Y, 160, 56));
            canvas.Add(nodeView);
        }

        var scroll = new ScrollView
        {
            Orientation = ScrollOrientation.Both,
            Content = canvas
        };

        _workflowGraphContainer.Children.Clear();
        _workflowGraphContainer.Add(scroll);
        _workflowGraphContainer.IsVisible = true;
    }

    private View BuildGraphNode(WorkflowNode node)
    {
        var border = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            StrokeThickness = 2,
            Padding = new Thickness(10, 6),
            Stroke = new SolidColorBrush(Color.FromArgb("#6B7280")),
            BackgroundColor = Color.FromArgb(
                Application.Current?.RequestedTheme == AppTheme.Dark ? "#1a1a2e" : "#fafafa")
        };

        var stack = new VerticalStackLayout { Spacing = 3 };

        var nameLabel = new Label
        {
            Text = node.Name,
            FontAttributes = FontAttributes.Bold,
            FontSize = 11,
            TextColor = GetThemedColor("#1a1a2e", "#f0f0f0")
        };
        stack.Add(nameLabel);

        var statusRow = new HorizontalStackLayout { Spacing = 4 };
        var statusIcon = new Label { FontSize = 10, Text = node.StatusIcon };
        statusRow.Add(statusIcon);

        var statusLabel = new Label { FontSize = 10, Text = node.StatusLabel, TextColor = Color.FromArgb("#6B7280") };
        statusRow.Add(statusLabel);

        stack.Add(statusRow);
        border.Content = stack;

        // Track for imperative updates
        _graphNodeViews[node.Id] = (border, statusIcon, statusLabel);

        // Subscribe to status changes
        node.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is "Status" or "StatusIcon" or "StatusLabel")
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    statusIcon.Text = node.StatusIcon;
                    statusLabel.Text = node.StatusLabel;
                    var color = node.Status switch
                    {
                        "running" => Color.FromArgb("#643FB2"),
                        "completed" => Color.FromArgb("#10B981"),
                        "failed" => Color.FromArgb("#EF4444"),
                        _ => Color.FromArgb("#6B7280")
                    };
                    border.Stroke = new SolidColorBrush(color);
                    statusLabel.TextColor = color;
                    var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
                    border.BackgroundColor = node.Status switch
                    {
                        "running" => Color.FromArgb(isDark ? "#2a1f4e" : "#f3f0ff"),
                        "completed" => Color.FromArgb(isDark ? "#1a2e25" : "#f0fdf4"),
                        "failed" => Color.FromArgb(isDark ? "#2e1a1a" : "#fef2f2"),
                        _ => Color.FromArgb(isDark ? "#1a1a2e" : "#fafafa")
                    };
                });
            }
        };

        return border;
    }

    #endregion

    #region Theming Helpers

    private static Color GetThemedColor(string lightHex, string darkHex)
    {
        return Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb(darkHex)
            : Color.FromArgb(lightHex);
    }

    #endregion
}
