# DayKeep / 日守

Read PROJECT_SPEC.md, IMPLEMENTATION_PLAN.md and the current stage report before
changing scope. This is an independent Windows desktop application. Follow the
maintainer's established one-stage-at-a-time workflow. Complete the current
stage, record evidence and limitations, and hand back the result. Do not silently
implement or declare later stages complete.

L0 is complete: real translucent WPF shell, tray, hotkey, compact layout,
reminder STYLE preview and local appearance settings. Example tasks are explicitly
marked and must never be represented as scheduled or stored user tasks. New-task
UI stays disabled until L1. There is no timer, task database or startup registration.
The next stage is L1; begin it when the maintainer asks to continue.

Maintain separate shell, domain, persistence and reminder scheduling boundaries.
Only the surface brush should be translucent; text and controls remain opaque.
Keep close/hide distinct from application exit. Do not silently swallow persistence
errors. No telemetry, accounts, backend, passwords, scraping or automatic payment.

Use .NET SDK pinned by global.json. Run scripts/build.ps1, scripts/test-shell.ps1
for shell changes, scripts/check.ps1 and git diff --check. Visually inspect WPF
renders after layout changes. Controlled smoke results are not human keyboard,
multi-monitor, full-screen or long-running reminder acceptance.

Use a standalone local main repository and stage commits. Exclude tools, bin/obj,
personal data, generated binaries and test reports. Keep AGPL-3.0-only and original
brand provenance. L6 covers final artwork review, multilingual READMEs, packaging,
public-content audit, GitHub main and final release confirmation. Do not publish
a public repository or Release at L0. A user instruction changing this workflow
takes precedence. No sub-agent delegation is required.
