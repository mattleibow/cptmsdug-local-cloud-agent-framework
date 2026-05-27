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

        // Strip leading YAML frontmatter (delimited by --- on its own line at
        // the start, ended by another --- line). Render it as a monospaced
        // metadata header above the markdown body so the user can see fields
        // like to:, from:, subject:, mailto: etc. verbatim.
        string? frontmatter = null;
        if (md.StartsWith("---"))
        {
            var endIdx = md.IndexOf("\n---", 3, StringComparison.Ordinal);
            if (endIdx > 0)
            {
                var fmLineEnd = md.IndexOf('\n', endIdx + 4);
                frontmatter = md.Substring(4, endIdx - 4).Trim('\r', '\n');
                md = fmLineEnd > 0
                    ? md[(fmLineEnd + 1)..].TrimStart('\r', '\n')
                    : string.Empty;
            }
        }

        var doc = Markdig.Markdown.Parse(md, s_pipeline);
        var fs = new FormattedString();

        if (frontmatter is not null)
        {
            fs.Spans.Add(new Span
            {
                Text = frontmatter,
                FontFamily = "Courier New",
                FontSize = FontSize * 0.9,
                TextColor = TextColor,
                BackgroundColor = Color.FromArgb("#11808080"),
            });
            if (doc.Count > 0)
                fs.Spans.Add(new Span { Text = "\n\n" });
        }

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
                    // Use Material Icons "fiber_manual_record" (small filled dot)
                    fs.Spans.Add(new Span
                    {
                        Text = "  \ue061  ",
                        FontFamily = GlyphShortcodes.FontFamily,
                        FontSize = FontSize * sizeMultiplier * 0.55,
                        TextColor = TextColor,
                    });
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
                // Underlined + accent colour. Tap → Launcher.OpenAsync (mailto:, https:, etc.).
                var linkAttrs = (forcedAttrs ?? FontAttributes.None);
                var linkColor = Color.FromArgb("#643FB2");
                var url = link.Url ?? string.Empty;
                int spansBefore = fs.Spans.Count;
                foreach (var child in link)
                {
                    if (child is LiteralInline lit)
                    {
                        AppendSpan(fs, lit.Content.ToString(), sizeMultiplier, linkAttrs);
                    }
                    else
                    {
                        RenderInline(child, fs, sizeMultiplier, linkAttrs);
                    }
                }
                // Style every span produced by this link as a link (underline + accent + tap).
                for (int i = spansBefore; i < fs.Spans.Count; i++)
                {
                    var span = fs.Spans[i];
                    span.TextColor = linkColor;
                    span.TextDecorations = TextDecorations.Underline;
                    if (!string.IsNullOrEmpty(url))
                    {
                        var tap = new TapGestureRecognizer();
                        tap.Tapped += async (_, _) =>
                        {
                            try { await Launcher.Default.OpenAsync(new Uri(url)); }
                            catch { /* swallow — best-effort opener */ }
                        };
                        span.GestureRecognizers.Add(tap);
                    }
                }
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

        // Split text into runs of normal text + glyph spans wherever a :shortcode: is found
        int pos = 0;
        while (pos < text.Length)
        {
            int open = text.IndexOf(':', pos);
            if (open < 0)
            {
                AppendTextRun(fs, text[pos..], sizeMultiplier, attrs);
                break;
            }
            int close = text.IndexOf(':', open + 1);
            if (close < 0)
            {
                AppendTextRun(fs, text[pos..], sizeMultiplier, attrs);
                break;
            }
            var name = text[(open + 1)..close];
            // Shortcodes are letters/digits/underscores only — skip false positives like "10:30"
            bool valid = name.Length > 0 && name.Length <= 32;
            if (valid)
            {
                foreach (var ch in name)
                {
                    if (!(char.IsLetterOrDigit(ch) || ch == '_')) { valid = false; break; }
                }
            }
            var glyph = valid ? GlyphShortcodes.TryGet(name) : null;
            if (glyph is null)
            {
                // Not a known shortcode — emit literal up to and including the first colon, keep scanning
                AppendTextRun(fs, text[pos..(open + 1)], sizeMultiplier, attrs);
                pos = open + 1;
                continue;
            }
            // Append leading text + glyph span
            if (open > pos)
                AppendTextRun(fs, text[pos..open], sizeMultiplier, attrs);
            fs.Spans.Add(new Span
            {
                Text = glyph,
                FontFamily = GlyphShortcodes.FontFamily,
                FontSize = FontSize * sizeMultiplier * 1.05,
                TextColor = TextColor,
            });
            pos = close + 1;
        }
    }

    private void AppendTextRun(FormattedString fs, string text, double sizeMultiplier, FontAttributes? attrs)
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
