# Parallel Development Tracks

> Source plan: `plan.md` (task details in `tasks.md`, 130 tasks)
> Generated: 2026-11-25
> Total tracks: 8

## Dependency Graph

```text
Track 1  Setup + Foundation (T001-T038)
   |
Track 2  US1 MVP: show a banner in a zone (T039-T060)
   |
   +--> Track 3  US2 rotation and scheduling (T061-T068)     \
   +--> Track 4  US3 impressions, clicks, reports (T069-T095, T123)   } parallel after Track 2
   +--> Track 5  US6 permissions, HTML banners, audit (T096-T105)    /
                    (T097 reports check waits for Track 4)
Track 4 --> Track 7  US5 limits (T113-T118)
Track 4 + Track 5 --> Track 6  US4 client mode for cached pages (T106-T112)
All --> Track 8  Polish and release (T119-T122, T124-T130; T119-T120 may start after Track 4)
```

- Tracks 1 and 2 run alone, one after the other (they block everything).
- Tracks 3, 4 and 5 can run at the same time after Track 2.
- Track 7 can run alongside Track 5 or 6 once Track 4 is merged.
- Track 6 waits for Tracks 4 and 5. Track 8 is last.

**Shared files.** Parallel tracks touch some of the same files. Run each parallel track in its own
git worktree (branch from `001-banner-manager`) and merge back one track at a time:

| File | Tracks |
|---|---|
| `src/Meresoft.Umbraco.Banners/Composing/BannersBuilderExtensions.cs` | 2, 4, 5, 6, 7 |
| `src/Meresoft.Umbraco.Banners/Rendering/BannerZoneHtmlWriter.cs` | 2, 4, 5, 6 |
| `src/Meresoft.Umbraco.Banners/Caching/LiveSnapshotBuilder.cs` | 2, 3, 5, 7 |
| `src/Meresoft.Umbraco.Banners/Selection/BannerSelector.cs` | 2, 3, 7 |
| `src/Meresoft.Umbraco.Banners/Client/src/banners/banner-editor.element.ts` | 2, 3, 5, 7 |
| `src/Meresoft.Umbraco.Banners/Client/script/banners.ts` | 4, 6 |
| `src/Meresoft.Umbraco.Banners/Client/src/api/` (generated client) | 2, 4, 5 (regenerate after each merge) |

**Rules for every track** (from `.specify/memory/constitution.md` v1.0.2):

- Test first: write the tests of the track, see them fail, then implement.
- Invented test data only. No client names or data in the package.
- No em dashes anywhere (code, comments, docs, commit messages).
- No secrets, keys or connection strings in the repository.
- One commit per task, ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
  Never push without the owner's approval.
- Tick the finished tasks in `tasks.md` (`- [x]`).

---

## Track 1: Setup and Foundation

**Skills:** `/feature-dev:feature-dev`, `/superpowers:test-driven-development`, `/superpowers:dispatching-parallel-agents`, `/superpowers:requesting-code-review`, `/superpowers:verification-before-completion`
**Dependencies:** None (can start immediately)
**Scope:** repository files, solution and projects, client asset builds, sample site, CI skeleton; options, domain rules, migrations with the administrator grant, repositories, section policy, management API base and OpenAPI document, back office section and localization, audit writer.
**Files likely affected:** root files (`LICENSE`, `.gitignore`, `.editorconfig`, `README.md`, `Directory.Build.props`, `Directory.Packages.props`, `Meresoft.Umbraco.Banners.slnx`, `.github/workflows/ci.yml`, `assets/icon.png`), `src/Meresoft.Umbraco.Banners/` (Composing, Configuration, Domain, Persistence, ManagementApi, Services/Audit, Client), `samples/Meresoft.Umbraco.Banners.Sample/`, `tests/*`

### Tasks
1. Phase 1 Setup: T001 to T014 (T005 to T010, T012, T013 in parallel). Checkpoint: solution builds, unit tests pass, sample site starts.
2. Phase 2 tests: T015 to T023 (all parallel), see them fail.
3. Phase 2 implementation: T024 to T038 in dependency order (T024, T025, T030, T036, T037, T038 parallel; T026 after T025; T027 -> T028 -> T029; T031, T032 after T030; T033 -> T034; T035).
4. Checkpoint: migration, grant, repository, policy and OpenAPI tests pass on SQLite; the empty Banners section shows for an administrator in the sample site.

### Agent Prompt
> You are working in `/Users/alekseyshturman/RiderProjects/Meresoft.Umbraco.Banners` on branch `001-banner-manager`. This repository will hold `Meresoft.Umbraco.Banners`, a generic, MIT licensed Umbraco 17 / .NET 10 banner manager package. Read `.specify/memory/constitution.md`, then `specs/001-banner-manager/plan.md`, `research.md`, `data-model.md`, `contracts/` and `tasks.md`. Your job is Track 1 in `specs/001-banner-manager/tracks.md`: tasks T001 to T038 (Phase 1 Setup and Phase 2 Foundation). Follow each task's file paths and rules exactly. Use `/feature-dev:feature-dev` for the overall flow and `/superpowers:test-driven-development` for every Phase 2 behaviour: write T015 to T023 first and see them fail, then T024 to T038. Use `/superpowers:dispatching-parallel-agents` for the tasks marked [P] in different projects. Constitution rules: test first, invented data only, no client names, no em dashes, no secrets (sample admin credentials from user-secrets or environment variables). Commit after each task with a message ending in `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; never push. Tick finished tasks in `tasks.md`. Stop at both checkpoints in `tasks.md` and check them: `dotnet build Meresoft.Umbraco.Banners.slnx`, `dotnet test`, and the sample site shows the empty Banners section for the administrator. Finish with `/superpowers:requesting-code-review` and then `/superpowers:verification-before-completion`, and report what passed, with the command output, and anything you could not do.

---

## Track 2: US1 MVP, show a banner in a zone

**Skills:** `/feature-dev:feature-dev`, `/superpowers:test-driven-development`, `/frontend-design:frontend-design`, `/superpowers:requesting-code-review`, `/superpowers:verification-before-completion`
**Dependencies:** Track 1 merged
**Scope:** zone and image banner services with draft and publish, live snapshot and cache refresher, uniform selector, tag helper and view component, requested zones and first flush job, management API for zones and banners, back office zones and banner views, US1 browser test.
**Files likely affected:** `src/Meresoft.Umbraco.Banners/Services/`, `Caching/`, `Selection/`, `Rendering/`, `Counting/BannersFlushJob.cs`, `Persistence/Repositories/RequestedZoneRepository.cs`, `ManagementApi/Zones/`, `ManagementApi/RequestedZones/`, `ManagementApi/Banners/`, `Client/src/zones/`, `Client/src/banners/`, `Client/src/api/`, `tests/e2e/us1-show-banner.spec.ts`

### Tasks
1. Tests first: T039 to T045 and T060 (parallel), plus models T046.
2. Services and caching: T047 -> T048 -> T049 -> T050; T051 and T052 alongside T049; T053.
3. API: T054, T055; registration T056; regenerate client T057.
4. Back office views: T058, T059 (parallel).
5. Checkpoint: quickstart scenarios 1 to 3, 5 and 14 pass, and scenario 4 up to "banner shows, link goes to the stored URL".

### Agent Prompt
> You are working in `/Users/alekseyshturman/RiderProjects/Meresoft.Umbraco.Banners` on branch `001-banner-manager`, after Track 1 (Setup and Foundation, T001 to T038) is done. Read `.specify/memory/constitution.md`, `specs/001-banner-manager/spec.md` (User Story 1), `plan.md`, `data-model.md`, `contracts/markup.md`, `contracts/management-api.md`, `contracts/configuration-and-csharp.md`, `quickstart.md` and `tasks.md`. Your job is Track 2 in `tracks.md`: tasks T039 to T060 (User Story 1, the MVP). Use `/superpowers:test-driven-development`: write T039 to T045 and the browser test T060 first and see them fail, then implement T046 to T059 in the order given in `tasks.md`. Use `/feature-dev:feature-dev` for the backend and `/frontend-design:frontend-design` for the Lit back office views T058 and T059, using only UUI components and localization keys. Selection is uniform random in this story, and image links point at the stored URL (the click address comes in US3). No database query during render (SC-003). Constitution rules: test first, invented data only, no em dashes, no secrets. Commit after each task with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; never push. Tick finished tasks in `tasks.md`. At the US1 checkpoint run the unit and integration tests and `tests/e2e/us1-show-banner.spec.ts` against the sample site. Finish with `/superpowers:requesting-code-review` and `/superpowers:verification-before-completion`, and report results with output.

---

## Track 3: US2 rotation and scheduling

**Skills:** `/superpowers:test-driven-development`, `/feature-dev:feature-dev`, `/frontend-design:frontend-design`, `/superpowers:verification-before-completion`
**Dependencies:** Track 2 merged; parallel with Tracks 4 and 5 (own worktree)
**Scope:** weighted sampler without repeats, schedule filter, multi-zone assignment in the snapshot, weight, schedule and show count fields in the editors.
**Files likely affected:** `src/Meresoft.Umbraco.Banners/Selection/` (`IRandomSource.cs`, `WeightedSampler.cs`, `ScheduleFilter.cs`, `BannerSelector.cs`), `Caching/LiveSnapshotBuilder.cs`, `Client/src/banners/`, `Client/src/zones/zone-editor.element.ts`, `Client/src/shared/date-time.ts`, `tests/e2e/us2-rotation.spec.ts`

### Tasks
1. Tests first: T061, T062, T063, T068 (parallel).
2. Implementation: T064, T065 (parallel), then T066, T067.
3. Checkpoint: weights 1 and 3 over 4,000 seeded draws within 5 points of 25% and 75%; expired banners never show.

### Agent Prompt
> You are working in `/Users/alekseyshturman/RiderProjects/Meresoft.Umbraco.Banners`, in a git worktree on a branch made from `001-banner-manager` after Tracks 1 and 2 are merged. Other agents may be working on Tracks 4 and 5 at the same time. Change only the files your tasks name, and keep edits to shared files (`LiveSnapshotBuilder.cs`, `BannerSelector.cs`, `banner-editor.element.ts`) small and local. Read `.specify/memory/constitution.md`, `specs/001-banner-manager/spec.md` (User Story 2 and the clarifications), `data-model.md`, `research.md` (R8) and `tasks.md`. Your job is Track 3 in `tracks.md`: tasks T061 to T068 (rotation and scheduling). Use `/superpowers:test-driven-development`: write T061, T062, T063 and the browser test T068 first, see them fail, then implement T064 to T067. Use a seeded random source and a fake `TimeProvider` in the tests. Use `/frontend-design:frontend-design` for the editor fields in T067 (times entered in the user's time zone, sent in UTC). Constitution rules: test first, invented data only, no em dashes. Commit after each task with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; never push. Tick finished tasks in `tasks.md`. Finish with `/superpowers:verification-before-completion`: run the unit, integration and US2 browser tests and report the output.

---

## Track 4: US3 impressions, clicks and reports

**Skills:** `/superpowers:test-driven-development`, `/feature-dev:feature-dev`, `/superpowers:dispatching-parallel-agents`, `/frontend-design:frontend-design`, `/superpowers:requesting-code-review`, `/superpowers:verification-before-completion`
**Dependencies:** Track 2 merged; parallel with Tracks 3 and 5 (own worktree)
**Scope:** view tokens and replay set, bot matcher, counter buffer and batched flush, public click and seen endpoints, package script (viewability), statistics repository and service, reports API, CSV, reports view with chart, privacy test.
**Files likely affected:** `src/Meresoft.Umbraco.Banners/Tokens/`, `Counting/`, `Delivery/`, `Persistence/Repositories/StatisticsRepository.cs`, `Services/IBannerStatisticsService.cs`, `Statistics/`, `ManagementApi/Reports/`, `Rendering/BannerZoneHtmlWriter.cs`, `Client/script/banners.ts`, `Client/scripts/check-size.mjs`, `Client/src/reports/`, `tests/e2e/us3-viewability.spec.ts`, `tests/.../Privacy/PrivacyTests.cs`

### Tasks
1. Tests first: T069 to T079 and T123 (all parallel).
2. Independent pieces: T080, T081, T082, T083, T091 (parallel).
3. Then: T084 -> T085; T086; T087 -> T088, T089; T090; T092 -> T093; T094.
4. T095: run the US3 browser test and quickstart scenario 10.
5. Checkpoint: 100 impressions and 3 clicks give 3.0% after the flush; no open redirect; script under 5 KB.

### Agent Prompt
> You are working in `/Users/alekseyshturman/RiderProjects/Meresoft.Umbraco.Banners`, in a git worktree on a branch made from `001-banner-manager` after Tracks 1 and 2 are merged. Tracks 3 and 5 may run at the same time. Change only the files your tasks name; your edits to `BannerZoneHtmlWriter.cs` and `BannersBuilderExtensions.cs` must stay small and local. Read `.specify/memory/constitution.md` (Privacy By Design and Fast Pages matter most here), `specs/001-banner-manager/spec.md` (User Story 3, FR-010 to FR-015, the view token and multi-server edge cases), `research.md` (R5 to R7, R10, R11), `data-model.md`, `contracts/public-endpoints.md`, `contracts/markup.md`, `contracts/management-api.md` and `tasks.md`. Your job is Track 4 in `tracks.md`: tasks T069 to T095 and T123. Use `/superpowers:test-driven-development`: write T069 to T079 and T123 first and see them fail. Use `/superpowers:dispatching-parallel-agents` for the independent pieces T080 to T083 and T091, then implement the rest in the order in `tasks.md`. Use `/frontend-design:frontend-design` for the reports view and the SVG chart (no chart library, accessible text alternative). Hard rules: no database write in page or seen requests; no IP, user agent or cookie stored or logged; the click endpoint redirects only to the stored link; the package script has no dependencies and stays under 5 KB. Constitution rules: test first, invented data only, no em dashes. Commit after each task with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; never push. Tick finished tasks in `tasks.md`. Finish with `/superpowers:requesting-code-review` and `/superpowers:verification-before-completion`, running all tests and `tests/e2e/us3-viewability.spec.ts`, and report the output.

---

## Track 5: US6 permissions, HTML banners and audit

**Skills:** `/superpowers:test-driven-development`, `/feature-dev:feature-dev`, `/frontend-design:frontend-design`, `/superpowers:requesting-code-review`, `/superpowers:verification-before-completion`
**Dependencies:** Track 2 merged; parallel with Tracks 3 and 4 (own worktree). The reports part of T097 needs Track 4: mark it `[Ignore]` with a reason until Track 4 is merged, then enable it.
**Scope:** 403 for every package endpoint without the section, HTML permission checks, Html banner type end to end, banner and zone history endpoints and views, entity user permission manifest.
**Files likely affected:** `src/Meresoft.Umbraco.Banners/ManagementApi/Authorization/HtmlPermissionChecker.cs`, `ManagementApi/Banners/` (create, update, history), `ManagementApi/Zones/GetZoneHistoryController.cs`, `Domain/BannerVersionValidator.cs`, `Caching/LiveSnapshotBuilder.cs`, `Rendering/BannerZoneHtmlWriter.cs`, `Delivery/ClickController.cs` (after Track 4), `Client/src/permissions/`, `Client/src/banners/`, `Client/src/zones/zone-history.element.ts`, `tests/e2e/us6-permissions.spec.ts`

### Tasks
1. Tests first: T096 to T100 (parallel).
2. Implementation: T101, T102, T103, T104 (parallel), T105.
3. Checkpoint: the sample editor sees no section and gets 403; a user without the HTML permission can publish but not change HTML; banner and zone history show changes.

### Agent Prompt
> You are working in `/Users/alekseyshturman/RiderProjects/Meresoft.Umbraco.Banners`, in a git worktree on a branch made from `001-banner-manager` after Tracks 1 and 2 are merged. Tracks 3 and 4 may run at the same time. Change only the files your tasks name, and keep shared file edits small. Read `.specify/memory/constitution.md`, `specs/001-banner-manager/spec.md` (User Story 6, FR-006, FR-017, FR-018, clarification 5), `research.md` (R4, R12), `contracts/management-api.md`, `contracts/markup.md` and `tasks.md`. Your job is Track 5 in `tracks.md`: tasks T096 to T105. Use `/superpowers:test-driven-development`: write T096 to T100 first and see them fail, then implement T101 to T105. The reports check in T097 and the Html 404 in `ClickController.cs` (T101) depend on Track 4. If Track 4 is not merged yet, mark that test `[Ignore("needs Track 4")]` and leave the click change for after the merge, and say so in your report. Use `/frontend-design:frontend-design` for the HTML field (read-only without the permission) and the banner and zone history views. Constitution rules: test first, invented data only, no em dashes. Commit after each task with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; never push. Tick finished tasks in `tasks.md`. Finish with `/superpowers:requesting-code-review` and `/superpowers:verification-before-completion`, running all tests and `tests/e2e/us6-permissions.spec.ts`, and report the output.

---

## Track 6: US4 client mode for cached pages

**Skills:** `/superpowers:test-driven-development`, `/feature-dev:feature-dev`, `/superpowers:verification-before-completion`
**Dependencies:** Tracks 4 and 5 merged
**Scope:** public zone delivery endpoint, client mode markup, client mode in the package script (safe DOM building, Html banner scripts re-created), render mode field in the zone editor.
**Files likely affected:** `src/Meresoft.Umbraco.Banners/Delivery/ZoneDeliveryController.cs`, `Delivery/Models/ZoneDeliveryResponse.cs`, `Rendering/BannerZoneHtmlWriter.cs`, `Client/script/banners.ts`, `Client/src/zones/zone-editor.element.ts`, `tests/e2e/us4-client-mode.spec.ts`

### Tasks
1. Tests first: T106, T107, T108 (parallel).
2. Implementation: T109, T110, T111, T112.
3. Checkpoint: two browsers on the cached page each get a banner and both impressions count; script still under 5 KB.

### Agent Prompt
> You are working in `/Users/alekseyshturman/RiderProjects/Meresoft.Umbraco.Banners` on branch `001-banner-manager`, after Tracks 1 to 5 are merged. Read `.specify/memory/constitution.md` (Fast Pages: the documented client side option for cached pages), `specs/001-banner-manager/spec.md` (User Story 4, FR-010), `contracts/public-endpoints.md`, `contracts/markup.md` and `tasks.md`. Your job is Track 6 in `tracks.md`: tasks T106 to T112. Use `/superpowers:test-driven-development`: write T106, T107 and T108 first and see them fail, then implement T109 to T112. The delivery response returns only public banner fields. The script builds elements through the DOM, not string concatenation, re-creates Html banner `<script>` elements so they run, and must stay under 5 KB (size check T091). Constitution rules: test first, invented data only, no em dashes. Commit after each task with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; never push. Tick finished tasks in `tasks.md`. Finish with `/superpowers:verification-before-completion`, running all tests and `tests/e2e/us4-client-mode.spec.ts`, and report the output.

---

## Track 7: US5 limits per banner

**Skills:** `/superpowers:test-driven-development`, `/feature-dev:feature-dev`, `/superpowers:verification-before-completion`
**Dependencies:** Track 4 merged; can run alongside Track 5 or 6 (own worktree)
**Scope:** limit filter, totals loaded into the snapshot, reload of totals after each flush, "limit reached" flag, limit fields in the editor (no click limit for Html banners).
**Files likely affected:** `src/Meresoft.Umbraco.Banners/Selection/LimitFilter.cs`, `Caching/LiveSnapshotBuilder.cs`, `Counting/BannersFlushJob.cs`, `Domain/BannerStateCalculator.cs`, `Client/src/banners/`, `tests/e2e/us5-limits.spec.ts`

### Tasks
1. Tests first: T113, T114, T118 (parallel).
2. Implementation: T115, T116, T117.
3. Checkpoint: limit 3, 5 views, the banner stops after the next flush and shows "Limit reached".

### Agent Prompt
> You are working in `/Users/alekseyshturman/RiderProjects/Meresoft.Umbraco.Banners`, in a git worktree on a branch made from `001-banner-manager` after Track 4 is merged. Read `.specify/memory/constitution.md`, `specs/001-banner-manager/spec.md` (User Story 5, FR-004, FR-006: no click limit for Html banners), `data-model.md` (runtime flags, Limit reached), `research.md` and `tasks.md`. Your job is Track 7 in `tracks.md`: tasks T113 to T118. Use `/superpowers:test-driven-development`: write T113, T114 and the browser test T118 first and see them fail, then implement T115 to T117. Totals are summed over all zones and days, including imported rows. The overshoot is at most one flush interval of views. Constitution rules: test first, invented data only, no em dashes. Commit after each task with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; never push. Tick finished tasks in `tasks.md`. Finish with `/superpowers:verification-before-completion`, running all tests and `tests/e2e/us5-limits.spec.ts`, and report the output.

---

## Track 8: Polish and release

**Skills:** `/superpowers:test-driven-development`, `/feature-dev:feature-dev`, `/superpowers:dispatching-parallel-agents`, `/code-review:code-review`, `/superpowers:requesting-code-review`, `/superpowers:verification-before-completion`
**Dependencies:** Tracks 1 to 7 merged (T119 and T120 may start right after Track 4)
**Scope:** import service, render benchmark, counting load test, SQL Server run, full CI, localization and accessibility checks, README, changelog, icon, marketplace file, final quickstart run and pack check.
**Files likely affected:** `src/Meresoft.Umbraco.Banners/Services/IBannerImportService.cs` and models, `tests/Meresoft.Umbraco.Banners.Benchmarks/`, `tests/.../Counting/CountingLoadTests.cs`, `tests/.../TestSetup/DatabaseSetup.cs`, `.github/workflows/ci.yml`, `Client/scripts/check-localization.mjs`, `tests/e2e/a11y.spec.ts`, `README.md`, `CHANGELOG.md`, `assets/icon.png`, `umbraco-marketplace.json`

### Tasks
1. Import: T119 (test first), T120.
2. Proof and checks in parallel: T121, T122, T126, T127.
3. Databases and CI: T124, T125.
4. Docs and release files: T128, T129.
5. Final: T130 (all 14 quickstart scenarios, performance checks, `dotnet pack` inspection, grep for em dashes and real client names or data).

### Agent Prompt
> You are working in `/Users/alekseyshturman/RiderProjects/Meresoft.Umbraco.Banners` on branch `001-banner-manager`, after all user story tracks are merged. Read `.specify/memory/constitution.md` (Distribution and Quality: the README must cover install, placing a zone, editor guide, configuration, cached pages, privacy statement, upgrade notes and how to report issues), `specs/001-banner-manager/spec.md` (FR-016, FR-020, FR-021, FR-023, SC-003 to SC-006), `quickstart.md`, `contracts/configuration-and-csharp.md` and `tasks.md`. Your job is Track 8 in `tracks.md`: tasks T119 to T122 and T124 to T130. Use `/superpowers:test-driven-development` for T119 and T120, and `/superpowers:dispatching-parallel-agents` for T121, T122, T126 and T127. For T124 the SQL Server connection string comes only from the `MB_TEST_SQLSERVER` environment variable or a CI secret, never the repository. If no SQL Server is available locally, say so and leave that run to CI. The package is generic: the README and examples use invented data and no client names. Constitution rules: invented data only, no em dashes, no secrets. Commit after each task with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; never push. Tick finished tasks in `tasks.md`. Before finishing run `/code-review:code-review` on the branch and `/superpowers:requesting-code-review`, then `/superpowers:verification-before-completion`: all tests, the benchmark, `dotnet pack` with the contents listed, and the T130 greps, and report the output.
