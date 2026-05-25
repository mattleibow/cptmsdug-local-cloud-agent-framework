using Microsoft.Maui.Layouts;

namespace Microsoft.Maui.AI.Agents.DevUI.Layout;

/// <summary>
/// A custom layout that positions WorkflowNodeView children and edge Path shapes
/// according to the orchestration topology computed by MSAGL.
/// Children are: edge Paths first, then node views.
/// </summary>
public class WorkflowGraphLayout : Microsoft.Maui.Controls.Layout
{
    public static readonly BindableProperty WorkflowProperty =
        BindableProperty.Create(nameof(Workflow), typeof(WorkflowInfo), typeof(WorkflowGraphLayout),
            propertyChanged: OnWorkflowChanged);

    public static readonly BindableProperty NodesProperty =
        BindableProperty.Create(nameof(Nodes), typeof(IReadOnlyList<WorkflowNode>), typeof(WorkflowGraphLayout));

    public WorkflowInfo? Workflow
    {
        get => (WorkflowInfo?)GetValue(WorkflowProperty);
        set => SetValue(WorkflowProperty, value);
    }

    public IReadOnlyList<WorkflowNode>? Nodes
    {
        get => (IReadOnlyList<WorkflowNode>?)GetValue(NodesProperty);
        set => SetValue(NodesProperty, value);
    }

    internal GraphLayout? ComputedLayout { get; private set; }

    /// <summary>Number of edge Path views at the start of Children.</summary>
    internal int EdgeCount { get; set; }

    protected override ILayoutManager CreateLayoutManager()
        => new WorkflowGraphLayoutManager(this);

    private static void OnWorkflowChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is WorkflowGraphLayout layout)
            layout.InvalidateMeasure();
    }

    internal void RecomputeLayout()
    {
        if (Workflow is null)
        {
            ComputedLayout = null;
            return;
        }
        ComputedLayout = GraphLayoutEngine.ComputeLayout(Workflow);
    }
}
