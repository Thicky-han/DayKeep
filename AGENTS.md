# DayKeep / 日守

Read PROJECT_SPEC.md, IMPLEMENTATION_PLAN.md and the current stage report before
changing scope. This is an independent Windows desktop application. Follow the
maintainer's established one-stage-at-a-time workflow. Complete the current
stage, record evidence and limitations, and hand back the result. Do not silently
implement or declare later stages complete.

L0 shell and L1 task management are complete. L1 uses a real, initially empty
SQLite database and supports create/edit, pause/resume, complete/persisted undo,
archive/restore and filtering. Task checks are stored separately from completion;
their user-facing daily workflow belongs to L2. Reminder times/weekdays are saved
but no timer, repeat reminders or startup registration exists. Preview reminders
are explicitly marked and never update real tasks. The next stage is L2 after
L1 handoff; begin it when the maintainer asks to continue.

Maintain separate shell, domain, persistence and reminder scheduling boundaries.
Only the surface brush should be translucent; text and controls remain opaque.
Keep close/hide distinct from application exit. Do not silently swallow persistence
errors. No telemetry, accounts, backend, passwords, scraping or automatic payment.

Use .NET SDK pinned by global.json. Run scripts/build.ps1, scripts/test-shell.ps1
for shell changes, scripts/test-core.ps1 for domain/storage, scripts/check.ps1
and git diff --check. Keep packages.lock.json under version control, use locked
restore and review dependency/license changes. Visually inspect WPF
renders after layout changes. Controlled smoke results are not human keyboard,
multi-monitor, full-screen or long-running reminder acceptance.

Use a standalone local main repository and stage commits. Exclude tools, bin/obj,
personal data, generated binaries and test reports. Keep AGPL-3.0-only and original
brand provenance. L6 covers final artwork review, multilingual READMEs, packaging,
public-content audit, GitHub main and final release confirmation. Do not publish
a public repository or Release before L6. A user instruction changing this workflow
takes precedence. No sub-agent delegation is required.

Current 0.1.1-l1 artwork is the loop clock with an amber reminder dot, applied at
the maintainer's explicit request. Reuse assets/brand PNG/ICO across all surfaces;
do not restore the old sunrise or draw another tray-specific variant.
