# Copilot Instructions for This Repository

## Project Overview

This repo contains demo apps for a user group presentation on combining on-device AI (Apple Intelligence) with cloud AI (Azure OpenAI) using the Microsoft Agent Framework (MAF).

- **Demo1.BasicAgent**: ASP.NET Core web app with MAF DevUI at `/devui`
- **Demo2.MauiAgent**: Native .NET MAUI app that recreates the DevUI experience natively

## Development Workflow — VALIDATE EVERYTHING

**Code is never "done" until it is validated in the running app.** Follow this loop:

### 1. Build → 2. Deploy → 3. Validate → 4. Review → 5. Fix → repeat

### Build
```bash
cd demos/Demo2.MauiAgent
dotnet build -f net10.0-maccatalyst -warnaserror
```

### Deploy (Launch the app)
```bash
# Kill any running instance first
kill $(pgrep -f "Demo2.Mau") 2>/dev/null
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
