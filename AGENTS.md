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

The historical L6 package 0.6.0-l6 has nine localized README tutorials and a zh-CN-only UI.
Packaging requires a clean commit, corresponding-source archive and SHA256SUMS.
Keep manifest verification, per-version install, no-overwrite rules, unknown-file
protection and narrow uninstall scope. Tests use isolated paths. Audit reachable
history and current files. Publication confirmation applies to the specific reviewed
candidate, not to later updates.

The maintainer explicitly confirmed publication on 2026-10-23. Public repository:
Thicky-han/DayKeep. Pre-release v0.6.0-l6 is fixed at build commit
93f6307b52e908c9fe4b31c97c4472286b8fa97c. Preserve the approved asset bytes and
tag. Publication and documentation commits after that build did not change its
approved assets. Do not overwrite the old release when preparing an update.

The maintainer requested the 0.7.0-beta.1 language update on 2026-10-23 and asked
for confirmation after packaging. This new candidate is not approved for public
publication. Prepare and verify the local binary/source archives and SHA256SUMS,
then ask; do not push, tag or publish this update before that confirmation.
See LANGUAGE_UPDATE_REPORT.md for current evidence; pending results stay pending.
Do not reuse historical L6 counts as proof that the new build passed.

0.7 supports zh-CN, zh-TW, en, ja, ko, fr, de, es and pt. Keep the top-level
Language / 语言 selector visible and reachable in expanded and compact windows;
list native language names. Default to zh-CN for missing/unsupported saved choices.
Persist before applying a change; show save errors and retain the previous language
on failure. Refresh existing windows and tray text without losing drafts, filters,
reviewed import state or reminder delivery identity. Do not translate task titles,
notes, links or raw stored records. Translate only app-owned presentation labels
and messages. Keep scheduling in the computer's local time zone with invariant
HH:mm and yyyy-MM-dd input, schema 5, and unchanged task data. History search matches
current-language action keywords as well as canonical Chinese actions and original
user text before the 200-result limit. Language changes preserve existing results,
filters and search text; another query uses the newly selected language. Logical backups
exclude language/settings; import must not change the current language.
Update all nine README tutorials and the packaged docs/LANGUAGES.md. Verify resource
keys/placeholders, all language selections, live existing windows, persistence/save
failure, compact access and rendered layout. Package checks must require all nine
resource JSONs and language test source in corresponding source. Three-day trial,
physical hardware gaps and the deferred final L4 live-minute observation remain
unpassed until separately evidenced.

The maintainer approved the loop clock with amber reminder dot on 2026-10-22
before requesting L2. Reuse assets/brand PNG/ICO across all surfaces;
do not restore the old sunrise or draw another tray-specific variant.
