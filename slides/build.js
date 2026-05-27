// Unifying Local and Cloud AI with Microsoft Agent Framework
// Deck for Cape Town MS Developer User Group (@CPTMSDUG)
// Speaker: Matthew Leibowitz (@mattleibow)
//
// Palette: Indigo (cloud night) + Coral (device glow) + Amber accent
// Motif: ☁ ↔ 📱 — device-to-cloud arrow as a recurring visual element
// Sandwich: dark title + demo + closing slides; light content slides

const PptxGenJS = require("pptxgenjs");
const Prism = require("prismjs");
require("prismjs/components/prism-csharp");
require("prismjs/components/prism-bash");

// ── Palette ───────────────────────────────────────────────────────────────
const C = {
  // Dark / cloud
  indigoDeep:   "0F0A2E",   // near-black indigo for dark backgrounds
  indigoMid:    "1E1B4B",
  indigoBright: "6366F1",   // accent indigo for highlights

  // Light
  bgLight:      "FAF8FF",
  cardLight:    "FFFFFF",
  borderLight:  "E5E1F5",

  // Coral / device glow
  coral:        "FB7185",
  coralDeep:    "E11D48",

  // Amber / warm secondary
  amber:        "FBBF24",

  // Text
  textDark:     "0F0A2E",
  textMuted:    "6B6B8F",
  textOnDark:   "FAF8FF",
  textOnDarkMuted: "CFCBED",
};

// ── Fonts ─────────────────────────────────────────────────────────────────
const F = {
  header: "Georgia",
  body:   "Calibri",
  mono:   "Consolas",
};

// ── Syntax highlight theme (for dark code backgrounds) ────────────────────
// Maps prismjs token types to colors. Anything not in the map falls through
// to the parent (default) color.
const SYNTAX = {
  // Text-on-dark default is C.textOnDark (FAF8FF) — set globally on the addText call
  keyword:      "FB7185",   // coral  (using, var, await, async, new, class, if, return…)
  "class-name": "A78BFA",   // light violet (IChatClient, AIAgent, AgentThread…)
  builtin:      "A78BFA",
  function:     "86EFAC",   // mint (method names being called)
  string:       "FBBF24",   // amber
  number:       "F472B6",   // pink
  comment:      "8B88B5",   // muted lavender — italic via run options
  punctuation:  "B8B5D9",   // muted off-white for brackets/commas
  operator:     "CFCBED",
  // prismjs sub-types
  "attr-name":  "A78BFA",
  "attr-value": "FBBF24",
  // bash
  "shell-symbol": "B8B5D9",
  // ours: rendered prompt/comment style for shell intro lines
  "shell-prompt": "8B88B5",
};

// Tokenize a code string with prismjs and return pptxgenjs text-runs.
// Leading spaces at the start of each line are converted to NBSP so PowerPoint
// preserves indentation (OOXML otherwise collapses leading whitespace).
function highlightCode(code, lang = "csharp") {
  // Preprocess: convert leading spaces on each line to non-breaking spaces.
  // PPTX/OOXML strips leading whitespace at paragraph start otherwise.
  const preprocessed = code
    .replace(/\t/g, "    ")
    .replace(/^( +)/gm, (m) => "\u00A0".repeat(m.length));

  const grammar = Prism.languages[lang] || Prism.languages.csharp;
  const tokens = Prism.tokenize(preprocessed, grammar);
  const runs = [];

  function emit(text, type) {
    if (!text) return;
    const color = SYNTAX[type] || "FAF8FF";
    const isComment = type === "comment" || type === "shell-prompt";
    const opts = { color, fontFace: "Consolas", italic: isComment };

    // Split on \n and emit a breakLine marker between segments so that
    // PowerPoint treats each line as its own paragraph — this prevents
    // leading-whitespace collapse after newlines inside a single run.
    const lines = text.split("\n");
    for (let i = 0; i < lines.length; i++) {
      if (i > 0) {
        runs.push({ text: "", options: { ...opts, breakLine: true } });
      }
      if (lines[i]) {
        runs.push({ text: lines[i], options: opts });
      }
    }
  }

  function walk(tok, parentType) {
    if (typeof tok === "string") {
      emit(tok, parentType);
      return;
    }
    const t = tok.type;
    if (Array.isArray(tok.content)) {
      for (const c of tok.content) walk(c, t);
    } else if (typeof tok.content === "string") {
      emit(tok.content, t);
    } else {
      walk(tok.content, t);
    }
  }

  for (const tok of tokens) walk(tok, null);
  return runs;
}

// Highlight multiple language blocks and concatenate.
function highlightMulti(blocks) {
  const runs = [];
  for (const b of blocks) {
    runs.push(...highlightCode(b.code, b.lang));
  }
  return runs;
}

const pptx = new PptxGenJS();
pptx.layout = "LAYOUT_WIDE"; // 13.333" x 7.5"
pptx.author = "Matthew Leibowitz";
pptx.title = "Unifying Local and Cloud AI with Microsoft Agent Framework";
pptx.subject = "Cape Town MS Developer User Group";

const W = 13.333;
const H = 7.5;

// ── Helpers ───────────────────────────────────────────────────────────────

// Recurring motif: device ↔ cloud glyph (small, top-right or bottom)
function addMotif(slide, opts = {}) {
  const { x = W - 2.2, y = 0.55, color = C.indigoBright, scale = 1 } = opts;
  // device square
  slide.addShape("roundRect", {
    x, y, w: 0.32 * scale, h: 0.32 * scale,
    line: { color, width: 1.5 },
    fill: { type: "none" },
    rectRadius: 0.05,
  });
  // arrow
  slide.addText("↔", {
    x: x + 0.32 * scale, y: y - 0.05 * scale, w: 0.45 * scale, h: 0.42 * scale,
    fontSize: 18 * scale, color, bold: true, fontFace: F.body,
    align: "center", valign: "middle",
  });
  // cloud (use ☁)
  slide.addText("☁", {
    x: x + 0.78 * scale, y: y - 0.08 * scale, w: 0.45 * scale, h: 0.5 * scale,
    fontSize: 22 * scale, color, fontFace: F.body,
    align: "center", valign: "middle",
  });
}

// Page footer for light slides
function addFooter(slide, pageNum, total) {
  slide.addText(
    [
      { text: "Unifying Local and Cloud AI", options: { color: C.textMuted } },
      { text: "  ·  ", options: { color: C.textMuted } },
      { text: "@mattleibow", options: { color: C.coral, bold: true } },
      { text: "  ·  @CPTMSDUG", options: { color: C.textMuted } },
    ],
    {
      x: 0.5, y: H - 0.45, w: 8, h: 0.3,
      fontSize: 10, fontFace: F.body, align: "left", valign: "middle",
    }
  );
  slide.addText(`${pageNum} / ${total}`, {
    x: W - 1.5, y: H - 0.45, w: 1, h: 0.3,
    fontSize: 10, fontFace: F.body, color: C.textMuted,
    align: "right", valign: "middle",
  });
}

// Section eyebrow label
function eyebrow(slide, text, color = C.coral) {
  slide.addText(text.toUpperCase(), {
    x: 0.6, y: 0.55, w: 8, h: 0.35,
    fontSize: 12, bold: true, fontFace: F.body,
    color, charSpacing: 4,
  });
}

// Big slide title (light bg)
function title(slide, text, opts = {}) {
  slide.addText(text, {
    x: 0.6, y: opts.y ?? 0.9, w: opts.w ?? 11, h: 0.9,
    fontSize: opts.size ?? 38, bold: true, fontFace: F.header,
    color: opts.color ?? C.textDark,
  });
}

// Dark slide background
function darkBg(slide) {
  slide.background = { color: C.indigoDeep };
}

const TOTAL = 16;

// ═══════════════════════════════════════════════════════════════════════════
// SLIDE 1 — TITLE
// ═══════════════════════════════════════════════════════════════════════════
{
  const s = pptx.addSlide();
  darkBg(s);

  // Left coral bar (motif: device side)
  s.addShape("rect", { x: 0, y: 0, w: 0.4, h: H, fill: { color: C.coral }, line: { color: C.coral } });

  // Big motif top-right (oversized for impact)
  s.addText("📱", {
    x: W - 3.2, y: 0.6, w: 1.2, h: 1.2,
    fontSize: 60, align: "center", valign: "middle", color: C.coral, fontFace: F.body,
  });
  s.addText("↔", {
    x: W - 2.2, y: 0.8, w: 1.0, h: 1.0,
    fontSize: 50, align: "center", valign: "middle", color: C.textOnDarkMuted, bold: true, fontFace: F.body,
  });
  s.addText("☁", {
    x: W - 1.4, y: 0.55, w: 1.2, h: 1.25,
    fontSize: 64, align: "center", valign: "middle", color: C.indigoBright, fontFace: F.body,
  });

  // Eyebrow
  s.addText("CAPE TOWN MS DEVELOPER USER GROUP", {
    x: 0.9, y: 2.2, w: 12, h: 0.35,
    fontSize: 14, bold: true, fontFace: F.body,
    color: C.coral, charSpacing: 6,
  });

  // Main title
  s.addText("Unifying", {
    x: 0.9, y: 2.7, w: 12, h: 1.1,
    fontSize: 64, bold: true, fontFace: F.header, color: C.textOnDark,
  });
  s.addText([
    { text: "Local ", options: { color: C.coral, bold: true } },
    { text: "and ", options: { color: C.textOnDarkMuted } },
    { text: "Cloud ", options: { color: C.indigoBright, bold: true } },
    { text: "AI", options: { color: C.textOnDark } },
  ], {
    x: 0.9, y: 3.7, w: 12, h: 1.1,
    fontSize: 64, bold: true, fontFace: F.header,
  });

  s.addText("with the Microsoft Agent Framework", {
    x: 0.9, y: 4.85, w: 12, h: 0.5,
    fontSize: 22, italic: true, fontFace: F.header, color: C.textOnDarkMuted,
  });

  // Speaker line
  s.addText([
    { text: "Matthew Leibowitz", options: { bold: true, color: C.textOnDark } },
    { text: "   ·   Software Engineer @ Microsoft   ·   ", options: { color: C.textOnDarkMuted } },
    { text: "@mattleibow", options: { bold: true, color: C.amber } },
  ], {
    x: 0.9, y: H - 1.1, w: 12, h: 0.4,
    fontSize: 16, fontFace: F.body,
  });
}

// ═══════════════════════════════════════════════════════════════════════════
// SLIDE 2 — ABOUT ME
// ═══════════════════════════════════════════════════════════════════════════
{
  const s = pptx.addSlide();
  s.background = { color: C.bgLight };
  eyebrow(s, "About the Speaker");
  title(s, "Hi, I'm Matthew");

  // Avatar circle (placeholder w/ initials)
  s.addShape("ellipse", {
    x: 0.6, y: 2.2, w: 3.6, h: 3.6,
    fill: { color: C.indigoMid },
    line: { color: C.coral, width: 4 },
  });
  s.addText("ML", {
    x: 0.6, y: 2.2, w: 3.6, h: 3.6,
    fontSize: 96, bold: true, fontFace: F.header,
    color: C.textOnDark, align: "center", valign: "middle",
  });

  // Bio block
  s.addText("Matthew Leibowitz", {
    x: 5, y: 2.3, w: 7.5, h: 0.7,
    fontSize: 32, bold: true, fontFace: F.header, color: C.textDark,
  });
  s.addText("Software Engineer at Microsoft", {
    x: 5, y: 3.0, w: 7.5, h: 0.45,
    fontSize: 18, italic: true, fontFace: F.body, color: C.textMuted,
  });

  // Handle rows: GitHub / Twitter / LinkedIn
  const handles = [
    ["GitHub",   "@mattleibow", C.indigoBright],
    ["X / Twitter", "@mattleibow", C.coral],
    ["LinkedIn", "@mattleibow", C.amber],
  ];
  handles.forEach(([label, handle, color], i) => {
    const y = 3.85 + i * 0.7;
    // small colored circle bullet
    s.addShape("ellipse", {
      x: 5, y: y + 0.07, w: 0.35, h: 0.35,
      fill: { color }, line: { color },
    });
    s.addText(label.charAt(0), {
      x: 5, y: y + 0.07, w: 0.35, h: 0.35,
      fontSize: 14, bold: true, color: C.bgLight, align: "center", valign: "middle", fontFace: F.body,
    });
    s.addText([
      { text: `${label}   `, options: { color: C.textMuted, fontSize: 14 } },
      { text: handle, options: { color: C.textDark, bold: true, fontSize: 20, fontFace: F.body } },
    ], {
      x: 5.55, y, w: 7, h: 0.5, valign: "middle", fontFace: F.body,
    });
  });

  addMotif(s);
  addFooter(s, 2, TOTAL);
}

// ═══════════════════════════════════════════════════════════════════════════
// SLIDE 3 — THE HYBRID AI REALITY (tradeoff triangle)
// ═══════════════════════════════════════════════════════════════════════════
{
  const s = pptx.addSlide();
  s.background = { color: C.bgLight };
  eyebrow(s, "The Why");
  title(s, "Why one model is never enough");

  // Subhead
  s.addText("Every AI deployment trades off three things. No single tier wins all three.", {
    x: 0.6, y: 1.8, w: 12, h: 0.5,
    fontSize: 18, italic: true, fontFace: F.body, color: C.textMuted,
  });

  // Triangle — three corners
  const tri = {
    cx: 3.8, cy: 4.6, r: 1.85,
  };
  const corners = [
    { label: "PRIVACY",    sub: "Data never leaves the device", color: C.coral,        dx: 0,    dy: -tri.r },
    { label: "LATENCY",    sub: "Instant response",             color: C.amber,        dx: tri.r * 0.95, dy: tri.r * 0.55 },
    { label: "CAPABILITY", sub: "Frontier reasoning, scale",    color: C.indigoBright, dx: -tri.r * 0.95, dy: tri.r * 0.55 },
  ];

  // Triangle lines
  const pts = corners.map(c => ({ x: tri.cx + c.dx, y: tri.cy + c.dy }));
  for (let i = 0; i < 3; i++) {
    const a = pts[i], b = pts[(i + 1) % 3];
    s.addShape("line", {
      x: Math.min(a.x, b.x), y: Math.min(a.y, b.y),
      w: Math.abs(b.x - a.x), h: Math.abs(b.y - a.y),
      line: { color: C.borderLight, width: 2, dashType: "dash" },
      flipH: b.x < a.x, flipV: b.y < a.y,
    });
  }

  corners.forEach((c, i) => {
    const p = pts[i];
    s.addShape("ellipse", {
      x: p.x - 0.45, y: p.y - 0.45, w: 0.9, h: 0.9,
      fill: { color: c.color }, line: { color: c.color },
    });
    s.addText(c.label, {
      x: p.x - 1.4, y: p.y + 0.5, w: 2.8, h: 0.4,
      fontSize: 14, bold: true, color: C.textDark, align: "center", fontFace: F.body, charSpacing: 2,
    });
    s.addText(c.sub, {
      x: p.x - 1.6, y: p.y + 0.85, w: 3.2, h: 0.5,
      fontSize: 11, italic: true, color: C.textMuted, align: "center", fontFace: F.body,
    });
  });

  // Center label
  s.addText("Pick 2", {
    x: tri.cx - 0.8, y: tri.cy - 0.25, w: 1.6, h: 0.5,
    fontSize: 18, bold: true, italic: true,
    color: C.textMuted, align: "center", fontFace: F.header,
  });

  // Right column: "The interesting apps live in the seams"
  s.addShape("roundRect", {
    x: 7.4, y: 2.6, w: 5.4, h: 3.8,
    fill: { color: C.indigoDeep }, line: { color: C.indigoDeep },
    rectRadius: 0.15,
  });
  s.addText("\u201CThe interesting apps", {
    x: 7.7, y: 3.0, w: 5.0, h: 0.7,
    fontSize: 26, bold: true, color: C.textOnDark, fontFace: F.header,
  });
  s.addText("live in the seams.\u201D", {
    x: 7.7, y: 3.6, w: 5.0, h: 0.7,
    fontSize: 26, bold: true, color: C.coral, fontFace: F.header,
  });
  s.addText(
    "Modern apps combine multiple models across multiple environments. " +
    "The job is no longer \u201Cpick a model\u201D — it's orchestrating the right one for each task.",
    {
      x: 7.7, y: 4.5, w: 5.0, h: 1.6,
      fontSize: 14, color: C.textOnDarkMuted, fontFace: F.body, italic: true,
    }
  );

  addMotif(s);
  addFooter(s, 3, TOTAL);
}

// ═══════════════════════════════════════════════════════════════════════════
// SLIDE 4 — CLOUD AI (start with what you know)
// ═══════════════════════════════════════════════════════════════════════════
{
  const s = pptx.addSlide();
  s.background = { color: C.bgLight };
  eyebrow(s, "Tier 3  ·  Start With What You Know", C.indigoBright);
  title(s, "Cloud AI — the frontier brain");

  // Left: big cloud glyph
  s.addText("☁", {
    x: 0.6, y: 2.4, w: 4.5, h: 4.0,
    fontSize: 220, color: C.indigoBright, align: "center", valign: "middle", fontFace: F.body,
  });

  // Right: provider chips + strengths/limits
  const px = 5.4, pw = 7.5;

  s.addText("Providers", {
    x: px, y: 1.9, w: pw, h: 0.4,
    fontSize: 16, bold: true, color: C.textMuted, fontFace: F.body, charSpacing: 2,
  });

  const providers = [
    ["Azure OpenAI / AI Foundry", C.indigoBright, "GPT-4o, GPT-5, o-series"],
    ["AWS Bedrock",               C.amber,        "Nova, Claude, Llama, Mistral"],
    ["Google Vertex AI",          C.coral,        "Gemini Pro / Flash, Model Garden"],
  ];
  providers.forEach(([name, color, models], i) => {
    const y = 2.4 + i * 0.65;
    s.addShape("roundRect", {
      x: px, y, w: 0.25, h: 0.45,
      fill: { color }, line: { color },
      rectRadius: 0.05,
    });
    s.addText(name, {
      x: px + 0.4, y, w: 3.5, h: 0.45,
      fontSize: 16, bold: true, color: C.textDark, fontFace: F.body, valign: "middle",
    });
    s.addText(models, {
      x: px + 3.9, y, w: 3.5, h: 0.45,
      fontSize: 13, color: C.textMuted, italic: true, fontFace: F.body, valign: "middle",
    });
  });

  // Strengths card
  s.addShape("roundRect", {
    x: px, y: 4.6, w: 3.6, h: 2.0,
    fill: { color: C.cardLight }, line: { color: C.indigoBright, width: 2 },
    rectRadius: 0.1,
  });
  s.addText("STRENGTHS", {
    x: px + 0.2, y: 4.7, w: 3.2, h: 0.3,
    fontSize: 11, bold: true, color: C.indigoBright, charSpacing: 2, fontFace: F.body,
  });
  s.addText(
    "Frontier reasoning\nMassive context windows\nMultimodal · tool calling\nLatest frontier models",
    {
      x: px + 0.2, y: 5.05, w: 3.2, h: 1.5,
      fontSize: 13, color: C.textDark, fontFace: F.body, paraSpaceAfter: 4,
    }
  );

  // Costs card
  s.addShape("roundRect", {
    x: px + 3.9, y: 4.6, w: 3.6, h: 2.0,
    fill: { color: C.cardLight }, line: { color: C.coral, width: 2 },
    rectRadius: 0.1,
  });
  s.addText("THE TAB", {
    x: px + 4.1, y: 4.7, w: 3.2, h: 0.3,
    fontSize: 11, bold: true, color: C.coral, charSpacing: 2, fontFace: F.body,
  });
  s.addText(
    "$ per million tokens\nNetwork latency · jitter\nData leaves the device\nNeeds connectivity",
    {
      x: px + 4.1, y: 5.05, w: 3.2, h: 1.5,
      fontSize: 13, color: C.textDark, fontFace: F.body, paraSpaceAfter: 4,
    }
  );

  addMotif(s);
  addFooter(s, 4, TOTAL);
}

// ═══════════════════════════════════════════════════════════════════════════
// SLIDE 5 — DEMO 1 (Cloud)
// ═══════════════════════════════════════════════════════════════════════════
{
  const s = pptx.addSlide();
  darkBg(s);

  s.addShape("rect", { x: 0, y: 0, w: 0.4, h: H, fill: { color: C.indigoBright }, line: { color: C.indigoBright } });

  s.addText("DEMO  01", {
    x: 0.9, y: 0.7, w: 6, h: 0.6,
    fontSize: 18, bold: true, color: C.indigoBright, charSpacing: 8, fontFace: F.body,
  });
  s.addText("Calling the Cloud", {
    x: 0.9, y: 1.4, w: 12, h: 1.3,
    fontSize: 60, bold: true, color: C.textOnDark, fontFace: F.header,
  });
  s.addText("A direct Azure OpenAI call — the baseline everyone already ships", {
    x: 0.9, y: 2.7, w: 12, h: 0.5,
    fontSize: 18, italic: true, color: C.textOnDarkMuted, fontFace: F.body,
  });

  // Code block
  s.addShape("roundRect", {
    x: 0.9, y: 3.5, w: 7.5, h: 3.4,
    fill: { color: C.indigoMid }, line: { color: C.indigoBright, width: 1 },
    rectRadius: 0.1,
  });
  s.addText(
    highlightCode(
`var client = new AzureOpenAIClient(
    new Uri(endpoint),
    new DefaultAzureCredential());

var chat = client.GetChatClient("gpt-4.1");

var response = await chat.CompleteChatAsync(
    "Summarise the agenda for today.");

Console.WriteLine(response.Value.Content[0].Text);`, "csharp"),
    {
      x: 1.1, y: 3.7, w: 7.1, h: 3.0,
      fontSize: 13, fontFace: F.mono, color: C.textOnDark,
      paraSpaceAfter: 0,
    }
  );

  // "Watch for" side panel
  s.addText("WATCH FOR", {
    x: 8.8, y: 3.5, w: 4, h: 0.3,
    fontSize: 12, bold: true, color: C.coral, charSpacing: 4, fontFace: F.body,
  });
  const watchItems = [
    ["⚡ Latency", "Network round-trip — feel it"],
    ["🌐 Connectivity", "Toggle Wi-Fi off and watch it fail"],
    ["🧠 Quality", "Frontier-class reasoning"],
    ["💸 Cost", "Each token is metered"],
  ];
  watchItems.forEach(([h, sub], i) => {
    const y = 3.95 + i * 0.7;
    s.addText(h, {
      x: 8.8, y, w: 4, h: 0.3,
      fontSize: 14, bold: true, color: C.textOnDark, fontFace: F.body,
    });
    s.addText(sub, {
      x: 8.8, y: y + 0.3, w: 4, h: 0.3,
      fontSize: 12, color: C.textOnDarkMuted, italic: true, fontFace: F.body,
    });
  });

  addMotif(s, { color: C.indigoBright });
}

// ═══════════════════════════════════════════════════════════════════════════
// SLIDE 6 — OS ON-DEVICE MODELS (the surprise)
// ═══════════════════════════════════════════════════════════════════════════
{
  const s = pptx.addSlide();
  s.background = { color: C.bgLight };
  eyebrow(s, "Tier 1  ·  The Surprise");
  title(s, "Your OS already ships an LLM");

  s.addText("Vendor-managed, shared across apps, zero cost, fully offline. ~3B-class models.", {
    x: 0.6, y: 1.75, w: 12, h: 0.5,
    fontSize: 17, italic: true, color: C.textMuted, fontFace: F.body,
  });

  // Three OS columns
  const cols = [
    {
      vendor: "Apple",
      product: "Apple Intelligence",
      api: "FoundationModels (Swift)",
      detail: "iOS 26 · macOS Tahoe\n~3B params · multilingual\nLoRA adapters · tool calls",
      status: "✅  shipped",
      color: C.coral,
    },
    {
      vendor: "Microsoft",
      product: "Phi Silica",
      api: "Windows AI APIs (.NET)",
      detail: "Copilot+ PCs · NPU-accelerated\n~3.3B Phi-3.5-class model\nSummarize · rewrite · generate",
      status: "🔜  maui-labs PR #178",
      color: C.indigoBright,
    },
    {
      vendor: "Google",
      product: "ML Kit + Gemini Nano",
      api: "ML Kit (Kotlin/Java)",
      detail: "Android · AICore service\nCompact Gemini Nano\nSummarize · proofread · rewrite",
      status: "🔬  .NET wrapper under investigation",
      color: C.amber,
    },
  ];

  const colW = 4.05, gap = 0.15, startX = 0.6;
  cols.forEach((c, i) => {
    const x = startX + i * (colW + gap);
    // card
    s.addShape("roundRect", {
      x, y: 2.5, w: colW, h: 3.9,
      fill: { color: C.cardLight }, line: { color: C.borderLight, width: 1 },
      rectRadius: 0.12,
    });
    // top color stripe
    s.addShape("rect", {
      x, y: 2.5, w: colW, h: 0.18,
      fill: { color: c.color }, line: { color: c.color },
    });
    // vendor
    s.addText(c.vendor.toUpperCase(), {
      x: x + 0.3, y: 2.85, w: colW - 0.6, h: 0.35,
      fontSize: 11, bold: true, color: c.color, charSpacing: 3, fontFace: F.body,
    });
    // product
    s.addText(c.product, {
      x: x + 0.3, y: 3.2, w: colW - 0.6, h: 0.6,
      fontSize: 22, bold: true, color: C.textDark, fontFace: F.header,
    });
    // api line
    s.addText(c.api, {
      x: x + 0.3, y: 3.8, w: colW - 0.6, h: 0.4,
      fontSize: 13, italic: true, color: C.textMuted, fontFace: F.mono,
    });
    // divider
    s.addShape("line", {
      x: x + 0.3, y: 4.3, w: colW - 0.6, h: 0,
      line: { color: C.borderLight, width: 1 },
    });
    // detail
    s.addText(c.detail, {
      x: x + 0.3, y: 4.45, w: colW - 0.6, h: 1.45,
      fontSize: 13, color: C.textDark, fontFace: F.body, paraSpaceAfter: 4,
    });
    // status pill
    s.addText(c.status, {
      x: x + 0.3, y: 5.95, w: colW - 0.6, h: 0.35,
      fontSize: 12, bold: true, color: c.color, fontFace: F.body,
    });
  });

  // Bottom unifier banner
  s.addShape("roundRect", {
    x: 0.6, y: 6.55, w: W - 1.2, h: 0.55,
    fill: { color: C.indigoDeep }, line: { color: C.indigoDeep },
    rectRadius: 0.08,
  });
  s.addText(
    [
      { text: "Unified for .NET MAUI:  ", options: { color: C.textOnDarkMuted } },
      { text: "Microsoft.Maui.Essentials.AI", options: { color: C.coral, bold: true, fontFace: F.mono } },
      { text: "  →  one IChatClient shape, Apple today · Phi Silica in progress · Gemini under investigation", options: { color: C.textOnDarkMuted, italic: true } },
    ],
    {
      x: 0.6, y: 6.55, w: W - 1.2, h: 0.55,
      fontSize: 13, fontFace: F.body, align: "center", valign: "middle",
    }
  );

  addMotif(s);
  addFooter(s, 6, TOTAL);
}

// ═══════════════════════════════════════════════════════════════════════════
// SLIDE 7 — DEMO 2 (Apple Intelligence)
// ═══════════════════════════════════════════════════════════════════════════
{
  const s = pptx.addSlide();
  darkBg(s);

  s.addShape("rect", { x: 0, y: 0, w: 0.4, h: H, fill: { color: C.coral }, line: { color: C.coral } });

  s.addText("DEMO  02", {
    x: 0.9, y: 0.7, w: 6, h: 0.6,
    fontSize: 18, bold: true, color: C.coral, charSpacing: 8, fontFace: F.body,
  });
  s.addText("Living on the Edge", {
    x: 0.9, y: 1.4, w: 12, h: 1.3,
    fontSize: 60, bold: true, color: C.textOnDark, fontFace: F.header,
  });
  s.addText("Apple Intelligence running 100% on this Mac — no network, no key, no bill", {
    x: 0.9, y: 2.7, w: 12, h: 0.5,
    fontSize: 18, italic: true, color: C.textOnDarkMuted, fontFace: F.body,
  });

  // Code block (Swift-ish)
  s.addShape("roundRect", {
    x: 0.9, y: 3.5, w: 7.5, h: 3.4,
    fill: { color: C.indigoMid }, line: { color: C.coral, width: 1 },
    rectRadius: 0.1,
  });
  s.addText(
    highlightCode(
`using Microsoft.Maui.Essentials.AI;
using Microsoft.Extensions.AI;

IChatClient chat = new AppleIntelligenceChatClient();

var response = await chat.GetResponseAsync(
    "Summarise the agenda for today.");

Console.WriteLine(response.Text);

// ✈️  Works in airplane mode
// 🆓  No API key, no per-token cost
// 🔒  Data never leaves the device`, "csharp"),
    {
      x: 1.1, y: 3.7, w: 7.1, h: 3.0,
      fontSize: 12, fontFace: F.mono, color: C.textOnDark,
      paraSpaceAfter: 0,
    }
  );

  // "Watch for"
  s.addText("WATCH FOR", {
    x: 8.8, y: 3.5, w: 4, h: 0.3,
    fontSize: 12, bold: true, color: C.coral, charSpacing: 4, fontFace: F.body,
  });
  const watchItems = [
    ["⚡ Instant first token", "No round-trip — sub-second"],
    ["🔒 Airplane mode", "Disconnect — still works"],
    ["📏 Tiny but capable", "~3B params on the NPU"],
    ["⚠ The limit", "Short context · narrow skills"],
  ];
  watchItems.forEach(([h, sub], i) => {
    const y = 3.95 + i * 0.7;
    s.addText(h, {
      x: 8.8, y, w: 4, h: 0.3,
      fontSize: 14, bold: true, color: C.textOnDark, fontFace: F.body,
    });
    s.addText(sub, {
      x: 8.8, y: y + 0.3, w: 4, h: 0.3,
      fontSize: 12, color: C.textOnDarkMuted, italic: true, fontFace: F.body,
    });
  });

  addMotif(s, { color: C.coral });
}

// ═══════════════════════════════════════════════════════════════════════════
// SLIDE 8 — BRING YOUR OWN LOCAL (the middle ground)
// ═══════════════════════════════════════════════════════════════════════════
{
  const s = pptx.addSlide();
  s.background = { color: C.bgLight };
  eyebrow(s, "Tier 2  ·  The Practical Middle", C.amber);
  title(s, "Bring your own local model");

  s.addText("The forgotten tier — cloud-class capability with local-class privacy", {
    x: 0.6, y: 1.75, w: 12, h: 0.5,
    fontSize: 17, italic: true, color: C.textMuted, fontFace: F.body,
  });

  // Three approach cards
  const approaches = [
    {
      title: "Foundry Local",
      sub: "Cloud-grade LLMs, local host",
      body: "Microsoft's runtime for running Phi, Llama, Qwen and friends on the user's box. .NET-friendly, ONNX-backed, GPU/NPU accelerated.",
      tag: "RECOMMENDED",
      color: C.coral,
    },
    {
      title: "ONNX Runtime GenAI",
      sub: "Ship your own model",
      body: "Cross-platform C# / native inference. Bring any ONNX-converted model — tiny custom classifiers to multi-billion-param LLMs.",
      tag: "FLEXIBLE",
      color: C.indigoBright,
    },
    {
      title: "Tiny Bespoke Models",
      sub: "Right-sized for the task",
      body: "Sometimes a 50MB classifier beats a 3B LLM. Embeddings, intent detection, structured extraction — fast, cheap, embarrassingly good.",
      tag: "UNDERRATED",
      color: C.amber,
    },
  ];

  const colW = 4.05, gap = 0.15, startX = 0.6;
  approaches.forEach((a, i) => {
    const x = startX + i * (colW + gap);
    s.addShape("roundRect", {
      x, y: 2.5, w: colW, h: 4.3,
      fill: { color: C.cardLight }, line: { color: a.color, width: 2 },
      rectRadius: 0.12,
    });
    // tag pill
    s.addShape("roundRect", {
      x: x + 0.3, y: 2.75, w: 1.7, h: 0.32,
      fill: { color: a.color }, line: { color: a.color },
      rectRadius: 0.16,
    });
    s.addText(a.tag, {
      x: x + 0.3, y: 2.75, w: 1.7, h: 0.32,
      fontSize: 9, bold: true, color: C.bgLight, charSpacing: 2, fontFace: F.body,
      align: "center", valign: "middle",
    });
    // title
    s.addText(a.title, {
      x: x + 0.3, y: 3.25, w: colW - 0.6, h: 0.6,
      fontSize: 22, bold: true, color: C.textDark, fontFace: F.header,
    });
    s.addText(a.sub, {
      x: x + 0.3, y: 3.85, w: colW - 0.6, h: 0.4,
      fontSize: 13, italic: true, color: a.color, fontFace: F.body,
    });
    s.addText(a.body, {
      x: x + 0.3, y: 4.4, w: colW - 0.6, h: 2.2,
      fontSize: 13, color: C.textDark, fontFace: F.body,
    });
  });

  addMotif(s);
  addFooter(s, 8, TOTAL);
}

// ═══════════════════════════════════════════════════════════════════════════
// SLIDE 9 — DEMO 3 (Foundry Local)
// ═══════════════════════════════════════════════════════════════════════════
{
  const s = pptx.addSlide();
  darkBg(s);

  s.addShape("rect", { x: 0, y: 0, w: 0.4, h: H, fill: { color: C.amber }, line: { color: C.amber } });

  s.addText("DEMO  03", {
    x: 0.9, y: 0.7, w: 6, h: 0.6,
    fontSize: 18, bold: true, color: C.amber, charSpacing: 8, fontFace: F.body,
  });
  s.addText("A Big Brain, Locally", {
    x: 0.9, y: 1.4, w: 12, h: 1.3,
    fontSize: 60, bold: true, color: C.textOnDark, fontFace: F.header,
  });
  s.addText("Foundry Local hosting a Phi-class LLM on this laptop — OpenAI-compatible endpoint", {
    x: 0.9, y: 2.7, w: 12, h: 0.5,
    fontSize: 18, italic: true, color: C.textOnDarkMuted, fontFace: F.body,
  });

  s.addShape("roundRect", {
    x: 0.9, y: 3.5, w: 7.5, h: 3.4,
    fill: { color: C.indigoMid }, line: { color: C.amber, width: 1 },
    rectRadius: 0.1,
  });
  s.addText(
    highlightMulti([
      { lang: "bash", code:
`# Start a local model from the CLI
> foundry model run phi-4-mini

` },
      { lang: "csharp", code:
`// Manager owns the dynamic endpoint + key
var manager = await FoundryLocalManager
    .StartModelAsync("phi-4-mini");

var client = new OpenAIClient(
    new ApiKeyCredential(manager.ApiKey),
    new OpenAIClientOptions { Endpoint = manager.Endpoint });

var chat = client.GetChatClient("phi-4-mini");` },
    ]),
    {
      x: 1.1, y: 3.7, w: 7.1, h: 3.0,
      fontSize: 11, fontFace: F.mono, color: C.textOnDark,
      paraSpaceAfter: 0,
    }
  );

  s.addText("WATCH FOR", {
    x: 8.8, y: 3.5, w: 4, h: 0.3,
    fontSize: 12, bold: true, color: C.amber, charSpacing: 4, fontFace: F.body,
  });
  const watchItems = [
    ["🧠 Real reasoning", "Multi-billion-param model"],
    ["🔒 Stays on device", "No data egress, no API key"],
    ["🔌 OpenAI-compatible", "Same SDK, swap the URL"],
    ["💾 The tradeoff", "Disk + RAM + warm-up time"],
  ];
  watchItems.forEach(([h, sub], i) => {
    const y = 3.95 + i * 0.7;
    s.addText(h, {
      x: 8.8, y, w: 4, h: 0.3,
      fontSize: 14, bold: true, color: C.textOnDark, fontFace: F.body,
    });
    s.addText(sub, {
      x: 8.8, y: y + 0.3, w: 4, h: 0.3,
      fontSize: 12, color: C.textOnDarkMuted, italic: true, fontFace: F.body,
    });
  });

  addMotif(s, { color: C.amber });
}

// ═══════════════════════════════════════════════════════════════════════════
// SLIDE 10 — MICROSOFT AGENT FRAMEWORK (the unifier)
// ═══════════════════════════════════════════════════════════════════════════
{
  const s = pptx.addSlide();
  s.background = { color: C.bgLight };
  eyebrow(s, "The Unifier", C.indigoBright);
  title(s, "Microsoft Agent Framework");

  s.addText("One programming model. Three tiers. Swap the brain — keep the code.", {
    x: 0.6, y: 1.75, w: 12, h: 0.5,
    fontSize: 18, italic: true, color: C.textMuted, fontFace: F.body,
  });

  // Left: stacked provider boxes feeding into MAF
  const lx = 0.6, lw = 3.0;
  const tiers = [
    { label: "OS Local",   note: "Apple · Phi Silica · Gemini Nano", color: C.coral },
    { label: "BYO Local",  note: "Foundry Local · ONNX · custom",     color: C.amber },
    { label: "Cloud",      note: "Azure OpenAI · Bedrock · Vertex",   color: C.indigoBright },
  ];
  tiers.forEach((t, i) => {
    const y = 2.6 + i * 1.05;
    s.addShape("roundRect", {
      x: lx, y, w: lw, h: 0.9,
      fill: { color: C.cardLight }, line: { color: t.color, width: 2 },
      rectRadius: 0.08,
    });
    s.addShape("rect", {
      x: lx, y, w: 0.15, h: 0.9,
      fill: { color: t.color }, line: { color: t.color },
    });
    s.addText(t.label, {
      x: lx + 0.3, y: y + 0.1, w: lw - 0.4, h: 0.35,
      fontSize: 16, bold: true, color: C.textDark, fontFace: F.body,
    });
    s.addText(t.note, {
      x: lx + 0.3, y: y + 0.45, w: lw - 0.4, h: 0.35,
      fontSize: 11, italic: true, color: C.textMuted, fontFace: F.body,
    });
  });

  // Arrows to MAF block
  const ax = lx + lw + 0.05;
  tiers.forEach((_, i) => {
    const y = 2.6 + i * 1.05 + 0.45;
    s.addShape("rightTriangle", {
      x: ax, y: y - 0.18, w: 0.4, h: 0.36,
      fill: { color: C.borderLight }, line: { color: C.borderLight },
      rotate: 90,
    });
  });

  // MAF central block — bigger now that it owns the right side too
  const mx = ax + 0.55, mw = 5.5;
  s.addShape("roundRect", {
    x: mx, y: 2.5, w: mw, h: 3.35,
    fill: { color: C.indigoDeep }, line: { color: C.indigoDeep },
    rectRadius: 0.15,
  });
  s.addText("AIAgent", {
    x: mx, y: 2.8, w: mw, h: 0.7,
    fontSize: 40, bold: true, color: C.textOnDark, fontFace: F.header, align: "center",
  });
  s.addText("AgentThread  ·  Tools  ·  Workflows  ·  Streaming", {
    x: mx, y: 3.65, w: mw, h: 0.4,
    fontSize: 14, color: C.textOnDarkMuted, fontFace: F.body, align: "center",
  });
  // Divider
  s.addShape("line", {
    x: mx + mw * 0.2, y: 4.2, w: mw * 0.6, h: 0,
    line: { color: C.coral, width: 1 },
  });
  s.addText("ChatClientAgent  ·  WorkflowAgent\nFunctionInvokingChatClient", {
    x: mx, y: 4.35, w: mw, h: 0.9,
    fontSize: 13, color: C.textOnDarkMuted, fontFace: F.body, align: "center", paraSpaceAfter: 4,
  });
  s.addText("Microsoft.Agents.AI", {
    x: mx, y: 5.4, w: mw, h: 0.35,
    fontSize: 13, italic: true, color: C.coral, fontFace: F.mono, align: "center",
  });

  // Right side: capability list (replacing old code block)
  const rx = mx + mw + 0.4, rw = W - rx - 0.5;
  const pillars = [
    ["Same code", "Swap the IChatClient, keep your agent"],
    ["Stateful",  "AgentThread persists conversation"],
    ["Tool-aware","Source-generated AITools (no reflection)"],
    ["Streaming", "Token-by-token via IAsyncEnumerable"],
  ];
  pillars.forEach(([h, sub], i) => {
    const y = 2.55 + i * 0.78;
    // small coral dot
    s.addShape("ellipse", {
      x: rx, y: y + 0.08, w: 0.2, h: 0.2,
      fill: { color: C.coral }, line: { color: C.coral },
    });
    s.addText(h, {
      x: rx + 0.3, y, w: rw - 0.3, h: 0.35,
      fontSize: 15, bold: true, color: C.textDark, fontFace: F.body,
    });
    s.addText(sub, {
      x: rx + 0.3, y: y + 0.35, w: rw - 0.3, h: 0.4,
      fontSize: 11, italic: true, color: C.textMuted, fontFace: F.body,
    });
  });

  // Bottom callout
  s.addText("📐 The provider changes. The agent code does not.", {
    x: 0.6, y: 6.1, w: 12, h: 0.5,
    fontSize: 20, italic: true, bold: true, color: C.coral, fontFace: F.header, align: "center",
  });

  addMotif(s);
  addFooter(s, 10, TOTAL);
}

// ═══════════════════════════════════════════════════════════════════════════
// SLIDE 11 — MAF: HOW TO USE IT (code-first, syntax-highlighted)
// ═══════════════════════════════════════════════════════════════════════════
{
  const s = pptx.addSlide();
  s.background = { color: C.bgLight };
  eyebrow(s, "Five Lines of Code", C.indigoBright);
  title(s, "How to use it");

  s.addText("Take an IChatClient — any tier — and wrap it as an agent.", {
    x: 0.6, y: 1.75, w: 12, h: 0.5,
    fontSize: 17, italic: true, color: C.textMuted, fontFace: F.body,
  });

  // Big code block on the left (~ 60% of slide)
  const cx = 0.6, cw = 8.4;
  s.addShape("roundRect", {
    x: cx, y: 2.4, w: cw, h: 4.4,
    fill: { color: C.indigoMid }, line: { color: C.indigoBright, width: 1 },
    rectRadius: 0.12,
  });
  s.addText(
    highlightCode(
`// 1. Take any IChatClient — Apple, Foundry Local, Cloud…
IChatClient client = new AppleIntelligenceChatClient();

// 2. Wrap it as an agent (with optional tools)
AIAgent agent = client.CreateAIAgent(
    instructions: "You are a travel guide.",
    tools: [.. TravelToolContext.Default.Tools]);

// 3. Keep a thread for conversation state
AgentThread thread = agent.GetNewThread();

// 4. Stream the response, token by token
await foreach (var update in agent.RunStreamingAsync(prompt, thread))
{
    Console.Write(update.Text);
}`, "csharp"),
    {
      x: cx + 0.25, y: 2.55, w: cw - 0.5, h: 4.1,
      fontSize: 11, fontFace: F.mono, color: C.textOnDark,
      paraSpaceAfter: 0,
    }
  );

  // Right side: 4 numbered callout cards
  const nx = cx + cw + 0.25, nw = W - nx - 0.5;
  const notes = [
    ["1", "Pluggable",   "Any IChatClient — Apple, Foundry Local, Azure OpenAI",                 C.coral],
    ["2", "Tool-aware",  "[ExportAIFunction] generates AITool descriptors at compile time",       C.indigoBright],
    ["3", "Stateful",    "AgentThread carries history across runs — no manual list-juggling",    C.amber],
    ["4", "Streaming",   "RunStreamingAsync yields AgentRunResponseUpdate — wire to your UI",    C.coral],
  ];
  notes.forEach(([num, h, sub, color], i) => {
    const y = 2.4 + i * 1.1;
    s.addShape("roundRect", {
      x: nx, y, w: nw, h: 1.0,
      fill: { color: C.cardLight }, line: { color: C.borderLight, width: 1 },
      rectRadius: 0.08,
    });
    // number badge
    s.addShape("ellipse", {
      x: nx + 0.2, y: y + 0.2, w: 0.55, h: 0.55,
      fill: { color }, line: { color },
    });
    s.addText(num, {
      x: nx + 0.2, y: y + 0.2, w: 0.55, h: 0.55,
      fontSize: 18, bold: true, color: C.bgLight, align: "center", valign: "middle", fontFace: F.header,
    });
    // heading + sub
    s.addText(h, {
      x: nx + 0.9, y: y + 0.15, w: nw - 1.0, h: 0.35,
      fontSize: 15, bold: true, color: C.textDark, fontFace: F.body,
    });
    s.addText(sub, {
      x: nx + 0.9, y: y + 0.5, w: nw - 1.0, h: 0.45,
      fontSize: 10, italic: true, color: C.textMuted, fontFace: F.body,
    });
  });

  addMotif(s);
  addFooter(s, 11, TOTAL);
}

// ═══════════════════════════════════════════════════════════════════════════
// SLIDE 12 — DEMO 4 (MAF Workflow mixing local + cloud)
// ═══════════════════════════════════════════════════════════════════════════
{
  const s = pptx.addSlide();
  darkBg(s);

  // Two-color left bar (coral / indigo) — privacy-sensitive ↔ frontier brain
  s.addShape("rect", { x: 0, y: 0,   w: 0.4, h: H/2, fill: { color: C.coral },        line: { color: C.coral } });
  s.addShape("rect", { x: 0, y: H/2, w: 0.4, h: H/2, fill: { color: C.indigoBright }, line: { color: C.indigoBright } });

  s.addText("DEMO  04", {
    x: 0.9, y: 0.55, w: 6, h: 0.45,
    fontSize: 18, bold: true, color: C.textOnDarkMuted, charSpacing: 8, fontFace: F.body,
  });
  s.addText("A Workflow of Agents", {
    x: 0.9, y: 1.05, w: 12, h: 0.95,
    fontSize: 48, bold: true, color: C.textOnDark, fontFace: F.header,
  });
  s.addText("MAF workflows compose agents — local privacy on the bookends, cloud reasoning in the middle.", {
    x: 0.9, y: 2.05, w: 12, h: 0.45,
    fontSize: 16, italic: true, color: C.textOnDarkMuted, fontFace: F.body,
  });

  // ── Vertical flow diagram on the right ───────────────────────────────
  // User → 📱 triage (Apple) → ☁️ expert (Azure) → User
  const fx = W - 3.4, fw = 2.9;
  const flowY = 2.6;
  const pillH = 0.75;
  const arrowH = 0.45;

  const pills = [
    { label: "User input",  sub: "raw, may have PII",  color: C.textOnDarkMuted, glyph: "👤" },
    { label: "triage",      sub: "Apple Intelligence", color: C.coral,           glyph: "📱" },
    { label: "expert",      sub: "Azure OpenAI",       color: C.indigoBright,    glyph: "☁️" },
    { label: "User answer", sub: "sanitised reply",    color: C.textOnDarkMuted, glyph: "👤" },
  ];

  pills.forEach((p, i) => {
    const y = flowY + i * (pillH + arrowH);
    // pill
    s.addShape("roundRect", {
      x: fx, y, w: fw, h: pillH,
      fill: { color: C.indigoMid }, line: { color: p.color, width: 2 },
      rectRadius: 0.18,
    });
    // glyph on left of pill
    s.addText(p.glyph, {
      x: fx + 0.15, y, w: 0.6, h: pillH,
      fontSize: 22, align: "center", valign: "middle", fontFace: F.body,
    });
    // label + sub stacked vertically on the right
    s.addText(p.label, {
      x: fx + 0.85, y: y + 0.1, w: fw - 0.95, h: 0.35,
      fontSize: 16, bold: true, color: C.textOnDark, fontFace: F.body, valign: "middle",
    });
    s.addText(p.sub, {
      x: fx + 0.85, y: y + 0.4, w: fw - 0.95, h: 0.3,
      fontSize: 11, italic: true, color: C.textOnDarkMuted, fontFace: F.body, valign: "middle",
    });

    // arrow between pills
    if (i < pills.length - 1) {
      const ax = fx + fw / 2;
      const ay = y + pillH + 0.05;
      const arrowLineH = arrowH - 0.2;
      s.addShape("line", {
        x: ax, y: ay, w: 0, h: arrowLineH,
        line: { color: C.coral, width: 2 },
      });
      // arrowhead — downward-pointing triangle
      s.addShape("triangle", {
        x: ax - 0.1, y: ay + arrowLineH, w: 0.2, h: 0.18,
        fill: { color: C.coral }, line: { color: C.coral },
        flipV: true,
      });
    }
  });

  // ── Workflow code block, taller and to the left of the flow ──────────
  const cx = 0.9;
  const cw = fx - cx - 0.3;
  const cy = 2.6, ch = H - cy - 0.4;

  s.addShape("roundRect", {
    x: cx, y: cy, w: cw, h: ch,
    fill: { color: C.indigoMid }, line: { color: C.indigoBright, width: 1 },
    rectRadius: 0.1,
  });
  s.addText(
    highlightCode(
`// 📱  Local — Apple Intelligence, on the device
AIAgent triage = new AppleIntelligenceChatClient()
    .CreateAIAgent(
        "Strip personal info. Return only topic + intent.",
        name: "triage");

// ☁️  Cloud — Azure OpenAI, frontier reasoning
AIAgent expert = new AzureOpenAIClient(
        endpoint, new DefaultAzureCredential())
    .GetChatClient("gpt-4.1").AsIChatClient()
    .CreateAIAgent(
        "Answer with depth and current context.",
        name: "expert");

// 🔗  MAF workflow — pipe local → cloud, expose as an agent
var workflow = AgentWorkflowBuilder
    .BuildSequential("local-then-cloud", triage, expert)
    .AsAgent();

await foreach (var update in workflow.RunStreamingAsync(userInput))
    Console.Write(update.Text);`, "csharp"),
    {
      x: cx + 0.25, y: cy + 0.18, w: cw - 0.5, h: ch - 0.35,
      fontSize: 12, fontFace: F.mono, color: C.textOnDark, paraSpaceAfter: 0,
      valign: "top",
    }
  );
}

// ═══════════════════════════════════════════════════════════════════════════
// SLIDE 13 — BUILT WITH (maui-labs credits)
// ═══════════════════════════════════════════════════════════════════════════
{
  const s = pptx.addSlide();
  s.background = { color: C.bgLight };
  eyebrow(s, "Built With", C.coral);
  title(s, "Standing on the shoulders of maui-labs");

  s.addText(
    "Everything you saw runs on experimental packages from " +
    "github.com/dotnet/maui-labs — try them, file issues, send PRs.",
    {
      x: 0.6, y: 1.75, w: 12, h: 0.55,
      fontSize: 15, italic: true, color: C.textMuted, fontFace: F.body,
    }
  );

  const pkgs = [
    {
      name: "Microsoft.Maui.Essentials.AI",
      ver:  "11.0.0-preview.4",
      desc: "Wraps Apple Intelligence as a standard IChatClient — same code path as your cloud client. No key, no cloud, works offline.",
      color: C.coral,
    },
    {
      name: "Microsoft.Maui.AI.Attributes",
      ver:  "0.1.0-preview.10",
      desc: "Source generator: decorate a C# method with [ExportAIFunction] and it becomes a strongly-typed AI tool. AOT-safe, zero reflection.",
      color: C.indigoBright,
    },
    {
      name: "Phi Silica  ·  PR #178",
      ver:  "draft / in flight",
      desc: "Brings the same IChatClient wrapper to Windows Copilot+ PCs via Windows AI APIs. Adds Phi Silica as another local provider.",
      color: C.amber,
    },
    {
      name: "Microsoft.Maui.DevFlow.Agent",
      ver:  "0.1.0-preview.10",
      desc: "Playwright-for-MAUI + MCP server. AI agents (Copilot, Claude) can drive the live app — this entire app was built that way.",
      color: C.coral,
    },
  ];

  // 2×2 grid
  const cols = 2, rows = 2;
  const cardW = 6.05, cardH = 1.9, gx = 0.3, gy = 0.2;
  const startX = 0.6, startY = 2.45;

  pkgs.forEach((p, i) => {
    const cx = i % cols, cy = Math.floor(i / cols);
    const x = startX + cx * (cardW + gx);
    const y = startY + cy * (cardH + gy);

    s.addShape("roundRect", {
      x, y, w: cardW, h: cardH,
      fill: { color: C.cardLight }, line: { color: C.borderLight, width: 1 },
      rectRadius: 0.1,
    });
    // left color bar
    s.addShape("rect", {
      x, y, w: 0.15, h: cardH,
      fill: { color: p.color }, line: { color: p.color },
    });
    // package name
    s.addText(p.name, {
      x: x + 0.35, y: y + 0.2, w: cardW - 0.6, h: 0.45,
      fontSize: 16, bold: true, color: C.textDark, fontFace: F.mono,
    });
    // version
    s.addText(p.ver, {
      x: x + 0.35, y: y + 0.65, w: cardW - 0.6, h: 0.35,
      fontSize: 11, italic: true, color: p.color, fontFace: F.body,
    });
    // description
    s.addText(p.desc, {
      x: x + 0.35, y: y + 1.05, w: cardW - 0.6, h: cardH - 1.15,
      fontSize: 13, color: C.textDark, fontFace: F.body,
    });
  });

  // Bottom call-to-action
  s.addText("github.com/dotnet/maui-labs", {
    x: 0.6, y: 6.6, w: W - 1.2, h: 0.4,
    fontSize: 15, bold: true, color: C.coral, fontFace: F.mono, align: "center",
  });

  addMotif(s);
  addFooter(s, 13, TOTAL);
}

// ═══════════════════════════════════════════════════════════════════════════
// SLIDE 14 — RULES OF THUMB (full-width decision table)
// ═══════════════════════════════════════════════════════════════════════════
{
  const s = pptx.addSlide();
  s.background = { color: C.bgLight };
  eyebrow(s, "Rules of Thumb");
  title(s, "Your choice. Your code stays the same.");

  s.addText("There's no single right answer — pick the tier that fits the task, and let MAF handle the rest.", {
    x: 0.6, y: 1.85, w: 12, h: 0.45,
    fontSize: 16, italic: true, color: C.textMuted, fontFace: F.body,
  });

  // Two-column comparison: Stay Local  vs  Reach for Cloud
  const colY = 2.55, colH = 3.6, gap = 0.3;
  const colW = (W - 1.2 - gap) / 2;
  const leftX = 0.6, rightX = leftX + colW + gap;

  function addRulesCard(x, color, eyebrowTxt, headline, items) {
    s.addShape("roundRect", {
      x, y: colY, w: colW, h: colH,
      fill: { color: C.cardLight }, line: { color: C.borderLight, width: 1 },
      rectRadius: 0.12,
    });
    s.addShape("rect", {
      x, y: colY, w: colW, h: 0.18,
      fill: { color }, line: { color },
    });
    s.addText(eyebrowTxt, {
      x: x + 0.3, y: colY + 0.35, w: colW - 0.6, h: 0.35,
      fontSize: 12, bold: true, color, charSpacing: 4, fontFace: F.body,
    });
    s.addText(headline, {
      x: x + 0.3, y: colY + 0.7, w: colW - 0.6, h: 0.55,
      fontSize: 26, bold: true, color: C.textDark, fontFace: F.header,
    });
    items.forEach(([when, then], i) => {
      const y = colY + 1.4 + i * 0.55;
      // dot
      s.addShape("ellipse", {
        x: x + 0.35, y: y + 0.08, w: 0.18, h: 0.18,
        fill: { color }, line: { color },
      });
      s.addText([
        { text: when,  options: { color: C.textDark,  bold: true } },
        { text: "  ·  ", options: { color: C.borderLight } },
        { text: then,  options: { color: C.textMuted, italic: true } },
      ], {
        x: x + 0.65, y, w: colW - 0.85, h: 0.45,
        fontSize: 13, fontFace: F.body, valign: "middle",
      });
    });
  }

  addRulesCard(leftX, C.coral, "Stay Local", "Keep it on the device", [
    ["Privacy non-negotiable",   "OS Local · BYO Local"],
    ["Offline / poor network",   "OS Local · BYO Local"],
    ["Instant first token",      "OS Local"],
    ["Cost matters at scale",    "Local pre/post-roll"],
  ]);

  addRulesCard(rightX, C.indigoBright, "Reach for Cloud", "Use the frontier brain", [
    ["Need deep reasoning",      "Cloud"],
    ["Huge context · multimodal","Cloud"],
    ["Tools requiring web data", "Cloud"],
    ["Cloud capability, no egress", "BYO Local (Foundry Local)"],
  ]);

  // Bottom tagline
  s.addText("📐  Hand-off pattern:  local triage  →  cloud reasoning  →  local rendering", {
    x: 0.6, y: 6.4, w: 12, h: 0.45,
    fontSize: 16, italic: true, bold: true, color: C.coral, fontFace: F.header, align: "center",
  });

  addMotif(s);
  addFooter(s, 14, TOTAL);
}

// ═══════════════════════════════════════════════════════════════════════════
// SLIDE 15 — LINKS (where to go next)
// ═══════════════════════════════════════════════════════════════════════════
{
  const s = pptx.addSlide();
  s.background = { color: C.bgLight };
  eyebrow(s, "Resources");
  title(s, "Where to go next");

  s.addText("Start with these — the deck, the demos, and the docs that back every claim today.", {
    x: 0.6, y: 1.85, w: 12, h: 0.45,
    fontSize: 16, italic: true, color: C.textMuted, fontFace: F.body,
  });

  const links = [
    {
      label: "Today's deck + demo code",
      url:   "github.com/mattleibow/cptmsdug-local-cloud-agent-framework",
      desc:  "All slides, all four demos, ready to clone & run",
      color: C.coral, icon: "★",
    },
    {
      label: "Microsoft Agent Framework",
      url:   "github.com/microsoft/agent-framework",
      desc:  "AIAgent · AgentThread · workflows · DevUI",
      color: C.indigoBright, icon: "◆",
    },
    {
      label: "Microsoft.Extensions.AI",
      url:   "learn.microsoft.com/dotnet/ai/microsoft-extensions-ai",
      desc:  "The IChatClient abstraction MAF builds on",
      color: C.indigoBright, icon: "◆",
    },
    {
      label: "maui-labs (Essentials.AI · AI.Attributes · DevFlow)",
      url:   "github.com/dotnet/maui-labs",
      desc:  "Apple Intelligence wrapper, source-gen tools, AI-driven app debugging",
      color: C.coral, icon: "◆",
    },
    {
      label: "Foundry Local",
      url:   "github.com/microsoft/Foundry-Local",
      desc:  "Cloud-grade LLMs running on your laptop",
      color: C.amber, icon: "▣",
    },
    {
      label: "Apple FoundationModels  ·  Windows AI APIs",
      url:   "developer.apple.com/documentation/foundationmodels  ·  learn.microsoft.com/windows/ai/apis",
      desc:  "On-device LLM platforms — straight from the source",
      color: C.amber, icon: "▣",
    },
  ];

  // 2-column × 3-row grid
  const cols = 2, rows = 3;
  const cardW = (W - 1.2 - 0.3) / cols, cardH = 1.45;
  const startX = 0.6, startY = 2.5, gapX = 0.3, gapY = 0.15;

  links.forEach((l, i) => {
    const cx = i % cols, cy = Math.floor(i / cols);
    const x = startX + cx * (cardW + gapX);
    const y = startY + cy * (cardH + gapY);

    s.addShape("roundRect", {
      x, y, w: cardW, h: cardH,
      fill: { color: C.cardLight }, line: { color: C.borderLight, width: 1 },
      rectRadius: 0.1,
    });
    // left color bar
    s.addShape("rect", {
      x, y, w: 0.12, h: cardH,
      fill: { color: l.color }, line: { color: l.color },
    });
    // icon glyph
    s.addText(l.icon, {
      x: x + 0.25, y: y + 0.2, w: 0.5, h: 0.5,
      fontSize: 22, bold: true, color: l.color, fontFace: F.header,
      align: "center", valign: "middle",
    });
    // label
    s.addText(l.label, {
      x: x + 0.85, y: y + 0.15, w: cardW - 1.0, h: 0.4,
      fontSize: 14, bold: true, color: C.textDark, fontFace: F.body,
    });
    // url
    s.addText(l.url, {
      x: x + 0.85, y: y + 0.55, w: cardW - 1.0, h: 0.35,
      fontSize: 11, color: l.color, fontFace: F.mono,
    });
    // desc
    s.addText(l.desc, {
      x: x + 0.85, y: y + 0.95, w: cardW - 1.0, h: 0.45,
      fontSize: 11, italic: true, color: C.textMuted, fontFace: F.body,
    });
  });

  addMotif(s);
  addFooter(s, 15, TOTAL);
}

// ═══════════════════════════════════════════════════════════════════════════
// SLIDE 16 — THANK YOU + QUESTIONS  (dark closing slide)
// ═══════════════════════════════════════════════════════════════════════════
{
  const s = pptx.addSlide();
  darkBg(s);

  // Mirror the title slide motif: tri-color bar on right, large glyphs top-right
  s.addShape("rect", { x: W - 0.4, y: 0, w: 0.4, h: H, fill: { color: C.coral }, line: { color: C.coral } });

  // Big motif top-right (echoes title slide)
  s.addText("☁", {
    x: 0.9, y: 0.55, w: 1.2, h: 1.25,
    fontSize: 64, align: "center", valign: "middle", color: C.indigoBright, fontFace: F.body,
  });
  s.addText("↔", {
    x: 1.7, y: 0.8, w: 1.0, h: 1.0,
    fontSize: 50, align: "center", valign: "middle", color: C.textOnDarkMuted, bold: true, fontFace: F.body,
  });
  s.addText("📱", {
    x: 2.5, y: 0.6, w: 1.2, h: 1.2,
    fontSize: 60, align: "center", valign: "middle", color: C.coral, fontFace: F.body,
  });

  // Big "Thank you"
  s.addText("Thank you.", {
    x: 0.9, y: 2.6, w: 12, h: 1.4,
    fontSize: 110, bold: true, color: C.textOnDark, fontFace: F.header,
  });

  // Questions?
  s.addText("Questions?", {
    x: 0.9, y: 4.1, w: 12, h: 0.9,
    fontSize: 54, italic: true, color: C.coral, fontFace: F.header,
  });

  // Speaker line + repo at bottom
  s.addText([
    { text: "Matthew Leibowitz",                    options: { bold: true, color: C.textOnDark } },
    { text: "   ·   Software Engineer @ Microsoft   ·   ", options: { color: C.textOnDarkMuted } },
    { text: "@mattleibow",                          options: { bold: true, color: C.amber } },
  ], {
    x: 0.9, y: H - 1.4, w: 12, h: 0.4,
    fontSize: 16, fontFace: F.body,
  });
  s.addText("github.com/mattleibow/cptmsdug-local-cloud-agent-framework", {
    x: 0.9, y: H - 0.9, w: 12, h: 0.4,
    fontSize: 13, italic: true, color: C.coral, fontFace: F.mono,
  });
}

// ═══════════════════════════════════════════════════════════════════════════

pptx.writeFile({ fileName: "Unifying-Local-Cloud-AI.pptx" })
  .then(name => console.log("✅ Wrote " + name));
