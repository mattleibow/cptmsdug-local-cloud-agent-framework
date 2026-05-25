using Microsoft.Maui.Layouts;

namespace Microsoft.Maui.AI.Agents.DevUI.Layout;

/// <summary>
/// A custom layout that positions WorkflowNodeView children according to the
/// orchestration topology. A GraphicsView child (first child) draws edges.
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
