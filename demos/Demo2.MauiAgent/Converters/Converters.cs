using System.Globalization;

namespace Demo2.MauiAgent.Converters;

public class StepBorderConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "running" => Color.FromArgb("#643FB2"),  // DevUI purple
            "completed" => Color.FromArgb("#10B981"), // Green
            "failed" => Color.FromArgb("#EF4444"),    // Red
            "skipped" => Color.FromArgb("#F97316"),   // Orange
            _ => Color.FromArgb("#6B7280")           // Gray
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StepBgConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "running" => Color.FromArgb("#643FB220"),   // Purple tint
            "completed" => Color.FromArgb("#10B98120"), // Green tint
            "failed" => Color.FromArgb("#EF444420"),    // Red tint
            "skipped" => Color.FromArgb("#F9731620"),   // Orange tint
            _ => Color.FromArgb("#F5F5F5")
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class MessageBgConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "user" => Color.FromArgb("#E3F2FD"),
            _ => Color.FromArgb("#F5F5F5")
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class MessageAlignConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString() == "user" ? LayoutOptions.End : LayoutOptions.Start;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class NotNullConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null and not 0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class InvertBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : true;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : true;
}

public class EventTypeColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var type = value?.ToString() ?? "";
        return type switch
        {
            "response.created" => Color.FromArgb("#3B82F6"),      // Blue
            "response.in_progress" => Color.FromArgb("#F59E0B"),  // Amber
            "response.completed" => Color.FromArgb("#10B981"),    // Green
            "function_call" => Color.FromArgb("#3B82F6"),         // Blue (DevUI)
            "workflow.started" => Color.FromArgb("#643FB2"),      // Purple (DevUI)
            "workflow.completed" => Color.FromArgb("#643FB2"),    // Purple
            "workflow_event.started" => Color.FromArgb("#643FB2"),// Purple
            "workflow_event.completed" => Color.FromArgb("#10B981"),// Green
            "group_chat.started" => Color.FromArgb("#643FB2"),
            "group_chat.round" => Color.FromArgb("#8B5CF6"),
            "handoff" => Color.FromArgb("#F59E0B"),              // Amber
            "user.message" => Color.FromArgb("#6B7280"),         // Gray
            "error" => Color.FromArgb("#EF4444"),                // Red
            _ => Color.FromArgb("#6B7280")                       // Gray
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
