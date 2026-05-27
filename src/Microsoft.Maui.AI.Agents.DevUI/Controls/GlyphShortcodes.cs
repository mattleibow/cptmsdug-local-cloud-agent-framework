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
        ["check"]        = "\ue5ca", // check
        ["check_circle"] = "\ue86c", // check_circle
        ["verified"]     = "\uef76", // verified
        ["fail"]         = "\ue5c9", // cancel
        ["x"]            = "\ue5cd", // close
        ["close"]        = "\ue5cd", // close
        ["cancel"]       = "\ue5c9", // cancel
        ["warning"]      = "\ue002", // warning
        ["error"]        = "\ue000", // error
        ["report"]       = "\ue160", // report
        ["info"]         = "\ue88e", // info
        ["help"]         = "\ue887", // help
        ["star"]         = "\ue838", // star
        ["circle"]       = "\ue061", // fiber_manual_record (filled dot)
        ["dot"]          = "\ue061",

        // ── Travel ──────────────────────────────────────────
        ["food"]         = "\ue56c", // restaurant
        ["restaurant"]   = "\ue56c", // restaurant
        ["dining"]       = "\ue556", // local_dining
        ["fastfood"]     = "\ue57a", // fastfood
        ["culture"]      = "\uea36", // museum
        ["museum"]       = "\uea36", // museum
        ["landmark"]     = "\ue53f", // local_attraction
        ["attractions"]  = "\uea52", // attractions
        ["map"]          = "\ue55b", // map
        ["place"]        = "\ue55f", // place
        ["location"]     = "\ue0c8", // location_on
        ["car"]          = "\ue531", // directions_car
        ["taxi"]         = "\ue559", // local_taxi
        ["transport"]    = "\ue52e", // directions
        ["walk"]         = "\ue536", // directions_walk
        ["bike"]         = "\ue52f", // directions_bike
        ["plane"]        = "\ue539", // flight
        ["train"]        = "\ue570", // train
        ["subway"]       = "\ue533", // directions_subway
        ["hotel"]        = "\ue53a", // hotel
        ["bed"]          = "\uea45", // king_bed
        ["beach"]        = "\ueb3e", // beach_access
        ["budget"]       = "\uef63", // payments
        ["money"]        = "\ue227", // attach_money
        ["coin"]         = "\ue263", // monetization_on

        // ── News / writing ──────────────────────────────────
        ["news"]         = "\ueb81", // newspaper
        ["article"]      = "\uef42", // article
        ["edit"]         = "\ue3c9", // edit
        ["edit_note"]    = "\ue745", // edit_note
        ["search"]       = "\ue8b6", // search

        // ── IT helpdesk ─────────────────────────────────────
        ["computer"]     = "\ue30a", // computer
        ["laptop"]       = "\ue31e", // laptop
        ["wifi"]         = "\ue63e", // wifi
        ["wifi_off"]     = "\ue648", // wifi_off
        ["vpn"]          = "\ue62f", // vpn_lock
        ["network"]      = "\ue9f4", // hub
        ["router"]       = "\ue328", // router
        ["cable"]        = "\uefe6", // cable
        ["ticket"]       = "\ue638", // confirmation_number
        ["bug"]          = "\ue868", // bug_report
        ["build"]        = "\ue869", // build
        ["wrench"]       = "\ue869", // build
        ["construction"] = "\uea3c", // construction
        ["engineering"]  = "\uea3d", // engineering

        // ── Startup pitch ───────────────────────────────────
        ["chart"]        = "\ue26b", // bar_chart
        ["insert_chart"] = "\ue24b", // insert_chart
        ["trending_up"]  = "\ue8e5", // trending_up
        ["lightbulb"]    = "\ue0f0", // lightbulb
        ["idea"]         = "\uea24", // emoji_objects
        ["rocket"]       = "\uebba", // rocket  (falls back to rocket_launch \ueb9b on some fonts)
        ["rocket_launch"]= "\ueb9b", // rocket_launch

        // ── Calendar / scheduling ───────────────────────────
        ["event"]        = "\ue878", // event
        ["calendar"]     = "\ue935", // calendar_today
        ["calendar_month"] = "\uebcc", // calendar_month
        ["schedule"]     = "\ue8b5", // schedule
        ["today"]        = "\ue8df", // today
        ["clock"]        = "\ue8b5", // schedule
        ["alarm"]        = "\ue855", // alarm
        ["meeting"]      = "\uea66", // groups
        ["people"]       = "\ue7fb", // people
        ["person"]       = "\ue7fd", // person
        ["mail"]         = "\ue0be", // mail
        ["email"]        = "\ue0be", // email
        ["send"]         = "\ue163", // send

        // ── Agents / workflow ───────────────────────────────
        ["robot"]        = "\uf06c", // smart_toy
        ["agent"]        = "\uf06c", // smart_toy
        ["workflow"]     = "\ue4fd", // schema
        ["graph"]        = "\ue97a", // account_tree
        ["bolt"]         = "\uea0b", // bolt
        ["flash"]        = "\ue3e7", // flash_on
        ["sparkles"]     = "\ue65f", // auto_awesome
    };

    /// <summary>
    /// Looks up a glyph by shortcode. Returns null if unknown.
    /// </summary>
    public static string? TryGet(string name)
        => s_map.TryGetValue(name, out var glyph) ? glyph : null;
}
