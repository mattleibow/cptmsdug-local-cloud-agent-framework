# Copilot Instructions for This Repository

## Project Overview

This repo contains demo apps for a user group presentation on combining on-device AI (Apple Intelligence) with cloud AI (Azure OpenAI) using the Microsoft Agent Framework (MAF).

- **Demo.WebAgentApp**: ASP.NET Core web app with MAF DevUI at `/devui`
- **Demo.MauiAgentApp**: Native .NET MAUI app that recreates the DevUI experience natively

## Development Workflow — VALIDATE EVERYTHING

**Code is never "done" until it is validated in the running app.** Follow this loop:

### 1. Build → 2. Deploy → 3. Validate → 4. Review → 5. Fix → repeat

### Build
```bash
cd demos/Demo.MauiAgentApp
dotnet build -f net10.0-maccatalyst -warnaserror
```

### Deploy (Launch the app)
```bash
# Ensure Aspire Dashboard is running (for telemetry)
aspire dashboard run --allow-anonymous 2>/dev/null &

# Kill any running instance first
kill $(pgrep -f "Demo.MauiAgentApp") 2>/dev/null
# Clean rebuild (needed if entitlements/signing change)
rm -rf bin/Debug/net10.0-maccatalyst && dotnet build -f net10.0-maccatalyst -warnaserror
# Launch
open "bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/Agent DevUI.app"
```

### Validate with MAUI DevFlow (USE THE `maui-devflow-debug` SKILL)

DevFlow is your Playwright/Appium equivalent. You ARE the user. Use it to:

```bash
# Wait for agent connection
maui devflow wait

# Take screenshots to see what the UI looks like
maui devflow ui screenshot --output /tmp/screenshot.png --overwrite

# Get the full visual tree (see what elements exist)
maui devflow ui tree --depth 10

# Find specific elements
maui devflow ui find --text "Send"
maui devflow ui find --type "Entry"
maui devflow ui find --type "Picker"

# Interact with the app AS A USER
maui devflow ui tap --id "elementId"
maui devflow ui tap --text "Send"
maui devflow ui type --id "elementId" --text "Hello world"
maui devflow ui picker --id "pickerId" --value "writer"

# Check theme
maui devflow theme get
maui devflow theme set dark
maui devflow theme set light

# View logs for errors
maui devflow logs --tail 50
```

### Review with AI Models

After each significant change, get a code review:
- Use **Opus 4.7** (`claude-opus-4.7`) for UI comparison and architecture review
- Use **GPT 5.5** (`gpt-5.5`) for a second opinion on code quality

Provide them with:
1. A screenshot of the current app state
2. The relevant code changes
3. A description of what the official DevUI looks like for comparison

### Fix and Iterate

Never stop at "it compiles." Iterate until:
- The app renders correctly (verified via screenshot)
- User interactions work (verified via DevFlow tap/type)
- Streaming works without hanging (verified by sending a message and watching the response)
- All tabs/panels function correctly
- Dark and light mode both look good

## Skills to Use

| Skill | When to Use |
|-------|-------------|
| `maui-devflow-debug` | Primary tool: build, deploy, inspect, interact with the running app |
| `devflow-connect` | When DevFlow can't connect to the app (port issues, broker problems) |
| `maui-devflow-onboard` | If DevFlow packages need to be added/updated |

## Key Technical Details

- **Target**: `net10.0-maccatalyst` (multi-TFM: android, ios, maccatalyst)
- **SDK**: Pinned in `global.json` to 10.0.201 with `rollForward: latestFeature`
- **DevFlow Package**: `Microsoft.Maui.DevFlow.Agent` 0.1.0-preview.10.26274.3
- **Entitlements**: MUST have `com.apple.security.network.server` for DevFlow HTTP listener
- **Secrets**: Embedded via MSBuild target from `~/.microsoft/usersecrets/ai-attributes-secrets/secrets.json`
- **Azure OpenAI**: Endpoint needs `/openai/v1` appended for non-Azure-SDK client

## Observability — Aspire Dashboard + OpenTelemetry

Both demo apps export telemetry (traces, metrics, structured logs) via OTLP/HTTP to the standalone Aspire Dashboard.

### Prerequisites

The Aspire CLI must be installed. Install or upgrade:
```bash
# Install (first time)
dotnet tool install --global aspire.cli

# Upgrade to latest
dotnet tool update --global aspire.cli

# Verify
aspire --version
```

### Start the Aspire Dashboard

**Always start the dashboard before launching demo apps:**
```bash
# Start dashboard (runs in background, anonymous auth for local dev)
aspire dashboard run --allow-anonymous &

# Dashboard UI: http://localhost:18888
# OTLP/HTTP endpoint: http://localhost:4318 (apps send telemetry here)
# OTLP/gRPC endpoint: http://localhost:4317
```

### How It Works

- Both apps use `AddDemoTelemetry()` from `Demo.Orchestrations/TelemetryExtensions.cs`
- Traces capture: HTTP client calls, AI completions (`Microsoft.Extensions.AI`), agent workflows (`Microsoft.Agents.AI`)
- Metrics capture: HTTP client metrics, AI operation metrics
- Structured logs: All `ILogger` output is exported via OTLP
- Default endpoint: `http://localhost:4317` (OTLP/gRPC — required; HTTP/protobuf has issues on .NET 10)

### Viewing Telemetry

After the app is running and you've interacted with it:
1. Open **http://localhost:18888** in a browser
2. **Traces** tab → see AI completion spans, HTTP calls, workflow execution
3. **Structured Logs** tab → see all ILogger output with structured properties
4. **Metrics** tab → see HTTP client and AI operation metrics

Or query from terminal:
```bash
aspire otel traces --dashboard-url "http://localhost:18888"
aspire otel logs --dashboard-url "http://localhost:18888"
```

### Connectivity by Platform

| Platform | OTLP Endpoint | Notes |
|----------|--------------|-------|
| Web app (localhost) | `http://localhost:4318` | Direct |
| macCatalyst | `http://localhost:4318` | Same host |
| iOS Simulator | `http://localhost:4318` | Shares host network |
| Android Emulator | `http://10.0.2.2:4318` | Special IP for host access |
| Physical device | `http://<host-lan-ip>:4318` | Same Wi-Fi required |

## UI Design Reference (Official DevUI)

The native app should match the official React-based DevUI as closely as possible:
- **Brand color**: Purple `#643FB2` (light) / `#8B5CF6` (dark)
- **Layout**: 3-panel (chat | splitter | debug sidebar)
- **Header**: Logo + agent picker + theme toggle + new chat
- **Debug panel tabs**: Events, Traces, Tools (3 tabs)
- **Workflow graph**: Nodes with status colors + connecting arrows + animated running state
- **Node states**: pending=gray, running=purple+pulse, completed=green, failed=red, cancelled=orange
- **Message bubbles**: User=purple tint (right), Assistant=muted (left), with agent label
- **Streaming**: Cursor `▍` appended during streaming, 120ms buffer interval
- **Dark mode**: Near-black backgrounds, lighter purple accents

## Common Pitfalls

1. **Entitlements not signed**: After changing `Entitlements.plist`, you MUST delete `bin/Debug/net10.0-maccatalyst/` to force re-signing
2. **DevFlow "Permission denied"**: Missing `com.apple.security.network.server` entitlement
3. **Streaming hangs UI**: Always use `Task.Run()` for AI calls, `RunOnUIAsync()` for UI updates
4. **Xcode version mismatch**: Use `<ValidateXcodeVersion>false</ValidateXcodeVersion>` in csproj
5. **Package confusion**: Use `Microsoft.Maui.DevFlow.Agent` (NOT `Redth.MauiDevFlow.Agent`)
6. **Aspire CLI not installed**: Run `dotnet tool install --global aspire.cli` — required for the Aspire Dashboard
7. **No telemetry in dashboard**: Ensure the dashboard is started BEFORE the app. Apps export on startup and won't retry connecting to a dashboard started later.
