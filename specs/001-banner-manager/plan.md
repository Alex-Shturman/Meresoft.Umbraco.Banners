# Implementation Plan: Banner manager for Umbraco (first release)

**Branch**: `001-banner-manager` | **Date**: 2026-11-25 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-banner-manager/spec.md`

## Summary

A free, MIT licensed Umbraco 17 package, `Meresoft.Umbraco.Banners`. Editors manage zones and
banners in a new back office section, with a draft and publish workflow. Templates place a zone
with one tag helper. Banners rotate by weighted random choice inside their schedule and limits, from
an in-memory snapshot refreshed across servers on publish. Only viewable impressions count (50% for
1 s, measured by a small package script and reported with single-use view tokens); clicks go
through a redirect endpoint that only targets the stored link. Counts live in memory and are
written as daily rows once a minute by every server. The back office shows reports with a chart and
CSV export. Built as one Razor Class Library with NPoco tables from package migrations, a
Lit back office bundle and a management API with its own OpenAPI document.

## Technical Context

**Language/Version**: C# 14 on .NET 10; TypeScript 5 (back office and package script)

**Primary Dependencies**: Umbraco.Cms.Web.Common, Umbraco.Cms.Api.Management,
Umbraco.Cms.Infrastructure `[17.8.0, 18.0.0)` (research R1); `@umbraco-cms/backoffice` ^17, Lit,
Vite 7, `@hey-api/openapi-ts` (dev only). No runtime third-party libraries in the package script.

**Storage**: the site's Umbraco database (SQL Server or SQLite) through NPoco and Umbraco scopes;
six own tables (data-model.md)

**Testing**: NUnit 3.14 + NSubstitute (unit), Umbraco.Cms.Tests.Integration 17.8.x (integration,
SQLite and SQL Server), Playwright (browser), BenchmarkDotNet (render performance)

**Target Platform**: any host that runs Umbraco 17 (Windows, Linux, containers, Azure App
Service), single server or load balanced

**Project Type**: library (NuGet package: Razor Class Library with static web assets)

**Performance Goals**: zone render under 2 ms p95 with no database query (SC-003); 400 impression
reports per second per server with under 0.1% loss (SC-004)

**Constraints**: no database write in page requests; one flush pass per server per
flush interval (one short transaction per counted row); package script under 5 KB; no cookies or personal data; no open redirect

**Scale/Scope**: tens of zones, hundreds of banners, millions of impressions a month (first client:
millions of shows so far); back office: 4 views (banners, banner editor, zones, reports)

## Constitution Check

*Gate before Phase 0 and re-checked after Phase 1. Constitution v1.0.2.*

| Principle | Status | How the plan meets it |
|---|---|---|
| I. Generic Package | Pass | No client names, data or styles; import is a generic C# service (FR-016); the first client's migration lives in the site repository. Test data invented. |
| II. Umbraco Native | Pass | Umbraco 17 LTS, .NET 10; Lit back office via manifests and UUI; own tables via `PackageMigrationPlan`; Umbraco services for users, audit, media, cache refresh, background jobs, scopes; SQL Server and SQLite both tested (R2 to R8). |
| III. Privacy By Design | Pass | Daily aggregates only; the user agent is read in memory for the bot filter and never stored or logged; no cookies; view tokens carry no visitor data (R6). |
| IV. Fast Pages | Pass | Snapshot cache refreshed on publish and unpublish (R8); counting in memory, batched flush (R7); bot filter (R11); click redirect only to the stored link; client mode for cached pages. |
| V. Test First | Pass | Every task starts with a failing test (R13); integration tests run on SQLite and SQL Server; release blocked on failures. |
| VI. Stable Public Surface | Pass | Public surface listed in contracts/ (markup, public endpoints, management API, configuration, C# services, schema); SemVer from 1.0.0; MIT; README and release notes in the release checklist. |
| Distribution and Quality | Pass | NuGet id, `umbraco-marketplace` tag, `umbraco-marketplace.json`, CI build with all tests and pack (R14). |
| Development Workflow | Pass | Spec Kit flow; commit per task, no push without approval; no em dashes; review before merge. |

No violations; Complexity Tracking stays empty.

**Post-design re-check**: Pass. Two points noted for the review checklist, not violations:
a view token replayed against another server can count once more per server (documented, R6);
the management API depends on Swashbuckle types that change in Umbraco 18, isolated in one file
(R4).

## Project Structure

### Documentation (this feature)

```text
specs/001-banner-manager/
├── plan.md              # this file
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   ├── markup.md
│   ├── public-endpoints.md
│   ├── management-api.md
│   └── configuration-and-csharp.md
├── checklists/requirements.md
└── tasks.md             # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
Meresoft.Umbraco.Banners.slnx
Directory.Build.props
Directory.Packages.props
umbraco-marketplace.json
README.md
LICENSE                                   # MIT
.github/workflows/ci.yml

src/Meresoft.Umbraco.Banners/             # the NuGet package (Razor Class Library)
├── Composing/                            # BannersComposer, DI registration, options validation
├── Configuration/                        # BannersOptions (Meresoft:Banners)
├── Domain/                               # Zone, Banner, BannerVersion, states, validation rules
├── Persistence/
│   ├── Dtos/                             # runtime NPoco DTOs
│   ├── Migrations/                       # PackageMigrationPlan, steps, frozen schema DTOs
│   └── Repositories/                     # zone, banner, statistics, requested zone
├── Services/                             # public C# services (contracts/configuration-and-csharp.md)
├── Caching/                              # live snapshot, builder, cache refresher, media handler
├── Selection/                            # weighted selector, schedule and limit filters
├── Counting/                             # counter buffer, flush job, stopping handler, bot matcher
├── Tokens/                               # view token protector and replay set
├── Delivery/                             # public controllers (click, seen, zone), route convention
├── Rendering/                            # BannerZoneTagHelper, BannerZoneViewComponent, HTML writer
├── ManagementApi/                        # controllers, models, section policy, Swagger document
├── Client/                               # back office source (Vite, Lit, TypeScript)
│   ├── public/umbraco-package.json
│   ├── src/                              # section, workspaces, report chart, localization (en)
│   └── script/                           # package script source (banners.ts)
└── wwwroot/
    ├── App_Plugins/MeresoftBanners/      # built back office bundle (generated)
    └── meresoft-banners/banners.js       # built package script (generated)

samples/Meresoft.Umbraco.Banners.Sample/  # Umbraco 17 site on SQLite for manual and e2e tests

tests/
├── Meresoft.Umbraco.Banners.Tests.Unit/
├── Meresoft.Umbraco.Banners.Tests.Integration/
├── Meresoft.Umbraco.Banners.Benchmarks/
└── e2e/                                  # Playwright (TypeScript)
```

**Structure Decision**: one package project, because Umbraco installs packages as one NuGet
reference and the public surface is small; folders separate the layers. Generated assets are built
by MSBuild targets that call the Vite builds before pack, and are not committed. Tests are split by
speed (unit, integration, browser), with a sample site shared by manual checks and Playwright.

## Implementation order (input for /speckit-tasks)

1. **Setup**: repository files, solution, props, CI skeleton, sample site, empty composer, Vite
   projects.
2. **Foundation**: options, migrations (all tables plus the admin grant), DTOs and repositories,
   section manifest and policy, Swagger document, localization base. Blocks every story.
3. **US1 (P1)**: zones and image banners with draft and publish, snapshot and cache refresher,
   tag helper and view component, requested zones, back office banner and zone workspaces.
4. **US2 (P1)**: weights, schedule, show count, multi-zone assignments, selection tests.
5. **US3 (P1)**: package script, view tokens, seen and click endpoints, bot filter, counter buffer
   and flush, reports, chart and CSV.
6. **US6 (P2)**: HTML banners and HTML permission, audit history, 403 tests, fresh install grant
   test.
7. **US4 (P2)**: client mode, delivery endpoint, cached page e2e.
8. **US5 (P3)**: limits, totals reload, "limit reached" flag.
9. **Polish**: import service, benchmarks and load test, README, release notes, marketplace file,
   pack check, size check, accessibility check of the back office views.

## Complexity Tracking

No constitution violations to justify.
