# Demo Orchestrations

Shared workflow definitions used by both the web (Demo1) and MAUI (Demo2) demos.

## Standalone Agents

| Agent | Description |
|-------|-------------|
| `storyteller` | Creative storyteller — short stories with vivid language and twists |
| `code-mentor` | Friendly coding mentor — explains concepts, helps debug |

**Example prompts:**
- "Tell me a story about a robot who learns to paint"
- "Explain async/await in C# with a simple example"

---

## Sequential Workflow — Newsdesk

**How it works:** Agents execute one after another, each passing its output to the next.

| Agent | Role |
|-------|------|
| `sequential-newsdesk-reporter` | Writes initial news article |
| `sequential-newsdesk-factchecker` | Reviews for accuracy, annotates claims |
| `sequential-newsdesk-editor` | Polishes for clarity, formats final version |

**Example prompts:**
- "Write a news article about a breakthrough in fusion energy"
- "Cover the announcement of a new Mars mission"

---

## Concurrent Workflow — Travel Planner

**How it works:** Multiple specialists analyze in parallel, then a coordinator synthesizes results.

| Agent | Role |
|-------|------|
| `concurrent-travel-food` | Culinary recommendations |
| `concurrent-travel-culture` | Cultural sites and experiences |
| `concurrent-travel-logistics` | Transport, accommodation, routing |
| `concurrent-travel-coordinator` | Assembles cohesive itinerary |

**Example prompts:**
- "Plan a 5-day trip to Tokyo for a food-loving couple"
- "Design a weekend getaway to Barcelona on a budget"

---

## Handoff Workflow — IT Help Desk

**How it works:** A dispatcher routes the request to the most appropriate specialist.

| Agent | Role |
|-------|------|
| `handoff-helpdesk-dispatcher` | Analyzes issue, routes to specialist |
| `handoff-helpdesk-network` | VPN, Wi-Fi, DNS, firewall |
| `handoff-helpdesk-software` | App crashes, installs, licensing |
| `handoff-helpdesk-hardware` | Laptops, monitors, peripherals |

**Example prompts:**
- "My VPN keeps disconnecting every 10 minutes and I can't access the internal wiki"
- "Excel crashes whenever I open files larger than 50MB"

---

## Group Chat Workflow — Startup Pitch

**How it works:** Agents take turns contributing to a shared discussion over multiple rounds (round-robin, 3 iterations).

| Agent | Role |
|-------|------|
| `groupchat-startup-founder` | Pitches the idea, defends vision |
| `groupchat-startup-investor` | Asks tough questions, evaluates |
| `groupchat-startup-advisor` | Bridges gaps, suggests improvements |

**Example prompts:**
- "Pitch an AI-powered personal finance app that uses on-device models for privacy"
- "Present a SaaS idea for AI-assisted code review"
