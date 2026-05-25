using System.Globalization;

namespace Demo2.MauiAgent.Converters;

public class StepBorderConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "running" => Color.FromArgb("#643FB2"),
            "completed" => Color.FromArgb("#10B981"),
            "failed" => Color.FromArgb("#EF4444"),
            "skipped" => Color.FromArgb("#F97316"),
            _ => Color.FromArgb("#6B7280")
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
            "running" => Color.FromArgb("#643FB220"),
            "completed" => Color.FromArgb("#10B98120"),
            "failed" => Color.FromArgb("#EF444420"),
            "skipped" => Color.FromArgb("#F9731620"),
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
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        return value?.ToString() switch
        {
            "user" => isDark ? Color.FromArgb("#2a1f45") : Color.FromArgb("#ede6ff"),
            _ => isDark ? Color.FromArgb("#1a1a2e") : Color.FromArgb("#f5f5f5")
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
        => value is not null and not 0 && value is not "";

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
            "response.created" => Color.FromArgb("#3B82F6"),
            "response.completed" => Color.FromArgb("#10B981"),
            "function_call" => Color.FromArgb("#3B82F6"),
            "workflow.started" => Color.FromArgb("#643FB2"),
            "workflow.completed" => Color.FromArgb("#643FB2"),
            "workflow_event.started" => Color.FromArgb("#643FB2"),
            "workflow_event.completed" => Color.FromArgb("#10B981"),
            "group_chat.started" => Color.FromArgb("#643FB2"),
            "group_chat.round" => Color.FromArgb("#8B5CF6"),
            "handoff" => Color.FromArgb("#F59E0B"),
            "user.message" => Color.FromArgb("#6B7280"),
            "error" => Color.FromArgb("#EF4444"),
            _ => Color.FromArgb("#6B7280")
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToFontAttributeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? FontAttributes.Bold : FontAttributes.None;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class TabActiveColorConverter : IValueConverter
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
        => throw new NotImplementedException();
}

public class IsAssistantConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() != "user";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class IsUserConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() == "user";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class RunningOpacityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? 0.6f : 0f;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class MarkdownStripConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string text || string.IsNullOrEmpty(text))
            return value;
        // Strip bold markers
        return text.Replace("**", "");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
