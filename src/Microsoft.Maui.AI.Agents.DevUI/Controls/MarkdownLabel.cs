using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.Maui.Controls;

namespace Microsoft.Maui.AI.Agents.DevUI.Controls;

/// <summary>
/// A Label that renders a small subset of Markdown into a <see cref="FormattedString"/>.
///
/// Supported:
///   - Headings (#, ##, ###) — bigger / bolder
///   - **bold**, *italic*, _emphasis_
///   - `inline code`
///   - Bullet lists (-, *)
///   - Paragraph breaks (blank line)
///
/// Not supported (yet): links, images, fenced code blocks, tables.
/// Everything not handled falls back to plain text.
/// </summary>
public sealed class MarkdownLabel : Label
{
    public static readonly BindableProperty MarkdownProperty =
        BindableProperty.Create(
            nameof(Markdown),
            typeof(string),
            typeof(MarkdownLabel),
            defaultValue: string.Empty,
            propertyChanged: (b, _, _) => ((MarkdownLabel)b).Render());

    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    private static readonly MarkdownPipeline s_pipeline = new MarkdownPipelineBuilder().Build();

    public MarkdownLabel()
    {
        LineBreakMode = LineBreakMode.WordWrap;
    }

    private void Render()
    {
        var md = Markdown ?? string.Empty;
        if (string.IsNullOrWhiteSpace(md))
        {
            FormattedText = null;
            Text = string.Empty;
            return;
        }

        var doc = Markdig.Markdown.Parse(md, s_pipeline);
        var fs = new FormattedString();
        bool first = true;
        foreach (var block in doc)
        {
            if (!first)
                fs.Spans.Add(new Span { Text = "\n\n" });
            first = false;
            RenderBlock(block, fs);
        }

        FormattedText = fs;
    }

    private void RenderBlock(Block block, FormattedString fs, double sizeMultiplier = 1.0, FontAttributes? forcedAttrs = null)
    {
        switch (block)
        {
            case HeadingBlock heading:
            {
                var hSize = heading.Level switch
                {
                    1 => 1.6,
                    2 => 1.35,
                    3 => 1.18,
                    _ => 1.08
                };
                if (heading.Inline is { } inl)
                    RenderInlines(inl, fs, sizeMultiplier * hSize, FontAttributes.Bold);
                break;
            }
            case ParagraphBlock para:
            {
                if (para.Inline is { } inl)
                    RenderInlines(inl, fs, sizeMultiplier, forcedAttrs);
                break;
            }
            case ListBlock list:
            {
                bool firstItem = true;
                foreach (var item in list)
                {
                    if (!firstItem)
                        fs.Spans.Add(new Span { Text = "\n" });
                    firstItem = false;
                    fs.Spans.Add(new Span { Text = "  • " });
                    if (item is ListItemBlock li)
                    {
                        foreach (var sub in li)
                            RenderBlock(sub, fs, sizeMultiplier, forcedAttrs);
                    }
                }
                break;
            }
            case QuoteBlock quote:
            {
                fs.Spans.Add(new Span { Text = "  ▎ ", TextColor = Color.FromArgb("#888") });
                foreach (var sub in quote)
                    RenderBlock(sub, fs, sizeMultiplier, FontAttributes.Italic);
                break;
            }
            default:
            {
                // Fallback: render raw text from any leaf block we don't model explicitly
                if (block is LeafBlock leaf && leaf.Inline is { } inl)
                    RenderInlines(inl, fs, sizeMultiplier, forcedAttrs);
                break;
            }
        }
    }

    private void RenderInlines(ContainerInline container, FormattedString fs,
                               double sizeMultiplier, FontAttributes? forcedAttrs)
    {
        foreach (var inline in container)
            RenderInline(inline, fs, sizeMultiplier, forcedAttrs);
    }

    private void RenderInline(Inline inline, FormattedString fs,
                              double sizeMultiplier, FontAttributes? forcedAttrs)
    {
        switch (inline)
        {
            case LiteralInline lit:
                AppendSpan(fs, lit.Content.ToString(), sizeMultiplier, forcedAttrs);
                break;

            case EmphasisInline em:
            {
                FontAttributes attrs = forcedAttrs ?? FontAttributes.None;
                // ** or __ = bold; * or _ = italic
                attrs |= em.DelimiterCount switch
                {
                    >= 2 => FontAttributes.Bold,
                    _ => FontAttributes.Italic,
                };
                foreach (var child in em)
                    RenderInline(child, fs, sizeMultiplier, attrs);
                break;
            }

            case CodeInline code:
            {
                fs.Spans.Add(new Span
                {
                    Text = code.Content,
                    FontFamily = "Courier New",
                    BackgroundColor = Color.FromArgb("#22808080"),
                    FontSize = FontSize * sizeMultiplier * 0.95
                });
                break;
            }

            case LineBreakInline:
                fs.Spans.Add(new Span { Text = "\n" });
                break;

            case LinkInline link:
            {
                // Render link text only — no navigation in v1
                var linkAttrs = (forcedAttrs ?? FontAttributes.None) | FontAttributes.Italic;
                foreach (var child in link)
                    RenderInline(child, fs, sizeMultiplier, linkAttrs);
                break;
            }

            case ContainerInline container:
            {
                foreach (var child in container)
                    RenderInline(child, fs, sizeMultiplier, forcedAttrs);
                break;
            }

            default:
                // Unknown inline — render as plain text
                AppendSpan(fs, inline.ToString() ?? string.Empty, sizeMultiplier, forcedAttrs);
                break;
        }
    }

    private void AppendSpan(FormattedString fs, string text, double sizeMultiplier, FontAttributes? attrs)
    {
        if (string.IsNullOrEmpty(text))
            return;
        var span = new Span
        {
            Text = text,
            FontSize = FontSize * sizeMultiplier,
        };
        if (attrs is { } a && a != FontAttributes.None)
            span.FontAttributes = a;
        fs.Spans.Add(span);
    }
}
