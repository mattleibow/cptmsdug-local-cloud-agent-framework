using System.Globalization;

namespace Microsoft.Maui.AI.Agents.DevUI;

internal sealed class NodeBorderColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var color = value?.ToString() switch
        {
            "running" => Color.FromArgb("#643FB2"),
            "completed" => Color.FromArgb("#10B981"),
            "failed" => Color.FromArgb("#EF4444"),
            "skipped" => Color.FromArgb("#9CA3AF"),
            "cancelled" => Color.FromArgb("#F97316"),
            _ => Color.FromArgb("#6B7280")
        };
        // Return Brush for Border.Stroke, Color for TextColor
        if (targetType == typeof(Brush))
            return new SolidColorBrush(color);
        return color;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

internal sealed class NodeBackgroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        return value?.ToString() switch
        {
            "running" => Color.FromArgb(isDark ? "#2a1f4e" : "#f3f0ff"),
            "completed" => Color.FromArgb(isDark ? "#1a2e25" : "#f0fdf4"),
            "failed" => Color.FromArgb(isDark ? "#2e1a1a" : "#fef2f2"),
            "skipped" => Color.FromArgb(isDark ? "#1f1f1f" : "#f9fafb"),
            _ => Color.FromArgb(isDark ? "#1a1a2e" : "#fafafa")
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

internal sealed class MessageBackgroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var isUser = value is true or "True" or "user";
        if (isUser)
            return Color.FromArgb(isDark ? "#3b2e6b" : "#e8e0f5");
        return Color.FromArgb(isDark ? "#1e1e30" : "#f5f5fa");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

internal sealed class MessageAlignConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isUser = value is true or "True" or "user";
        return isUser ? LayoutOptions.End : LayoutOptions.Start;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

internal sealed class IsAssistantRoleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() != "user";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

internal sealed class NotNullOrEmptyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s)
            return !string.IsNullOrEmpty(s);
        return value is not null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

internal sealed class InvertBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}

internal sealed class EventTypeColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "function_call" => Color.FromArgb("#3B82F6"),
            "handoff" => Color.FromArgb("#F97316"),
            "error" => Color.FromArgb("#EF4444"),
            "group_chat.round" => Color.FromArgb("#8B5CF6"),
            "workflow.started" or "workflow.completed" => Color.FromArgb("#643FB2"),
            _ when value?.ToString()?.Contains("started") == true => Color.FromArgb("#10B981"),
            _ when value?.ToString()?.Contains("completed") == true => Color.FromArgb("#059669"),
            _ => Color.FromArgb("#6B7280")
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

internal sealed class RunningOpacityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? 0.6f : 0f;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

internal sealed class MarkdownCleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string text || string.IsNullOrEmpty(text))
            return value;
        // Strip markdown bold, italic, and heading markers
        return text.Replace("**", "").Replace("__", "").Replace("# ", "");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

internal sealed class TabActiveColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true)
            return Color.FromArgb("#643FB2");
        return Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#888888")
            : Color.FromArgb("#999999");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

internal sealed class BoolToFontAttributeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? FontAttributes.Bold : FontAttributes.None;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
