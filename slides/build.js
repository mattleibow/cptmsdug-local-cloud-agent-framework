// Unifying Local and Cloud AI with Microsoft Agent Framework
// Deck for Cape Town MS Developer User Group (@CPTMSDUG)
// Speaker: Matthew Leibowitz (@mattleibow)
//
// Palette: Indigo (cloud night) + Coral (device glow) + Amber accent
// Motif: ☁ ↔ 📱 — device-to-cloud arrow as a recurring visual element
// Sandwich: dark title + demo + closing slides; light content slides

const PptxGenJS = require("pptxgenjs");

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

const TOTAL = 13;

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
`var client = new AzureOpenAIClient(
    new Uri(endpoint),
    new DefaultAzureCredential());

var chat = client.GetChatClient("gpt-4.1");

var response = await chat.CompleteChatAsync(
    "Summarise the agenda for today.");

Console.WriteLine(response.Value.Content[0].Text);`,
    {
      x: 1.1, y: 3.7, w: 7.1, h: 3.0,
      fontSize: 14, fontFace: F.mono, color: C.textOnDark,
      paraSpaceAfter: 2,
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
      status: "🔬  .NET wrapper in scope",
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
      { text: "  →  one IChatClient shape, Apple today · Phi Silica next · Gemini in scope", options: { color: C.textOnDarkMuted, italic: true } },
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
`using Microsoft.Maui.Essentials.AI;
using Microsoft.Extensions.AI;

IChatClient chat = new AppleIntelligenceChatClient();

var response = await chat.GetResponseAsync(
    "Summarise the agenda for today.");

Console.WriteLine(response.Text);

// ✈️  Works in airplane mode
// 🆓  No API key, no per-token cost
// 🔒  Data never leaves the device`,
    {
      x: 1.1, y: 3.7, w: 7.1, h: 3.0,
      fontSize: 13, fontFace: F.mono, color: C.textOnDark,
      paraSpaceAfter: 2,
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
`# Start a local model from the CLI
> foundry model run phi-4-mini

// Manager owns the dynamic endpoint + key
var manager = await FoundryLocalManager
    .StartModelAsync("phi-4-mini");

var client = new OpenAIClient(
    new ApiKeyCredential(manager.ApiKey),
    new OpenAIClientOptions { Endpoint = manager.Endpoint });

var chat = client.GetChatClient("phi-4-mini");`,
    {
      x: 1.1, y: 3.7, w: 7.1, h: 3.0,
      fontSize: 12, fontFace: F.mono, color: C.textOnDark,
      paraSpaceAfter: 2,
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
  const lx = 0.6, lw = 3.5;
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

  // MAF central block
  const mx = ax + 0.55, mw = 3.6;
  s.addShape("roundRect", {
    x: mx, y: 2.6, w: mw, h: 3.15,
    fill: { color: C.indigoDeep }, line: { color: C.indigoDeep },
    rectRadius: 0.15,
  });
  s.addText("AIAgent", {
    x: mx, y: 2.9, w: mw, h: 0.6,
    fontSize: 26, bold: true, color: C.textOnDark, fontFace: F.header, align: "center",
  });
  s.addText("AgentThread · Tools\nWorkflows · Streaming\nProviders", {
    x: mx, y: 3.7, w: mw, h: 1.6,
    fontSize: 14, color: C.textOnDarkMuted, fontFace: F.body, align: "center", paraSpaceAfter: 4,
  });
  s.addText("Microsoft.Agents.AI", {
    x: mx, y: 5.2, w: mw, h: 0.4,
    fontSize: 12, italic: true, color: C.coral, fontFace: F.mono, align: "center",
  });

  // Right: code snippet
  const rx = mx + mw + 0.3, rw = W - rx - 0.5;
  s.addShape("roundRect", {
    x: rx, y: 2.6, w: rw, h: 3.15,
    fill: { color: C.indigoMid }, line: { color: C.indigoBright, width: 1 },
    rectRadius: 0.1,
  });
  s.addText(
`AIAgent agent = chatClient
    .CreateAIAgent(
        instructions: "You are a helpful assistant.",
        name: "assistant");

await foreach (var update in
    agent.RunStreamingAsync(prompt))
{
    Console.Write(update.Text);
}`,
    {
      x: rx + 0.2, y: 2.75, w: rw - 0.4, h: 2.85,
      fontSize: 12, fontFace: F.mono, color: C.textOnDark,
    }
  );

  // Bottom callout
  s.addText("📐 The provider changes. The agent code does not.", {
    x: 0.6, y: 6.0, w: 12, h: 0.5,
    fontSize: 18, italic: true, bold: true, color: C.coral, fontFace: F.header, align: "center",
  });

  addMotif(s);
  addFooter(s, 10, TOTAL);
}

// ═══════════════════════════════════════════════════════════════════════════
// SLIDE 11 — DEMO 4 (MAF mixing it all)
// ═══════════════════════════════════════════════════════════════════════════
{
  const s = pptx.addSlide();
  darkBg(s);

  // Tri-color left bar (coral / amber / indigo)
  s.addShape("rect", { x: 0, y: 0,         w: 0.4, h: H/3, fill: { color: C.coral }, line: { color: C.coral } });
  s.addShape("rect", { x: 0, y: H/3,       w: 0.4, h: H/3, fill: { color: C.amber }, line: { color: C.amber } });
  s.addShape("rect", { x: 0, y: 2*H/3,     w: 0.4, h: H/3, fill: { color: C.indigoBright }, line: { color: C.indigoBright } });

  s.addText("DEMO  04", {
    x: 0.9, y: 0.7, w: 6, h: 0.6,
    fontSize: 18, bold: true, color: C.textOnDarkMuted, charSpacing: 8, fontFace: F.body,
  });
  s.addText("Mixing It All", {
    x: 0.9, y: 1.4, w: 12, h: 1.3,
    fontSize: 60, bold: true, color: C.textOnDark, fontFace: F.header,
  });
  s.addText("One MAUI app. Three AI tiers. Same AIAgent code path.", {
    x: 0.9, y: 2.7, w: 12, h: 0.5,
    fontSize: 18, italic: true, color: C.textOnDarkMuted, fontFace: F.body,
  });

  // Live wire-up: three columns showing what each provider line looks like
  const items = [
    {
      tier: "OS Local",
      code: `chatClient = new AppleIntelligenceChatClient();\n//  Microsoft.Maui.Essentials.AI`,
      color: C.coral,
    },
    {
      tier: "BYO Local",
      code: `chatClient = new OpenAIClient(\n    new ApiKeyCredential("not-used"),\n    new OpenAIClientOptions {\n      Endpoint = manager.Endpoint\n    })\n    .GetChatClient("phi-4-mini")\n    .AsIChatClient();`,
      color: C.amber,
    },
    {
      tier: "Cloud",
      code: `chatClient = new AzureOpenAIClient(\n    endpoint, new DefaultAzureCredential())\n    .GetChatClient("gpt-4.1")\n    .AsIChatClient();`,
      color: C.indigoBright,
    },
  ];
  const cw = 4.0, gap = 0.15, sx = 0.9;
  items.forEach((it, i) => {
    const x = sx + i * (cw + gap);
    s.addShape("roundRect", {
      x, y: 3.5, w: cw, h: 2.4,
      fill: { color: C.indigoMid }, line: { color: it.color, width: 2 },
      rectRadius: 0.1,
    });
    s.addText(it.tier.toUpperCase(), {
      x: x + 0.2, y: 3.65, w: cw - 0.4, h: 0.35,
      fontSize: 12, bold: true, color: it.color, charSpacing: 3, fontFace: F.body,
    });
    s.addText(it.code, {
      x: x + 0.2, y: 4.05, w: cw - 0.4, h: 1.8,
      fontSize: 11, fontFace: F.mono, color: C.textOnDark, paraSpaceAfter: 2,
    });
  });

  // Bottom unified line
  s.addShape("roundRect", {
    x: 0.9, y: 6.1, w: W - 1.8, h: 0.9,
    fill: { color: C.indigoDeep }, line: { color: C.coral, width: 1 },
    rectRadius: 0.1,
  });
  s.addText(
    [
      { text: `var agent = chatClient.CreateAIAgent("You are helpful.");`, options: { color: C.textOnDark } },
      { text: `   ←  identical for every tier`, options: { color: C.coral, italic: true } },
    ],
    {
      x: 0.9, y: 6.1, w: W - 1.8, h: 0.9,
      fontSize: 15, fontFace: F.mono, align: "center", valign: "middle",
    }
  );
}

// ═══════════════════════════════════════════════════════════════════════════
// SLIDE 12 — BUILT WITH (maui-labs credits)
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
  addFooter(s, 12, TOTAL);
}

// ═══════════════════════════════════════════════════════════════════════════
// SLIDE 13 — RULES OF THUMB + RESOURCES (closing, dark)
// ═══════════════════════════════════════════════════════════════════════════
{
  const s = pptx.addSlide();
  darkBg(s);

  s.addText("RULES OF THUMB", {
    x: 0.6, y: 0.55, w: 8, h: 0.4,
    fontSize: 14, bold: true, color: C.coral, charSpacing: 6, fontFace: F.body,
  });
  s.addText("Your choice. Your code stays the same.", {
    x: 0.6, y: 1.0, w: 12, h: 0.9,
    fontSize: 40, bold: true, color: C.textOnDark, fontFace: F.header,
  });

  // Decision table
  const rows = [
    ["Privacy is non-negotiable",         "OS Local · BYO Local",        C.coral],
    ["Offline / poor connectivity",       "OS Local · BYO Local",        C.coral],
    ["Instant first token matters",       "OS Local",                    C.coral],
    ["Need frontier reasoning / tools",   "Cloud",                       C.indigoBright],
    ["Huge context / multimodal",         "Cloud",                       C.indigoBright],
    ["Cloud capability, no data egress",  "BYO Local (Foundry Local)",   C.amber],
    ["Cost matters at scale",             "Local — pre-roll, post-roll", C.amber],
  ];

  const tx = 0.6, ty = 2.3, tw = 8.0;
  // header
  s.addShape("rect", {
    x: tx, y: ty, w: tw, h: 0.5,
    fill: { color: C.indigoMid }, line: { color: C.indigoMid },
  });
  s.addText("WHEN…", {
    x: tx + 0.2, y: ty, w: 4.5, h: 0.5,
    fontSize: 11, bold: true, color: C.textOnDarkMuted, charSpacing: 3, valign: "middle", fontFace: F.body,
  });
  s.addText("REACH FOR", {
    x: tx + 4.5, y: ty, w: tw - 4.7, h: 0.5,
    fontSize: 11, bold: true, color: C.textOnDarkMuted, charSpacing: 3, valign: "middle", fontFace: F.body,
  });

  rows.forEach(([when, pick, color], i) => {
    const y = ty + 0.55 + i * 0.55;
    if (i % 2 === 0) {
      s.addShape("rect", {
        x: tx, y, w: tw, h: 0.55,
        fill: { color: C.indigoMid }, line: { color: C.indigoMid },
      });
    }
    s.addText(when, {
      x: tx + 0.2, y, w: 4.3, h: 0.55,
      fontSize: 13, color: C.textOnDark, valign: "middle", fontFace: F.body,
    });
    s.addShape("ellipse", {
      x: tx + 4.5, y: y + 0.16, w: 0.22, h: 0.22,
      fill: { color }, line: { color },
    });
    s.addText(pick, {
      x: tx + 4.8, y, w: tw - 5.0, h: 0.55,
      fontSize: 13, bold: true, color: C.textOnDark, valign: "middle", fontFace: F.body,
    });
  });

  // Resources card (right)
  const rx = 9.0, rw = W - rx - 0.5;
  s.addShape("roundRect", {
    x: rx, y: 2.3, w: rw, h: 4.4,
    fill: { color: C.coral }, line: { color: C.coral },
    rectRadius: 0.12,
  });
  s.addText("THANK YOU", {
    x: rx + 0.3, y: 2.5, w: rw - 0.6, h: 0.35,
    fontSize: 12, bold: true, color: C.indigoDeep, charSpacing: 4, fontFace: F.body,
  });
  s.addText("Find me", {
    x: rx + 0.3, y: 2.95, w: rw - 0.6, h: 0.55,
    fontSize: 28, bold: true, color: C.indigoDeep, fontFace: F.header,
  });
  s.addText([
    { text: "@mattleibow\n", options: { bold: true, fontSize: 18, color: C.indigoDeep } },
    { text: "GitHub · X · LinkedIn\n\n", options: { fontSize: 12, italic: true, color: C.indigoDeep } },
    { text: "Slides + demo code\n", options: { bold: true, fontSize: 14, color: C.indigoDeep } },
    { text: "github.com/mattleibow\n", options: { fontSize: 13, italic: true, fontFace: F.mono, color: C.indigoDeep } },
    { text: "  /cptmsdug-local-cloud-\n  agent-framework", options: { fontSize: 11, italic: true, fontFace: F.mono, color: C.indigoDeep } },
  ], {
    x: rx + 0.3, y: 3.6, w: rw - 0.6, h: 3.0,
    fontFace: F.body, paraSpaceAfter: 2,
  });

  // Q&A
  s.addText("Questions?", {
    x: 0.6, y: 6.6, w: 8, h: 0.5,
    fontSize: 22, italic: true, color: C.coral, fontFace: F.header,
  });
}

// ═══════════════════════════════════════════════════════════════════════════

pptx.writeFile({ fileName: "Unifying-Local-Cloud-AI.pptx" })
  .then(name => console.log("✅ Wrote " + name));
