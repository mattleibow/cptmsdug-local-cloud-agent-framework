namespace Microsoft.Maui.AI.Agents.DevUI.Controls;

/// <summary>
/// Maps GitHub-style shortcodes (e.g. <c>:check:</c>, <c>:food:</c>) to a glyph
/// from the Material Icons font.
///
/// Codepoint reference:
/// https://github.com/google/material-design-icons/blob/master/font/MaterialIcons-Regular.codepoints
///
/// To use the glyph, set <see cref="Span.FontFamily"/> to <c>"MaterialIcons"</c>
/// (the font alias registered in MauiProgram.cs).
/// </summary>
public static class GlyphShortcodes
{
    /// <summary>The MAUI font alias to use for glyph spans.</summary>
    public const string FontFamily = "MaterialIcons";

    private static readonly Dictionary<string, string> s_map = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Status / feedback ───────────────────────────────
        ["check"]        = "\ue876", // check
        ["check_circle"] = "\ue86c",
        ["verified"]     = "\uef76",
        ["fail"]         = "\ue5c9", // close
        ["x"]            = "\ue5c9",
        ["close"]        = "\ue5c9",
        ["warning"]      = "\ue002",
        ["error"]        = "\ue000",
        ["info"]         = "\ue88e",
        ["help"]         = "\ue887",
        ["star"]         = "\ue838",

        // ── Travel ──────────────────────────────────────────
        ["food"]         = "\ue561", // restaurant
        ["restaurant"]   = "\ue561",
        ["dining"]       = "\ueb4c", // local_dining
        ["culture"]      = "\uea3e", // museum
        ["museum"]       = "\uea3e",
        ["landmark"]     = "\ueb4d", // local_attraction
        ["map"]          = "\ue55b",
        ["place"]        = "\ue55f",
        ["car"]          = "\ue531", // directions_car
        ["transport"]    = "\ue530", // directions
        ["walk"]         = "\ue536", // directions_walk
        ["plane"]        = "\ue539", // flight
        ["train"]        = "\ue570", // train
        ["hotel"]        = "\ue53a",
        ["beach"]        = "\ueb3e",
        ["budget"]       = "\ue227", // payments

        // ── News / writing ──────────────────────────────────
        ["news"]         = "\ueb7e", // newspaper
        ["article"]      = "\uef42",
        ["edit"]         = "\ue3c9",
        ["search"]       = "\ue8b6",

        // ── IT helpdesk ─────────────────────────────────────
        ["computer"]     = "\ue30a",
        ["laptop"]       = "\ue31e",
        ["wifi"]         = "\ue63e",
        ["vpn"]          = "\ueb0a", // vpn_lock
        ["network"]      = "\ueb2f", // hub
        ["ticket"]       = "\uef64", // confirmation_number
        ["bug"]          = "\ue868", // bug_report
        ["build"]        = "\ue869", // build (tools/wrench)
        ["wrench"]       = "\ue869",

        // ── Startup pitch ───────────────────────────────────
        ["chart"]        = "\ue26b", // bar_chart
        ["trending_up"]  = "\ue8e5",
        ["money"]        = "\ue227",
        ["lightbulb"]    = "\ue90f",
        ["rocket"]       = "\ueba5", // rocket_launch

        // ── Agents / workflow ───────────────────────────────
        ["robot"]        = "\uf882", // smart_toy
        ["agent"]        = "\uf882",
        ["workflow"]     = "\ue6df", // schema
        ["graph"]        = "\ue922", // account_tree
        ["tool"]         = "\ue869",
        ["bolt"]         = "\uea0b",
        ["sparkles"]     = "\uf06d", // auto_awesome
    };

    /// <summary>
    /// Looks up a glyph by shortcode. Returns null if unknown.
    /// </summary>
    public static string? TryGet(string name)
        => s_map.TryGetValue(name, out var glyph) ? glyph : null;
}
