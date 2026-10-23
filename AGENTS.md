# DayKeep / 日守

Read PROJECT_SPEC.md, IMPLEMENTATION_PLAN.md and the current stage report before
changing scope. This is an independent Windows desktop application. Follow the
maintainer's established one-stage-at-a-time workflow. Complete the current
stage, record evidence and limitations, and hand back the result. Do not silently
implement or declare later stages complete.

L0 through L4 are complete. Keep the pure core planner, production 1-second WPF
timer, local time/weekdays and merged non-activating windows. Checking is distinct
from completion. L3 persists per-task daily extra counts, next repeat, snooze and
last-delivery watermark in schema 3. Default: 30 minutes, 3 extras per local day;
fixed slots share the cap, collisions prefer fixed, explicit snooze overrides all
intervening automatic slots and does not spend the cap. Daily checks, pause,
completion/archive and edits clear pending followups atomically; readiness rearms
from the confirmed time. Date-only deadlines prioritize today/overdue; never auto-complete.
Schema 1/2/3 upgrades are transactional. Failed initialization must stop reads/writes;
preserve original files on invalid/unknown/newer schema or failed migration.
Production enables missed-slot recovery: latest eligible fixed slot per task,
never replaying all missed days. Schema 4 queues delivery and progress atomically
before showing; Pending/Shown restore until explicit durable dismissal or cancellation.
Keep only the latest active delivery per task. Daily budgets persist by local date,
including date rollback. Fixed IDs use local wall time; repeat/snooze IDs also use
generation and UTC instant. Poll recent keys plus all active records, not full history.
System power/session/time/display events refresh on the UI dispatcher, with timer
fallback. Defer on unavailable desktop/fullscreen and fit in monitor work area.
Login startup is opt-in HKCU Run with quoted exe and --background; never enable it
on installation/upgrade. Test its adapter in an isolated registry key. Single-instance
tests use isolated databases and named objects, preserving the user's running app.
L5 adds history and versioned logical backups. Schema 5 adds a revision floor;
imports rebase revisions while keeping stable delivery keys. Preview freezes the
reviewed file and hashes the target. Merge keeps complete local same-ID groups;
replace restores the full source only after writing a recovery backup. Failures
must preserve data; never overwrite exports. Keep impact confirmation and the
open-editor guard. L5 development is delivered; human trial remains pending.
The maintainer explicitly requested L6 on 2026-10-23 while trial is pending.
Proceed with L6 preparation without declaring trial or hardware acceptance passed.
Package all reviewable materials before asking for final publishing confirmation.
Previews never update tasks.

Maintain separate shell, domain, persistence and reminder scheduling boundaries.
Only the surface brush should be translucent; text and controls remain opaque.
Keep close/hide distinct from application exit. Do not silently swallow persistence
errors. No telemetry, accounts, backend, passwords, scraping or automatic payment.

Use .NET SDK pinned by global.json. Run scripts/build.ps1, scripts/test-shell.ps1
for shell changes, scripts/test-core.ps1 for domain/storage, scripts/check.ps1
and git diff --check. For scheduling changes use scripts/test-reminder-live.ps1
for a separate real-clock observation on an interactive desktop; deterministic
clock checks are not real delivery evidence. Keep packages.lock.json under version control, use locked
restore and review dependency/license changes. Visually inspect WPF
renders after layout changes. Controlled smoke results are not human keyboard,
multi-monitor, full-screen or long-running reminder acceptance.
Build shared projects sequentially; simultaneous dotnet builds can lock common obj files.

Use a standalone local main repository and stage commits. Exclude tools, bin/obj,
personal data, generated binaries and test reports. Keep AGPL-3.0-only and original
brand provenance. L6 covers final artwork review, multilingual READMEs, packaging,
public-content audit, GitHub main and final release confirmation. Do not publish
a public repository or Release before L6. A user instruction changing this workflow
takes precedence. No sub-agent delegation is required.

L6 candidate 0.6.0-l6 has nine full localized README tutorials. UI remains zh-CN.
Packaging requires a clean commit, corresponding-source archive and SHA256SUMS.
Keep manifest verification, per-version install, no-overwrite rules, unknown-file
protection and narrow uninstall scope. Tests use isolated paths. Audit reachable
history and current files. The maintainer asked for confirmation after packaging;
do not create a remote, push, tag or publish until that confirmation arrives.

The maintainer approved the loop clock with amber reminder dot on 2026-10-22
before requesting L2. Reuse assets/brand PNG/ICO across all surfaces;
do not restore the old sunrise or draw another tray-specific variant.
