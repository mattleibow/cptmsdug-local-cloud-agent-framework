# Copilot Instructions for This Repository

## Project Overview

Single .NET MAUI app for a user-group demo on combining on-device AI
(Apple Intelligence) with cloud AI (Azure OpenAI) via the Microsoft Agent
Framework (MAF).

```
demos/
  Demo.MauiAgentApp/        ← the app (multi-TFM: maccatalyst, ios, android, windows)
    Orchestrations/         ← agents + workflows live here
      Sequential/
      Concurrent/
      Handoff/
      GroupChat/
      SequentialHybrid/     ← the marquee local+cloud meeting-invite demo
      StandaloneAgents.cs
      AIModels.cs
      TelemetryExtensions.cs
      WorkflowExtensions.cs
src/
  Microsoft.Maui.AI.Agents.DevUI/   ← reusable native DevUI control library
```

The web variant was dropped — Apple Intelligence is device-local and the
demo story needs the on-device path on macCatalyst.

## Development Loop — VALIDATE EVERYTHING

**Code is never "done" until it is validated in the running app.**

### One-shot build + launch + telemetry

```bash
# 0. Aspire Dashboard — start once per session, HTTPS endpoints are required
#    (Apple's CFNetwork stack will not negotiate h2c, so OTLP/gRPC over plain
#    HTTP fails silently with HTTP 400; HTTPS works because dev-cert trust
#    enables HTTP/2 over TLS).
aspire dashboard run --allow-anonymous \
  --frontend-url https://localhost:18888 \
  --otlp-grpc-url https://localhost:4317 \
  --otlp-http-url https://localhost:4318 &

# 1. Build (one project, one TFM)
cd demos/Demo.MauiAgentApp
dotnet build -f net10.0-maccatalyst -warnaserror

# 2. Stop any prior instance, then launch
PID=$(pgrep -f "Demo.MauiAgentApp" | head -1)
[ -n "$PID" ] && kill "$PID" && sleep 2
open "bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/Agent DevUI.app"

# 3. Drive the app
maui devflow wait
maui devflow ui screenshot --output /tmp/app.png --overwrite
```

Force a clean rebuild only when entitlements / signing change:
`rm -rf bin/Debug/net10.0-maccatalyst && dotnet build ...`

### Drive the UI with MAUI DevFlow (skill: `maui-devflow-debug`)

You ARE the user — drive everything through DevFlow:

```bash
maui devflow ui tree --depth 10
maui devflow ui query --automationId DevUI.MessageEntry
maui devflow ui query --type Picker
maui devflow ui query --text "Send"

maui devflow ui tap <elementId>
maui devflow ui fill DevUI.MessageEntry "Draft a meeting invite to resolve the latest customer issue"
maui devflow ui tap DevUI.SendButton

maui devflow theme set dark
maui devflow logs --tail 50
```

### Inspect telemetry

```bash
# Trace and log lists (filter with -n N, --severity Trace, --trace-id <id>)
aspire otel traces --dashboard-url "https://localhost:18888" -n 20
aspire otel logs   --dashboard-url "https://localhost:18888" --severity Trace -n 20

# Or the dashboard UI:
open https://localhost:18888
```

`Microsoft.Extensions.AI` / `Microsoft.Agents.AI` / `Demo.MauiAgentApp`
log filters are pinned to **Trace**, and both chat clients are wrapped in
`.UseOpenTelemetry(... EnableSensitiveData = true)`, so prompt and response
content lands in the spans. **Dev-only switch — never ship with real
user data flowing through the pipeline.**

## Sub-agents for review

After significant UI or workflow changes, run a 2-model review pass:

| Agent | Best at |
|-------|---------|
| `claude-opus-4.7` | DevUI parity vs. official React DevUI, architecture |
| `gpt-5.5` | Second opinion on code quality + edge cases |

Always give the reviewer: a fresh screenshot, the relevant diff, and a one-
paragraph description of what the official DevUI looks like for the same flow.

## Skills

| Skill | When |
|-------|------|
| `maui-devflow-debug` | Primary loop — build, deploy, inspect, interact |
| `devflow-connect` | DevFlow can't connect (port / broker / discovery) |
| `maui-devflow-onboard` | Adding / updating DevFlow packages |

## Key Technical Details

- **Target**: multi-TFM (`net10.0-{android,ios,maccatalyst,windows10.0.19041.0}`); marquee path is **maccatalyst**.
- **SDK**: pinned in `global.json` to `10.0.201` with `rollForward: latestFeature`.
- **DevFlow**: `Microsoft.Maui.DevFlow.Agent` 0.1.0-preview.10.26274.3.
- **Entitlements**: MUST have `com.apple.security.network.server` (DevFlow listener) and `com.apple.security.network.client` (OTLP export).
- **Secrets**: `~/.microsoft/usersecrets/ai-attributes-secrets/secrets.json` is embedded by an MSBuild target. **Dev only.**
- **Azure OpenAI endpoint**: needs `/openai/v1` appended for the non-Azure-SDK client.
- **Apple Intelligence**: iOS / macCatalyst 26+ on Apple silicon. Other platforms fall back to the cloud client for the `local-model` key.

## Observability — How It Works

- `builder.AddDemoTelemetry("Demo.MauiAgentApp")` (in `Orchestrations/TelemetryExtensions.cs`) does it all:
  - Registers tracing for HTTP client + `Microsoft.Extensions.AI` + `Microsoft.Agents.AI`.
  - Registers metrics for HTTP client + runtime + the two AI sources.
  - Registers the OTel logging provider on `builder.Logging` (the only logging entry point MAUI consumes).
  - Calls `.UseOtlpExporter()` once — reads `OTEL_EXPORTER_OTLP_ENDPOINT` / `_PROTOCOL` from configuration (seeded to `https://localhost:4317` / `grpc`).
  - Registers an `IMauiInitializeService` that resolves `TracerProvider` / `MeterProvider` / `LoggerProvider` after `Build()` — MAUI never starts `IHostedService` instances, so without this nothing exports.

### Connectivity by platform (OTLP endpoint)

| Platform | Endpoint | Notes |
|----------|----------|-------|
| macCatalyst | `https://localhost:4317` | Dev cert must be trusted (`dotnet dev-certs https --trust`). |
| iOS Simulator | `https://localhost:4317` | Same host network. |
| Android Emulator | `https://10.0.2.2:4317` | Host-loopback alias. |
| Physical device | `https://<host-lan-ip>:4317` | Same Wi-Fi; dev cert must be trusted on the device. |

## UI Design Reference (Official DevUI)

- **Brand**: purple `#643FB2` (light) / `#8B5CF6` (dark)
- **Layout**: 3-panel (chat | splitter | debug sidebar)
- **Header**: logo + agent picker + theme toggle + new chat
- **Debug tabs**: Events, Traces, Tools
- **Workflow graph**: nodes with status colors + animated running state
- **Node states**: pending=gray, running=purple+pulse, completed=green, failed=red, cancelled=orange
- **Message bubbles**: user=purple tint (right), assistant=muted (left), with agent label
- **Streaming**: cursor `▍` appended during streaming, 120 ms buffer interval

## Common Pitfalls

1. **Entitlements not re-signed**: after changing `Entitlements.plist`, `rm -rf bin/Debug/net10.0-maccatalyst` to force a fresh sign.
2. **DevFlow "Permission denied"**: missing `com.apple.security.network.server`.
3. **No telemetry**: dashboard must be on HTTPS (CFNetwork ≠ h2c) and started **before** the app. App exports on startup; it does not retry against a late-arriving dashboard. Trust the dev cert: `dotnet dev-certs https --trust`.
4. **Streaming hangs UI**: wrap AI calls in `Task.Run()` and UI updates in `RunOnUIAsync()`.
5. **Xcode version mismatch**: `<ValidateXcodeVersion>false</ValidateXcodeVersion>` in the csproj covers Xcode N+1.
6. **Package confusion**: use `Microsoft.Maui.DevFlow.Agent` (NOT `Redth.MauiDevFlow.Agent`).
7. **Aspire CLI missing**: `dotnet tool install --global aspire.cli`.
8. **`partial` missing on tool-source class**: `[AIToolSource]` requires its containing class to be `partial`.
9. **`[FromServices]` not found**: it's in `Microsoft.Extensions.DependencyInjection`, not `Microsoft.Maui.AI.Attributes`.
