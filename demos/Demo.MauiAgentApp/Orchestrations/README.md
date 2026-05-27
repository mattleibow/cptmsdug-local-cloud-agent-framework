# Orchestrations

Agents, workflows, and shared tools used by `Demo.MauiAgentApp`. Each
workflow is self-contained — extension method, tool definitions, and
tool context all in the same folder:

```
Orchestrations/
├─ Sequential/Workflow.cs         (NewsDesk: reporter → factchecker → editor)
├─ Concurrent/Workflow.cs         (Travel: food + culture + logistics fan-out)
├─ Handoff/Workflow.cs            (Help Desk: dispatcher → specialist)
├─ GroupChat/Workflow.cs          (Startup pitch: founder + investor + advisor)
├─ SequentialHybrid/              (local + cloud meeting-invite pipeline ⭐)
│  ├─ Workflow.cs
│  ├─ 1_LocalInboxSearchAgent.cs            agent     — local RAG, picks email as JSON
│  ├─ 2_LocalPickerToStateExecutor.cs       executor  — parse JSON, save to workflow state, forward body
│  ├─ 3_LocalIssueSummariserAgent.cs        agent     — summarise body, strip PII
│  ├─ 4_LocalSummaryToCloudExecutor.cs      executor  — one-way valve: forward summary only
│  ├─ 5_CloudInviteDrafterAgent.cs          agent     — Azure OpenAI, writes body, calls get_calendar tool
│  ├─ 6_LocalInviteFinaliserExecutor.cs     executor  — read picked email from state, build envelope
│  ├─ 7_LocalOutputMessagesExecutor.cs      executor  — terminal
│  ├─ LocalCalendarTool.cs                  tool      — exposed to cloud agent, runs on-device
│  ├─ Models/PickedEmail.cs
│  └─ Services/{InboxService,CalendarService}.cs
├─ StandaloneAgents.cs            (storyteller, code-mentor — single-agent)
├─ AIModels.cs                    (DI keys: "local-model", "cloud-model")
├─ WorkflowExtensions.cs          (shared helpers)
└─ TelemetryExtensions.cs         (AddDemoTelemetry → Aspire Dashboard)
```

---

## Standalone agents

Single-agent chats — useful warmups before the multi-agent demos.

| Agent | Description | Tools |
| --- | --- | --- |
| `storyteller` | Creative storyteller; vivid language, surprising twists | none |
| `code-mentor` | Friendly coding mentor; concepts, debug help, best practices | none |

**Try:**
- `Tell me a story about a robot who learns to paint`
- `Tell me a story about a lighthouse keeper and a comet`
- `Explain async/await in C# with a simple example`
- `Why does my for-loop variable behave weirdly inside a lambda?`

---

## Sequential — Newsdesk

**Demos:** a fixed pipeline where each agent passes its output to the next.

```
USER ─▶ reporter ─▶ factchecker ─▶ editor ─▶ Final output
```

| Agent | Tools | Role |
| --- | --- | --- |
| `sequential-newsdesk-reporter` | `search_news` | Calls the search tool, writes a 200-word article using stats and quotes from the results verbatim |
| `sequential-newsdesk-factchecker` | `verify_fact` | Pulls 3+ claims out of the article, verifies each one, writes a fact-check report |
| `sequential-newsdesk-editor` | none | Rewrites the article to drop/correct unverified or disputed claims |

**Tool design note** — `search_news` is intentionally mischievous: it returns
**one real, one plausible-but-invented, and one outrageously fake** headline
per query so the fact-checker has a meaningful job. `verify_fact` is a
wire-service style judge that returns one of: `VERIFIED`, `PARTIALLY
VERIFIED`, `UNVERIFIED`, `DISPUTED`.

**Try:**
- `Write a short news article about quantum computing breakthroughs`
- `Cover the latest in AI safety regulation`
- `Write about a breakthrough in cancer immunotherapy`
- `Cover SpaceX's most recent Starship launch`

**What you'll see:**
- **Reporter bubble** — article with `## :news: <headline>`, dateline,
  three paragraphs interleaving real + invented + outrageous facts.
- **Factchecker bubble** — bulleted report tagging each claim with a verdict
  glyph (`:check:`, `:warning:`, `:fail:`, `:error:`).
- **Editor bubble** — final polished article with the bad claims removed.
- **Final output** — duplicates the editor's article (workflow's last
  assistant message).

---

## Concurrent — Travel Planner

**Demos:** fan-out / fan-in. Three specialists run in parallel on the same
input; a custom aggregator stitches their Markdown sections into one trip
plan.

```
                  ┌─▶ food      ─┐
USER ─▶ Start ────┼─▶ culture   ─┼─▶ ConcurrentEnd ─▶ Final output
                  └─▶ logistics ─┘
                     (parallel)
```

| Agent | Tools | Role |
| --- | --- | --- |
| `concurrent-travel-food` | `search_restaurants` | Restaurants + iconic local dishes, prices, booking tips |
| `concurrent-travel-culture` | _none_ | Museums, historical sites, neighbourhoods to wander |
| `concurrent-travel-logistics` | `check_transport`, `check_accommodation` | Where to stay, getting around, budget |

The aggregator delegate is plain string concat under a `# :map: Trip Plan`
heading — see `ConcurrentWorkflow.cs` to swap it for an AI synthesizer if
you want a richer "Final output".

**Try:**
- `Plan a 3 day trip to Cape Town`
- `5 days in Tokyo for a food-loving couple`
- `Weekend getaway to Barcelona on a mid-range budget`
- `4 nights in Reykjavik in winter`
- `Family-friendly 3 days in Edinburgh`

**What you'll see:**
- **Three specialist bubbles** stream in roughly simultaneously, each a
  self-contained Markdown section.
- **Final output bubble** = `# :map: Trip Plan` + all three sections
  stitched together.

---

## Handoff — IT Help Desk

**Demos:** routing. A dispatcher acknowledges the user then invokes a
`handoff_to_<agent>` function to route to the right specialist.

```
USER ─▶ dispatcher ─┬─▶ network   ─┐
                    ├─▶ software  ─┤─▶ Final output
                    └─▶ hardware  ─┘
```

| Agent | Tools | Role |
| --- | --- | --- |
| `handoff-helpdesk-dispatcher` | `handoff_to_*` (auto) | Acknowledges + routes |
| `handoff-helpdesk-network` | `search_knowledge_base`, `check_system_status`, `create_ticket` | VPN, Wi-Fi, DNS, firewall |
| `handoff-helpdesk-software` | `search_knowledge_base`, `create_ticket` | App crashes, installs, licensing |
| `handoff-helpdesk-hardware` | `search_knowledge_base`, `create_ticket` | Laptops, monitors, peripherals |

**Try (different specialists hit different prompts):**

| Prompt | Routes to |
| --- | --- |
| `My VPN keeps disconnecting every 10 minutes` | network |
| `Wi-Fi at the office is super slow on my floor only` | network |
| `Excel crashes when I open files larger than 50MB` | software |
| `Visual Studio won't activate my license after the update` | software |
| `My external monitor flickers when I undock and redock` | hardware |
| `The keyboard on my laptop stopped working after a coffee spill` | hardware |

**What you'll see:**
- **Dispatcher bubble** — one-line ack, e.g. `:transport: **Dispatcher:**
  I'll connect you with our network specialist for this VPN issue.`
- **Specialist bubble** — diagnosis + steps + ticket status, with the
  matching section heading (`:network:`, `:bug:`, `:wrench:`).
- **Final output** = the specialist's response.

---

## Group chat — Startup pitch

**Demos:** round-robin multi-agent conversation (3 rounds × 3 agents = 9
iterations). Each participant builds on what the previous one just said.

```
                       ┌──▶ founder  ──┐
USER ─▶ GroupChatHost  ◀─▶ investor  ◀─▶  (3 rounds)  ─▶ Final output
                       └──▶ advisor  ──┘
```

| Agent | Tools | Role |
| --- | --- | --- |
| `groupchat-startup-founder` | `lookup_market_data` | Pitches, defends, refines |
| `groupchat-startup-investor` | `estimate_unit_economics`, `search_competitors` | Tough questions, red flags |
| `groupchat-startup-advisor` | `lookup_market_data` | Mediates, suggests pivots |

**Try:**
- `Pitch a Gen Z personal finance app that uses on-device AI for privacy`
- `Present a SaaS idea for AI-assisted code review`
- `Pitch a marketplace connecting hobby farmers with local restaurants`
- `Pitch a wearable that nudges you to drink water based on your activity`
- `Pitch a no-code mobile app builder for indie musicians`

**What you'll see:**
- **Nine bubbles** alternating Founder → Investor → Advisor for three rounds.
- Each contribution starts with its role glyph + bold label
  (`:rocket: **Founder:**`, `:chart: **Investor:**`, `:lightbulb:
  **Advisor:**`).
- Tool calls (`lookup_market_data`, `estimate_unit_economics`,
  `search_competitors`) appear in the Tools panel as agents reach for data
  during their turn.
- **Final output** = the last participant's contribution.

---

## Sequential Hybrid — Local + Cloud meeting invite ⭐

**Demos:** the headline local + cloud story. Local on-device AI does
private RAG and summarisation; cloud AI drafts a polished invite by
calling **back into the device** for free/busy slots. The privacy
boundary is enforced **structurally** — through a deliberate pipeline
shape, not just by trusting the model to redact.

```
USER prompt
  │
  ▼
1. local-inbox-search        local agent       picks email → PickedEmail JSON
  │
  ▼
2. local-picker-to-state     local executor    parse JSON · save PickedEmail to workflow state · forward body
  │
  ▼
3. local-issue-summariser    local agent       2-3 sentence brief, strips PII
  │
  ▼
4. local-summary-to-cloud    local executor    one-way valve: forward summary only
  │
  ▼
5. cloud-invite-drafter      cloud agent       Azure OpenAI · writes body · calls get_calendar tool
  │                                                  │
  │                                                  └─ tool runs on-device ─▶ CalendarService
  ▼
6. local-invite-finaliser    local executor    read PickedEmail back from state · build envelope (subject/from/to/mailto)
  │
  ▼
7. local-output-messages     terminal
```

| Stage | Where it runs | Tools | Role |
| --- | --- | --- | --- |
| `local-inbox-search` | Local (on-device) | RAG over fabricated inbox | Picks one customer email; returns `PickedEmail` JSON |
| `local-picker-to-state` | Local executor | — | Parses the JSON, stores the typed `PickedEmail` in **workflow state** under `"picked-email"`. Forwards a single user message containing only the email **body** downstream. |
| `local-issue-summariser` | Local (on-device) | — | Sees only the body. Writes a 2-3 sentence brief; **omits addresses, phones, passwords, cards, SSNs**. |
| `local-summary-to-cloud` | Local executor | — | One-way valve. Forwards only the summary as a fresh user message — nothing else from prior turns crosses the boundary. |
| `cloud-invite-drafter` | Cloud (Azure OpenAI) | `get_calendar` | Sees only the summary. Calls back to the device for free/busy slots. Writes the **body** of the invite — no subject/from/to lines. |
| `local-invite-finaliser` | Local executor | — | Reads `PickedEmail` back from workflow state and assembles the full envelope (subject/from/to + cloud body + mailto link). |
| `local-output-messages` | Local executor | — | Terminal — yields the final messages as workflow output. |

**Why two executors between the LLM agents?**

MAF agents communicate by passing the full chat history forward. Without
the executors, the cloud agent would receive the original user prompt
**and** the raw inbox JSON **and** the summary, defeating the privacy
story. The executors are explicit, auditable boundaries: each one decides
exactly what its downstream stage gets to see.

The picked email's subject and recipient address are kept on-device via
`IWorkflowContext.QueueStateUpdateAsync<PickedEmail>("picked-email", …,
scopeName: "shared")`. The cloud never sees them; the finaliser reads
them back at the end to build the envelope.

**Privacy contract — cloud sees:**
- the 2-3 sentence summary brief (customer name + IDs only)
- calendar free/busy slots with generic labels (`Standup`, `Focus block`, etc — no real event titles)

**Cloud NEVER sees:**
- the customer's raw email body
- the customer's email address or full name (lives in workflow state)
- physical address, phone, password, credit card, SSN
- the user's actual calendar events
- the rest of the inbox
- the original user prompt

**Try:**
- `Draft a meeting invite to resolve the latest customer issue`
- `Find the most urgent customer issue and propose a meeting`
- `Schedule a follow-up with the customer about the lockout problem`

**What you'll see (7 graph nodes, all green):**
- **Inbox search bubble** — JSON picked email (sender, subject, body).
- **Picker-to-state info** — event in the sidebar showing what was saved to state.
- **Summariser bubble** — short prose brief, no PII.
- **Summary-to-cloud info** — event in the sidebar confirming the forwarded brief length.
- **Drafter bubble** — `## :event: Meeting invite — <reason>` heading, `get_calendar` tool call visible in the Tools sidebar, body with proposed slot.
- **Finaliser bubble** — YAML frontmatter with real `to:` / `from:` / `subject:` + the cloud's body.

---



Most prompts are open — the agents pull the topic out of the user message
and adapt. Use this for live demos:

- For **Sequential**, swap the topic: `Write about <topic>` — fact-checker
  will hit different verdicts depending on how mainstream the topic is.
- For **Concurrent**, swap the destination: `Plan a 3 day trip to
  <city>` — restaurant prices, transport options, and currency change with
  destination.
- For **Handoff**, swap the symptom: a VPN keyword → network, a crash
  keyword → software, a hardware keyword → hardware.
- For **Group chat**, swap the startup category: each role still plays
  itself but pulls different market data / competitors.
