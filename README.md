# Unifying Local and Cloud AI with the Microsoft Agent Framework

> 📱 ↔ ☁️ &nbsp; A working .NET MAUI app combining on-device Apple Intelligence with
> cloud Azure OpenAI through the [Microsoft Agent Framework][maf]. Demo code +
> slides for the [Cape Town MS Developer User Group][cptmsdug] talk by
> [Matthew Leibowitz][mattleibow].

---

## Why this repo exists

Every AI deployment trades off three things — **privacy**, **latency**, and
**capability** — and no single tier wins all three. So the interesting apps
live in the seams: a small model on the device for the things that must stay
private and instant, a frontier model in the cloud for the deep reasoning, and
glue to route work between them.

This repo is the demo for a talk that walks through exactly that — five
runnable MAF orchestrations, all inside one MAUI app, all built on the same
`IChatClient` abstraction whether the work runs locally or in the cloud.

> _"The interesting apps live in the seams."_

---

## What's in the box

```
demos/Demo.MauiAgentApp/         ← one MAUI app, multi-TFM
  Orchestrations/                  ← all agents and workflows
    Sequential/                      newsdesk: reporter → factchecker → editor
    Concurrent/                      travel: food + culture + logistics fan-out
    Handoff/                         help-desk: dispatcher → specialist
    GroupChat/                       startup pitch: founder + investor + advisor
    SequentialHybrid/  ⭐           local + cloud meeting-invite — the marquee demo
    StandaloneAgents.cs              storyteller + code-mentor
    TelemetryExtensions.cs           single-call AddDemoTelemetry → Aspire Dashboard
src/Microsoft.Maui.AI.Agents.DevUI/  reusable native DevUI control library
slides/                              pptxgenjs source for the deck (build.js)
```

Target frameworks: `net10.0-{android,ios,maccatalyst}` everywhere, plus
`net10.0-windows10.0.19041.0` on Windows. The marquee Apple Intelligence path
needs **macCatalyst 26+ on Apple silicon** (or iOS 26+ on Apple silicon).

---

## The marquee demo — local + cloud meeting invite

A 7-stage pipeline that drafts a meeting invite from a customer email. Each
agent sees only what it needs:

```
USER prompt
  │
  ▼
1. local-inbox-search          local agent      RAG over a fabricated inbox → PickedEmail JSON
2. local-picker-to-state       local executor   save PickedEmail to workflow state; forward body only
3. local-issue-summariser      local agent      2-3 sentence brief; strips addresses/phones/IDs/passwords
4. local-summary-to-cloud      local executor   one-way valve: forward only the summary
5. cloud-invite-drafter        cloud agent      Azure OpenAI · calls back to device via get_calendar tool
6. local-invite-finaliser      local executor   read PickedEmail from state; build envelope (subject/from/to/mailto)
7. local-output-messages       terminal
```

**Privacy contract.** The cloud sees the 2-3 sentence summary and an opaque
view of the user's calendar (working hours + busy ranges, no event titles).
The cloud never sees the customer's email body, address, phone, password,
card or SSN, the user's actual calendar events, the rest of the inbox, or
the original user prompt.

**The headline moment.** The cloud agent calls a local tool (`get_calendar`)
that runs back on the device. The cloud has a typed `DateOnly` parameter,
gets back working hours and opaque busy ranges, and walks forward one day at
a time if today is fully booked. The user's calendar never leaves the device.

Every stage is wired into the [Aspire Dashboard][aspire-dashboard] so you can
see the prompt, response, and tool calls live during the demo. See
[`demos/Demo.MauiAgentApp/Orchestrations/README.md`](demos/Demo.MauiAgentApp/Orchestrations/README.md)
for the full per-workflow tour with try-prompts.

---

## Quick start

### Prerequisites

- [.NET 10 SDK][dotnet10] (pinned in [`global.json`](global.json))
- macCatalyst path: Xcode 26+ on Apple silicon
- iOS path: iOS 26+ on Apple silicon
- An Azure OpenAI deployment (`gpt-4.1` works great). Put the endpoint, key
  and deployment name in user secrets under ID `ai-attributes-secrets`:
  ```bash
  dotnet user-secrets init --id ai-attributes-secrets
  dotnet user-secrets set "AzureOpenAI:Endpoint"       "https://your-resource.openai.azure.com" --id ai-attributes-secrets
  dotnet user-secrets set "AzureOpenAI:Key"            "your-key" --id ai-attributes-secrets
  dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-4.1"  --id ai-attributes-secrets
  ```
- Optional, recommended for the demo experience:
  - [Aspire CLI][aspire-cli] for the dashboard
  - [MAUI DevFlow][devflow] for AI-driven app inspection

### Run it

```bash
# 1. Start the standalone Aspire Dashboard (HTTPS — Apple's CFNetwork won't
#    do HTTP/2 cleartext, so plain http://localhost:4317 silently 400s).
aspire dashboard run --allow-anonymous \
  --frontend-url   https://localhost:18888 \
  --otlp-grpc-url  https://localhost:4317 \
  --otlp-http-url  https://localhost:4318 &

# 2. Build and launch on macCatalyst
cd demos/Demo.MauiAgentApp
dotnet build -f net10.0-maccatalyst -warnaserror
open "bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/Agent DevUI.app"

# 3. Watch traces + structured logs (including prompts) in the dashboard
open https://localhost:18888
```

Pick `local-cloud-meeting-invite` from the agent picker, send the prompt
`Draft a meeting invite to resolve the latest customer issue`, and watch the
7-node graph light up.

---

## Rules of thumb

| Stay local — keep it on the device | Reach for the cloud — frontier brain |
| --- | --- |
| Privacy non-negotiable | Need deep reasoning |
| Offline / poor network | Huge context · multimodal |
| Instant first token | Tools requiring web data |
| Cost matters at scale (local pre/post-roll) | Cloud capability without egress → BYO Local (Foundry Local) |

The hand-off pattern: **local triage → cloud reasoning → local rendering.**

---

## Built with

Everything in this app rides on the experimental packages from
[`dotnet/maui-labs`][maui-labs] — try them, file issues, send PRs.

| Package | What it does |
| --- | --- |
| [`Microsoft.Maui.Essentials.AI`][essentials-ai] | Wraps Apple Intelligence (and Phi Silica via [PR #178][phi-silica-pr]) as a standard `IChatClient`. Same code path as your cloud client — no key, no cloud, works offline. |
| [`Microsoft.Maui.AI.Attributes`][ai-attrs] | Source generator: decorate a C# method with `[ExportAIFunction]` and it becomes a strongly-typed AI tool. AOT-safe, zero reflection. |
| [`Microsoft.Maui.DevFlow.Agent`][devflow] | Playwright-for-MAUI + MCP server. AI agents (Copilot, Claude) can drive the live app — this entire app was built that way. |
| [`Microsoft.Agents.AI`][maf] | The Microsoft Agent Framework itself — `AIAgent`, workflows, DevUI. |

The `IChatClient` abstraction underneath everything is
[`Microsoft.Extensions.AI`][ext-ai] — that's what makes "swap the local
client for a cloud client" a one-line change.

---

## Where to go next

| | Link |
| :-- | :-- |
| ⭐ Today's deck + demo code | [github.com/mattleibow/cptmsdug-local-cloud-agent-framework][this-repo] |
| ◆ Microsoft Agent Framework | [github.com/microsoft/agent-framework][maf-repo] |
| ◆ Microsoft.Extensions.AI | [learn.microsoft.com/dotnet/ai/microsoft-extensions-ai][ext-ai] |
| ◆ maui-labs (Essentials.AI · AI.Attributes · DevFlow) | [github.com/dotnet/maui-labs][maui-labs] |
| ▣ Foundry Local | [github.com/microsoft/Foundry-Local][foundry-local] |
| ▣ Apple FoundationModels | [developer.apple.com/documentation/foundationmodels][apple-fm] |
| ▣ Windows AI APIs | [learn.microsoft.com/windows/ai/apis][windows-ai] |

---

## Speaker

[Matthew Leibowitz][mattleibow] · Software Engineer @ Microsoft ·
[@mattleibow](https://github.com/mattleibow)

Originally presented at the [Cape Town MS Developer User Group][cptmsdug].
Slides are under [`slides/`](slides/) — built from `slides/build.js` with
[pptxgenjs](https://gitbrent.github.io/PptxGenJS/) (`cd slides && npm install && node build.js`).

---

## License

Sample code for talks. Use it, fork it, share it — but please check the
licenses of the underlying packages ([maui-labs][maui-labs] is experimental
and moves fast) before shipping anything based on it.


<!-- Link references -->
[maf]:           https://github.com/microsoft/agent-framework
[maf-repo]:      https://github.com/microsoft/agent-framework
[ext-ai]:        https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai
[essentials-ai]: https://github.com/dotnet/maui-labs
[ai-attrs]:      https://github.com/dotnet/maui-labs
[devflow]:       https://github.com/dotnet/maui-labs
[phi-silica-pr]: https://github.com/dotnet/maui-labs/pull/178
[maui-labs]:     https://github.com/dotnet/maui-labs
[foundry-local]: https://github.com/microsoft/Foundry-Local
[apple-fm]:      https://developer.apple.com/documentation/foundationmodels
[windows-ai]:    https://learn.microsoft.com/windows/ai/apis
[dotnet10]:      https://dotnet.microsoft.com/download/dotnet/10.0
[aspire-cli]:    https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling
[aspire-dashboard]: https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/standalone
[mattleibow]:    https://github.com/mattleibow
[cptmsdug]:      https://www.meetup.com/cptmsdug/
[this-repo]:     https://github.com/mattleibow/cptmsdug-local-cloud-agent-framework
