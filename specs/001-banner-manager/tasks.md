---

description: "Task list for the banner manager package (001)"
---

# Tasks: Banner manager for Umbraco (first release)

**Input**: Design documents from `specs/001-banner-manager/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Required. Constitution V (Test First) says every behaviour starts with a failing
automated test, so each story lists its tests first. Write them, see them fail, then implement.
Tests use invented data only.

**Organization**: tasks are grouped by user story, in the order of plan.md: Setup, Foundation, US1,
US2, US3, US6, US4, US5, Polish. US6 comes before US4 because the HTML banner type it adds is also
delivered by client mode.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an unfinished task)
- **[Story]**: the user story the task belongs to (US1 to US6)
- Every task names its files

## Path conventions

- Package: `src/Meresoft.Umbraco.Banners/` (one Razor Class Library)
- Back office and package script source: `src/Meresoft.Umbraco.Banners/Client/`
- Unit tests: `tests/Meresoft.Umbraco.Banners.Tests.Unit/`
- Integration tests: `tests/Meresoft.Umbraco.Banners.Tests.Integration/`
- Benchmarks: `tests/Meresoft.Umbraco.Banners.Benchmarks/`
- Browser tests: `tests/e2e/`
- Sample site: `samples/Meresoft.Umbraco.Banners.Sample/`
- C# namespaces follow folders under the root namespace `Meresoft.Umbraco.Banners`

---

## Phase 1: Setup (shared infrastructure)

**Purpose**: repository, solution, projects, build of the client assets, sample site, CI skeleton.

- [x] T001 Create root files: `LICENSE` (MIT, copyright Meresoft), `.gitignore` (dotnet and node, plus the generated `src/Meresoft.Umbraco.Banners/wwwroot/App_Plugins/MeresoftBanners/` and `src/Meresoft.Umbraco.Banners/wwwroot/meresoft-banners/`), `.editorconfig`, and a stub `README.md`
- [x] T002 Create `Directory.Build.props` (`net10.0`, `LangVersion` 14, `Nullable` enable, `TreatWarningsAsErrors`, `Deterministic`, SourceLink) and `Directory.Packages.props` with central versions: `Umbraco.Cms.Web.Common`, `Umbraco.Cms.Api.Management`, `Umbraco.Cms.Infrastructure` at `[17.8.0, 18.0.0)`, `Umbraco.Cms.Tests.Integration` 17.8.x, NUnit 3.14, NUnit3TestAdapter, Microsoft.NET.Test.Sdk, NSubstitute, Microsoft.Extensions.TimeProvider.Testing, BenchmarkDotNet, Microsoft.SourceLink.GitHub
- [x] T003 Create the package project `src/Meresoft.Umbraco.Banners/Meresoft.Umbraco.Banners.csproj` (`Microsoft.NET.Sdk.Razor`, `StaticWebAssetBasePath` `/`, `PackageId` `Meresoft.Umbraco.Banners`, MIT licence expression, tags `umbraco umbraco-marketplace banners ads`, packed README and icon; a plain placeholder `assets/icon.png` is added now so pack works, replaced in T129) with the empty folders of plan.md (Composing, Configuration, Domain, Persistence/Dtos, Persistence/Migrations, Persistence/Repositories, Services, Caching, Selection, Counting, Tokens, Delivery, Rendering, ManagementApi, Client, wwwroot)
- [x] T004 Create `Meresoft.Umbraco.Banners.slnx` with the package, sample, unit, integration and benchmark projects
- [x] T005 [P] Create `tests/Meresoft.Umbraco.Banners.Tests.Unit/Meresoft.Umbraco.Banners.Tests.Unit.csproj` (NUnit, NSubstitute, TimeProvider testing, reference to the package) with one passing placeholder test in `tests/Meresoft.Umbraco.Banners.Tests.Unit/SmokeTests.cs`
- [x] T006 [P] Create `tests/Meresoft.Umbraco.Banners.Tests.Integration/Meresoft.Umbraco.Banners.Tests.Integration.csproj` with `appsettings.Tests.json` (SQLite by default) and `tests/Meresoft.Umbraco.Banners.Tests.Integration/TestSetup/DatabaseSetup.cs` that switches to SQL Server (`Tests:Database:DatabaseType=SqlServer`) when the environment variable `MB_TEST_SQLSERVER` holds a connection string; tests for that run carry `[Category("SqlServer")]`; no connection string in the repository
- [x] T007 [P] Create `tests/Meresoft.Umbraco.Banners.Benchmarks/Meresoft.Umbraco.Banners.Benchmarks.csproj` (BenchmarkDotNet console, reference to the package) with an empty `Program.cs`
- [x] T008 [P] Create the sample site `samples/Meresoft.Umbraco.Banners.Sample/` (Umbraco 17, SQLite, unattended install; admin credentials read from user-secrets or environment variables, never committed) with a project reference to the package, `@addTagHelper *, Meresoft.Umbraco.Banners` in `Views/_ViewImports.cshtml`, and document types, templates and invented content for: a home page with zone `home-top`, a long page with zone `long-bottom` below the fold, and a cached page (output cache 60 s) with zone `cached-top`; plus `samples/Meresoft.Umbraco.Banners.Sample/SampleUsersComposer.cs` that creates an invented editor user without the Banners section when missing (credentials from environment variables)
- [x] T009 [P] Create the back office client `src/Meresoft.Umbraco.Banners/Client/` like the `umbraco-extension` template: `package.json` (Vite 7, TypeScript 5, Lit, `@umbraco-cms/backoffice` ^17 as dev dependency and Vite external, `@hey-api/openapi-ts` dev, eslint), `tsconfig.json`, `vite.config.ts` writing to `../wwwroot/App_Plugins/MeresoftBanners/`, `public/umbraco-package.json` with one `bundle` extension, `src/bundle.manifests.ts` exporting an empty list
- [x] T010 [P] Create the package script build: `src/Meresoft.Umbraco.Banners/Client/script/banners.ts` (empty IIFE) and `src/Meresoft.Umbraco.Banners/Client/vite.script.config.ts` (library build, IIFE, minified, no dependencies) writing `../wwwroot/meresoft-banners/banners.js`; `npm run build` runs both Vite builds
- [x] T011 Add an MSBuild target `BuildClientAssets` in `src/Meresoft.Umbraco.Banners/Meresoft.Umbraco.Banners.csproj` that runs `npm ci` and `npm run build` in `Client/` before build and pack, with Inputs and Outputs so it is incremental and skippable with `-p:SkipClientBuild=true`
- [x] T012 [P] Create `tests/e2e/` with `package.json` (Playwright), `tsconfig.json` and `playwright.config.ts` whose `webServer` starts the sample site; credentials come from environment variables
- [x] T013 [P] Create `.github/workflows/ci.yml` skeleton: checkout, .NET 10 and Node 22, `dotnet build` of the solution, unit tests
- [x] T014 Create `src/Meresoft.Umbraco.Banners/Composing/BannersComposer.cs` (`IComposer`) calling `builder.AddMeresoftBanners()` from `src/Meresoft.Umbraco.Banners/Composing/BannersBuilderExtensions.cs` (empty for now); check that the sample site starts with the package installed

**Checkpoint**: `dotnet build Meresoft.Umbraco.Banners.slnx` and `dotnet test` of the unit project pass; the sample site starts.

---

## Phase 2: Foundation (blocking prerequisites)

**Purpose**: options, domain rules, all tables and the administrator grant, repositories for zones
and banners, section access policy, management API base and OpenAPI document, back office section
and localization base, audit writer. No user story starts before this phase is done.

### Tests for the foundation (write first, they must fail)

- [x] T015 [P] Unit tests for options in `tests/Meresoft.Umbraco.Banners.Tests.Unit/Configuration/BannersOptionsValidatorTests.cs`: `PublicPath` "starts with `/`, no trailing `/`, must not start with `/umbraco`" (default `/_banners`); `FlushInterval` "5 seconds to 1 minute" (default 1 minute); `ViewTokenLifetime` "1 to 60 minutes" (default 10 minutes); `CacheSafetyReloadInterval` "1 to 60 minutes; 0 disables" (default 5 minutes); `DeliveryEndpointEnabled` default true; `Bots:AdditionalUserAgentPatterns` default empty; every failure message names the key
- [x] T016 [P] Unit tests for zone rules in `tests/Meresoft.Umbraco.Banners.Tests.Unit/Domain/ZoneValidatorTests.cs`: Alias "lowercase `a-z`, `0-9`, `-`, `_`; 1 to 100 chars; starts with a letter" and normalised (trimmed, lowercased); Name required, max 255; Description max 1000, nullable; ShowCount "1 to 10, default 1"; ImageWidth and ImageHeight "1 to 4000", nullable; RenderMode "0 = Server, 1 = Client"
- [x] T017 [P] Unit tests for banner version rules in `tests/Meresoft.Umbraco.Banners.Tests.Unit/Domain/BannerVersionValidatorTests.cs`: MediaKey "required for Image banners"; Html "required for Html banners"; LinkUrl "required for Image banners; absolute `http` or `https` URL, otherwise save is refused", max 2048; AltText "required for Image banners", max 255; Title max 255, nullable; Weight "1 to 100, default 1"; StartUtc and EndUtc "when both are set, End > Start"; ImpressionLimit and ClickLimit ">= 1", nullable; ClickLimit "must be null for Html banners" (FR-006); Name (banner) required, max 255; Type "fixed after creation"
- [x] T018 [P] Unit tests for derived states and flags in `tests/Meresoft.Umbraco.Banners.Tests.Unit/Domain/BannerStateCalculatorTests.cs`, one case per row of data-model.md "Banner publish state" (Draft, Published, Published with unpublished changes using `Draft.SavedDate > PublishDate`, Unpublished) and "Banner runtime flags" (Not placed, Image missing, Scheduled `now < StartUtc`, Expired `now >= EndUtc`, Limit reached), with a fake `TimeProvider`
- [x] T019 [P] Integration test in `tests/Meresoft.Umbraco.Banners.Tests.Integration/Persistence/MigrationTests.cs`: a fresh install creates the six tables `meresoftBannersZone`, `meresoftBannersBanner`, `meresoftBannersBannerVersion`, `meresoftBannersZoneAssignment`, `meresoftBannersDailyStat`, `meresoftBannersRequestedZone` with their primary keys, the unique indexes on Zone.Alias, Zone.Key, Banner.Key and (BannerId, Kind), the index (BannerId, Day) on DailyStat, and no foreign key on DailyStat; running the plan again changes nothing; no Umbraco core table changes (FR-019) (SQL Server unverified locally, covered by the CI job `integration-tests-sqlserver`)
- [x] T020 [P] Integration test in `tests/Meresoft.Umbraco.Banners.Tests.Integration/Persistence/AdminGrantMigrationTests.cs`: after a fresh install the Administrators group has section `Meresoft.Banners.Section` and the fallback permission `Meresoft.Banners.Html`, and no other group has either; when an administrator removes them and a later migration step runs, they stay removed (FR-017, clarification 5)
- [x] T021 [P] Integration tests in `tests/Meresoft.Umbraco.Banners.Tests.Integration/Persistence/ZoneRepositoryTests.cs` and `tests/Meresoft.Umbraco.Banners.Tests.Integration/Persistence/BannerRepositoryTests.cs`: create, read by key and alias, update, delete; unique alias; at most one Draft and one Live version per banner; assignments belong to a version; deleting a banner cascades to versions and assignments but leaves DailyStat rows; "copy draft over live" copies fields and assignments in one scope; zone delete is blocked by a version assignment (no cascade)
- [x] T022 [P] Integration test in `tests/Meresoft.Umbraco.Banners.Tests.Integration/ManagementApi/SectionAccessTests.cs` with a test-only controller derived from the package base controller: anonymous gets 401, a user whose groups lack `Meresoft.Banners.Section` gets 403, an administrator gets 200 (R4 policy, US6 scenario 1)
- [x] T023 [P] Integration test in `tests/Meresoft.Umbraco.Banners.Tests.Integration/ManagementApi/OpenApiDocumentTests.cs`: the OpenAPI document `meresoft-banners` is served by the Umbraco swagger endpoint, contains only package operations and declares back office bearer security

### Implementation for the foundation

- [x] T024 [P] Implement `src/Meresoft.Umbraco.Banners/Configuration/BannersOptions.cs` (with nested `BotOptions`) and `src/Meresoft.Umbraco.Banners/Configuration/BannersOptionsValidator.cs` (`IValidateOptions<BannersOptions>`); bind section `Meresoft:Banners` with `ValidateOnStart` in `src/Meresoft.Umbraco.Banners/Composing/BannersBuilderExtensions.cs` (makes T015 pass)
- [x] T025 [P] Implement domain types in `src/Meresoft.Umbraco.Banners/Domain/`: `BannerZone.cs`, `Banner.cs`, `BannerVersion.cs`, `BannerType.cs` (Image = 0, Html = 1), `ZoneRenderMode.cs` (Server = 0, Client = 1), `BannerVersionKind.cs` (Draft = 0, Live = 1), `BannerState.cs`, `BannerFlags.cs` (flags enum), `BannerOperationStatus.cs` (Success, NotFound, InvalidData, DuplicateAlias, ZoneInUse, ChangedByAnotherUser, NoZoneAssigned, TypeChangeNotAllowed), `AliasNormalizer.cs`
- [x] T026 Implement `src/Meresoft.Umbraco.Banners/Domain/ZoneValidator.cs`, `src/Meresoft.Umbraco.Banners/Domain/BannerVersionValidator.cs` (field errors keyed by field name, reused by the API and the import service) and `src/Meresoft.Umbraco.Banners/Domain/BannerStateCalculator.cs` (depends on T025; makes T016 to T018 pass)
- [x] T027 Create frozen schema DTOs in `src/Meresoft.Umbraco.Banners/Persistence/Migrations/Schema/V1/` (`ZoneSchemaDto.cs`, `BannerSchemaDto.cs`, `BannerVersionSchemaDto.cs`, `ZoneAssignmentSchemaDto.cs`, `DailyStatSchemaDto.cs`, `RequestedZoneSchemaDto.cs`) with NPoco attributes matching data-model.md exactly: string lengths 100, 255, 1000, 2048; `Html` as text; nullable columns as listed; `RenderMode`, `Type` and `Kind` as tinyint; limits and counters as bigint; `Day` as date; FK BannerVersion to Banner with cascade delete, FK ZoneAssignment to BannerVersion with cascade delete, FK ZoneAssignment to Zone without cascade; no FK on DailyStat
- [x] T028 Implement `src/Meresoft.Umbraco.Banners/Persistence/Migrations/BannersMigrationPlan.cs` (`PackageMigrationPlan` named `Meresoft.Umbraco.Banners`) and `src/Meresoft.Umbraco.Banners/Persistence/Migrations/V1_0_0/CreateTables.cs` (`AsyncMigrationBase`, `Create.Table<...SchemaDto>()` guarded by `TableExists`) (depends on T027; makes T019 pass)
- [x] T029 Implement `src/Meresoft.Umbraco.Banners/Persistence/Migrations/V1_0_0/GrantAdministratorsAccess.cs` as the second step of the first version: `IUserGroupService.GetAsync(Constants.Security.AdminGroupKey)`, `AddAllowedSection("Meresoft.Banners.Section")`, add `Meresoft.Banners.Html` to `Permissions`, `UpdateAsync(group, Constants.Security.SuperUserKey)`; section alias and verb come from `src/Meresoft.Umbraco.Banners/BannersConstants.cs` (depends on T028; makes T020 pass)
- [x] T030 [P] Create runtime NPoco DTOs in `src/Meresoft.Umbraco.Banners/Persistence/Dtos/`: `ZoneDto.cs`, `BannerDto.cs`, `BannerVersionDto.cs`, `ZoneAssignmentDto.cs`, `DailyStatDto.cs`, `RequestedZoneDto.cs` (separate from the frozen schema DTOs)
- [x] T031 Implement `src/Meresoft.Umbraco.Banners/Persistence/Repositories/IZoneRepository.cs` and `ZoneRepository.cs` with `Sql<ISqlContext>` (portable to SQL Server and SQLite), including "banners assigned to zone" for the delete check (depends on T030) (SQL Server unverified locally, covered by the CI job `integration-tests-sqlserver`)
- [x] T032 Implement `src/Meresoft.Umbraco.Banners/Persistence/Repositories/IBannerRepository.cs` and `BannerRepository.cs`: banner with Draft and Live versions and their assignments, paged list with filters, copy draft over live, delete (depends on T030; with T031 makes T021 pass) (SQL Server unverified locally, covered by the CI job `integration-tests-sqlserver`)
- [x] T033 Implement the section policy `Meresoft.Banners.SectionAccess` in `src/Meresoft.Umbraco.Banners/ManagementApi/Authorization/SectionAccessRequirement.cs` and `SectionAccessHandler.cs` (`MustSatisfyRequirementAuthorizationHandler<SectionAccessRequirement>`, user from `IAuthorizationHelper.TryGetUmbracoUser`, checks `AllowedSections` contains `Meresoft.Banners.Section`), registered with the OpenIddict validation scheme in `BannersBuilderExtensions.cs`
- [x] T034 Implement `src/Meresoft.Umbraco.Banners/ManagementApi/BannersManagementApiControllerBase.cs` (`ManagementApiControllerBase`, `[VersionedApiBackOfficeRoute("meresoft-banners")]`, `[ApiExplorerSettings(GroupName = ...)]`, `[MapToApi("meresoft-banners")]`, `[Authorize(Policy = "Meresoft.Banners.SectionAccess")]`) and `src/Meresoft.Umbraco.Banners/ManagementApi/OperationStatusResults.cs` mapping `BannerOperationStatus` to RFC 9457 problem details (400 with `errors` per field, 404, 409 `changed-by-another-user`, 409 `zone-in-use`, 409 alias taken) (depends on T033; with T033 makes T022 pass)
- [x] T035 Implement `src/Meresoft.Umbraco.Banners/ManagementApi/OpenApi/BannersSwaggerConfiguration.cs` (`IConfigureOptions<SwaggerGenOptions>`, document `meresoft-banners`, operation filter from `BackOfficeSecurityRequirementsOperationFilterBase`); this is the only file that uses Swashbuckle types (R4, Umbraco 18 note) (makes T023 pass)
- [x] T036 [P] Implement `src/Meresoft.Umbraco.Banners/Services/Audit/BannerAuditWriter.cs` over `IAuditService.AddAsync` with entity types `meresoft-banner` and `meresoft-banner-zone`, audit types New, Save, Publish, Unpublish, Copy, Delete; comments hold ids and action names only (R12, FR-018)
- [x] T037 [P] Add the section to the back office: `src/Meresoft.Umbraco.Banners/Client/src/section/manifests.ts` (type `section`, alias `Meresoft.Banners.Section`, condition `Umb.Condition.SectionUserPermission`; section sidebar menu with Banners, Zones and Reports items using `Umb.Condition.SectionAlias`), `src/Meresoft.Umbraco.Banners/Client/src/lang/en.ts` plus its `localization` manifest (FR-021; every UI text from now on is a localization key), both registered in `src/bundle.manifests.ts`
- [x] T038 [P] Add API client generation: `src/Meresoft.Umbraco.Banners/Client/openapi-ts.config.ts`, npm script `generate:api` reading the `meresoft-banners` document from the running sample site into `src/Meresoft.Umbraco.Banners/Client/src/api/` (committed, regenerated whenever the contract changes), and `src/Meresoft.Umbraco.Banners/Client/src/api/client-setup.ts` taking the token and base URL from `UMB_AUTH_CONTEXT`

**Checkpoint**: migrations, grant, repositories, policy and OpenAPI tests pass on SQLite; the Banners section shows (empty) for an administrator in the sample site.

---

## Phase 3: User Story 1 - Show a banner in a zone (Priority: P1) 🎯 MVP

**Goal**: editors create zones and image banners with draft and publish; templates show the live banner through the tag helper or view component; unknown aliases are listed as "placed but not set up".

**Independent Test**: on the sample site, place a zone, create one image banner, publish: the page shows the image with alt text and a link (quickstart scenarios 1 to 3, 5 and 14, and scenario 4 without the click address). Selection is uniform random in this story; weights arrive in US2. The click address and the view token arrive in US3, so in this story the link points at the stored URL.

### Tests for User Story 1 (write first, they must fail)

- [ ] T039 [P] [US1] Integration tests in `tests/Meresoft.Umbraco.Banners.Tests.Integration/Services/BannerZoneServiceTests.cs`: create, update, rename, change alias, delete; duplicate alias gives DuplicateAlias; delete refused with ZoneInUse and the assigned banner keys while any draft or live version is assigned (FR-007); creating a zone removes the matching requested zone; stale `updateDate` gives ChangedByAnotherUser; each write adds an audit entry
- [ ] T040 [P] [US1] Integration tests in `tests/Meresoft.Umbraco.Banners.Tests.Integration/Services/BannerServiceTests.cs` for image banners: create gives Draft; save draft leaves the live version and the snapshot unchanged and gives "Published with unpublished changes" (US1 scenario 5); publish copies draft fields and assignments to Live and sets `IsPublished` and `PublishDate`; publish refused when the draft is invalid or has no zone (NoZoneAssigned); unpublish keeps the Live row; copy creates a Draft with the draft fields and assignments and no statistics; delete keeps DailyStat rows; stale `updateDate` gives ChangedByAnotherUser; changing Type gives TypeChangeNotAllowed
- [ ] T041 [P] [US1] Unit tests in `tests/Meresoft.Umbraco.Banners.Tests.Unit/Caching/LiveSnapshotBuilderTests.cs`: only published banners with a Live version and an assignment become candidates; the image URL is built once with `IImageUrlGenerator` using the zone width and height; a banner whose media item is missing is left out and reported as "image missing"; zones are keyed by normalised alias
- [ ] T042 [P] [US1] Unit tests in `tests/Meresoft.Umbraco.Banners.Tests.Unit/Rendering/BannerZoneHtmlWriterTests.cs` against contracts/markup.md server mode: `<div class="mb-zone {class}" data-mbz="{alias}" data-mb-base="{PublicPath}">`, `<div class="mb-banner" data-mbb="{id}">`, link with `rel="sponsored noopener"`, `target="_blank"` only when OpenInNewWindow, `title` only when set, `<img>` with alt, width, height, `loading="lazy" decoding="async"`, all values HTML encoded; nothing at all when no banner and no fallback; fallback content when no banner or unknown zone; the `<script src="/meresoft-banners/banners.js" defer>` tag written once per request even with several zones
- [ ] T043 [P] [US1] Integration tests in `tests/Meresoft.Umbraco.Banners.Tests.Integration/Rendering/ZoneRenderingTests.cs`: after publish the tag helper and the `BannerZone` view component render the banner; an NPoco command counter shows no database command during render (SC-003); two zones with the same alias on one page render independently; an unknown alias renders nothing and is recorded as requested; after the flush job runs, `meresoftBannersRequestedZone` holds the alias with FirstSeen and LastSeen
- [ ] T044 [P] [US1] Integration tests in `tests/Meresoft.Umbraco.Banners.Tests.Integration/Caching/CacheRefreshTests.cs`: publish, unpublish, delete and zone changes send the `BannersCacheRefresher` payload and the snapshot changes within 5 seconds without restart (US1 scenario 4); a media delete notification for a referenced item drops the banner and sets "image missing"; the safety reload rebuilds the snapshot and is off when the interval is 0
- [ ] T045 [P] [US1] Integration tests in `tests/Meresoft.Umbraco.Banners.Tests.Integration/ManagementApi/ZonesApiTests.cs` and `tests/Meresoft.Umbraco.Banners.Tests.Integration/ManagementApi/BannersApiTests.cs`: every row of the Zones and Banners tables in contracts/management-api.md (status codes, Location on 201, problem detail types, list paging with `take` max 100, filters `zone`, `state`, `type`), requested zones list and dismiss
- [ ] T060 [P] [US1] Browser test `tests/e2e/us1-show-banner.spec.ts`, covering the zones and banner views of T058 and T059: quickstart scenarios 1 to 3, 5 and 14 on the sample site with invented banner data, and scenario 4 up to "the home page shows the banner within 5 seconds and its link goes to the stored URL" (the click address arrives in US3, T079)

### Implementation for User Story 1

- [x] T046 [P] [US1] Create service models in `src/Meresoft.Umbraco.Banners/Services/Models/`: `BannerZoneModel.cs`, `BannerCreateModel.cs`, `BannerDraftModel.cs` (mediaKey, html, linkUrl, openInNewWindow, altText, title, weight, startUtc, endUtc, impressionLimit, clickLimit, zoneKeys), `BannerQuery.cs`, `BannerListItem.cs`
- [ ] T047 [US1] Implement `src/Meresoft.Umbraco.Banners/Services/IBannerZoneService.cs` and `BannerZoneService.cs` (signatures from contracts/configuration-and-csharp.md; scope per call, validation, audit, cache refresh, requested zone removal) (makes T039 pass)
- [ ] T048 [US1] Implement `src/Meresoft.Umbraco.Banners/Services/IBannerService.cs` and `BannerService.cs` (GetAsync, GetPagedAsync with state and flags, CreateAsync, SaveDraftAsync, PublishAsync, UnpublishAsync, CopyAsync, DeleteAsync; scope per call; audit; cache refresh on publish, unpublish and delete) (makes T040 pass)
- [ ] T049 [US1] Implement the snapshot in `src/Meresoft.Umbraco.Banners/Caching/`: `LiveSnapshot.cs` (immutable: zones by alias with settings and candidates holding id, type, image URL, width, height, alt, title, link, new window, weight, schedule, limits), `LiveSnapshotBuilder.cs` (media resolved with `IPublishedMediaCache.GetByIdAsync(Guid)` at build time), `ILiveSnapshotAccessor.cs` and `LiveSnapshotAccessor.cs` (atomic swap, first build on `UmbracoApplicationStartingNotification` or lazily) (makes T041 pass)
- [ ] T050 [US1] Implement cache refresh in `src/Meresoft.Umbraco.Banners/Caching/`: `BannersCacheRefresher.cs` (`JsonCacheRefresherBase<BannersCacheRefresherNotification, BannersCacheRefresherPayload>`, fixed unique id), `BannersCacheRefresherNotification.cs`, `BannersCacheRefresherPayload.cs`, `SnapshotRebuildHandler.cs`, `BannersDistributedCacheExtensions.cs` (`RefreshByPayload`), `MediaChangedHandler.cs` (`MediaCacheRefresherNotification`), `SnapshotSafetyReloadJob.cs` (`RecurringBackgroundJobBase`, period `CacheSafetyReloadInterval`, disabled at 0) (makes T044 pass)
- [ ] T051 [US1] Implement `src/Meresoft.Umbraco.Banners/Selection/IBannerSelector.cs`, `Selection/SelectedBanner.cs` and `Selection/BannerSelector.cs`: reads the snapshot only, picks up to ShowCount different candidates uniformly at random (weights in T068)
- [ ] T052 [US1] Implement rendering in `src/Meresoft.Umbraco.Banners/Rendering/`: `BannerZoneHtmlWriter.cs` (server mode markup with `data-mb-base` from `PublicPath`, fallback, once-per-request script flag in `HttpContext.Items`), `PackageScript.cs` (the constant path `/meresoft-banners/banners.js`), `BannerZoneTagHelper.cs` (`<banner-zone alias class>`, child content as fallback), `BannerZoneViewComponent.cs` (`alias`, `cssClass`) (makes T042 pass)
- [ ] T053 [US1] Implement requested zones: `src/Meresoft.Umbraco.Banners/Rendering/RequestedZoneRecorder.cs` (in-memory set of normalised valid aliases with first and last seen), `src/Meresoft.Umbraco.Banners/Persistence/Repositories/IRequestedZoneRepository.cs` and `RequestedZoneRepository.cs` (upsert keeping FirstSeen, delete by alias), and `src/Meresoft.Umbraco.Banners/Counting/BannersFlushJob.cs` (`RecurringBackgroundJobBase`, all server roles, `Period` = `FlushInterval`, `Delay` 30 s) that for now writes requested zones, skipping aliases that exist as zones (makes T043 pass)
- [ ] T054 [US1] Implement zone controllers in `src/Meresoft.Umbraco.Banners/ManagementApi/Zones/` (`ListZonesController.cs`, `GetZoneController.cs`, `CreateZoneController.cs`, `UpdateZoneController.cs`, `DeleteZoneController.cs`, models in `ManagementApi/Zones/Models/`) and requested zone controllers in `src/Meresoft.Umbraco.Banners/ManagementApi/RequestedZones/` (list, dismiss), with `activeBannerCount` from the snapshot
- [ ] T055 [US1] Implement banner controllers in `src/Meresoft.Umbraco.Banners/ManagementApi/Banners/` (`ListBannersController.cs`, `GetBannerController.cs`, `CreateBannerController.cs`, `UpdateBannerController.cs`, `PublishBannerController.cs`, `UnpublishBannerController.cs`, `CopyBannerController.cs`, `DeleteBannerController.cs`, models in `ManagementApi/Banners/Models/`; dates in UTC) (with T054 makes T045 pass)
- [ ] T056 [US1] Register services, repositories, snapshot, refresher, handlers, jobs, selector, recorder and tag helper in `src/Meresoft.Umbraco.Banners/Composing/BannersBuilderExtensions.cs`
- [ ] T057 [US1] Regenerate the API client into `src/Meresoft.Umbraco.Banners/Client/src/api/` with `npm run generate:api`
- [ ] T058 [P] [US1] Build the zones views in `src/Meresoft.Umbraco.Banners/Client/src/zones/`: `zones-list-view.element.ts` (zones table and a "Placed but not set up" list with one-click create that fills the alias, and dismiss), `zone-editor.element.ts` (all zone fields with validation messages, a warning when the alias changes, delete showing the assigned banners on 409), `manifests.ts`; UUI components and localization keys only
- [ ] T059 [P] [US1] Build the banner views in `src/Meresoft.Umbraco.Banners/Client/src/banners/`: `banners-list-view.element.ts` (state badges, flags Not placed and Image missing, filters zone, state and type, paging), `banner-editor.element.ts` (name, media picker for the image, link, new window, alt text, title, zones multi-select; Save, Publish, Unpublish, Copy, Delete; state shown; stale edit message on 409), `manifests.ts`

**Checkpoint**: US1 works on its own: an editor can create a zone and a banner, publish it and see it on the page; this is the MVP.

---

## Phase 4: User Story 2 - Rotation and scheduling (Priority: P1)

**Goal**: weighted random rotation without repeats, ShowCount banners per view, start and end times, one banner in several zones.

**Independent Test**: weights 1 and 3 over 4,000 seeded draws give shares within 5 points of 25% and 75%; a banner with a past end date never shows (quickstart scenario 6).

### Tests for User Story 2 (write first, they must fail)

- [ ] T061 [P] [US2] Unit tests in `tests/Meresoft.Umbraco.Banners.Tests.Unit/Selection/WeightedSamplerTests.cs` with a seeded random source: weights 1 and 3, ShowCount 1, 4,000 draws, shares within 5 points of 25% and 75% (US2 scenario 1); ShowCount 3 of 5 active gives 3 different banners every time (US2 scenario 3); ShowCount 3 with 1 active gives 1; no candidates gives an empty list
- [ ] T062 [P] [US2] Unit tests in `tests/Meresoft.Umbraco.Banners.Tests.Unit/Selection/ScheduleFilterTests.cs` with a fake `TimeProvider`: active inside [StartUtc, EndUtc), not active at `EndUtc` or before `StartUtc`, open ends when null (US2 scenario 2)
- [ ] T063 [P] [US2] Integration tests in `tests/Meresoft.Umbraco.Banners.Tests.Integration/Services/MultiZoneAssignmentTests.cs`: a banner assigned to two zones is a candidate in both with the same weight (clarification 1); changing the draft's zones does not change the site until publish; removing a zone from a banner keeps the DailyStat rows of that zone; an unpublished banner is never selected and keeps its statistics (US2 scenario 4)
- [ ] T068 [P] [US2] Browser test `tests/e2e/us2-rotation.spec.ts`, covering the weight, schedule and show count fields of T067: quickstart scenario 6 and an expired banner that never shows

### Implementation for User Story 2

- [ ] T064 [P] [US2] Implement `src/Meresoft.Umbraco.Banners/Selection/IRandomSource.cs` (default over `Random.Shared`) and `src/Meresoft.Umbraco.Banners/Selection/WeightedSampler.cs` (draw by weight, remove, repeat ShowCount times) (makes T061 pass)
- [ ] T065 [P] [US2] Implement `src/Meresoft.Umbraco.Banners/Selection/ScheduleFilter.cs` with `TimeProvider`, evaluated per request against snapshot candidates (R8) (makes T062 pass)
- [ ] T066 [US2] Use `WeightedSampler` and `ScheduleFilter` in `src/Meresoft.Umbraco.Banners/Selection/BannerSelector.cs` and make sure `LiveSnapshotBuilder.cs` adds a banner to every assigned zone (makes T063 pass)
- [ ] T067 [P] [US2] Add weight, start and end fields to `src/Meresoft.Umbraco.Banners/Client/src/banners/banner-editor.element.ts` (times entered and shown in the user's time zone, sent in UTC, helper in `src/Meresoft.Umbraco.Banners/Client/src/shared/date-time.ts`), the Scheduled and Expired flags to `banners-list-view.element.ts`, and the show count field to `src/Meresoft.Umbraco.Banners/Client/src/zones/zone-editor.element.ts`

**Checkpoint**: US1 and US2 work together; rotation and schedules follow the weights and dates.

---

## Phase 5: User Story 3 - Impression and click statistics (Priority: P1)

**Goal**: viewable impressions reported by the package script with single-use view tokens, clicks through the redirect endpoint, bot filter, in-memory counting with batched flush, reports with chart and CSV.

**Independent Test**: bring a banner into view 100 times and click it 3 times; after the flush the report for today shows 100 impressions, 3 clicks and 3.0% (quickstart scenarios 4, 7, 8, 10, 11).

### Tests for User Story 3 (write first, they must fail)

- [ ] T069 [P] [US3] Unit tests in `tests/Meresoft.Umbraco.Banners.Tests.Unit/Tokens/ViewTokenProtectorTests.cs`: round trip of format version, zone id, banner ids and a 96 bit nonce; a changed byte fails; an expired token fails; two tokens never share a nonce; the payload holds no visitor data
- [ ] T070 [P] [US3] Unit tests in `tests/Meresoft.Umbraco.Banners.Tests.Unit/Tokens/ReplaySetTests.cs`: the first (nonce, bannerId) is accepted, the second is refused, a different banner id with the same nonce is accepted, entries expire with the token lifetime (fake `TimeProvider`)
- [ ] T071 [P] [US3] Unit tests in `tests/Meresoft.Umbraco.Banners.Tests.Unit/Counting/BotMatcherTests.cs`: shipped list matches common crawlers, link previewers, uptime monitors, headless browsers and an empty user agent; current desktop and mobile browser user agents pass; `Bots:AdditionalUserAgentPatterns` adds case-insensitive substrings (FR-013)
- [ ] T072 [P] [US3] Unit tests in `tests/Meresoft.Umbraco.Banners.Tests.Unit/Counting/CounterBufferTests.cs`: parallel increments from many threads give exact totals per (Day, BannerId, ZoneId); swap returns all counts and leaves an empty buffer; restoring a failed batch merges it back without loss
- [ ] T073 [P] [US3] Unit tests in `tests/Meresoft.Umbraco.Banners.Tests.Unit/Delivery/SeenBodyParserTests.cs`: lines `{token} {bannerId}`, at most 50 lines (extra lines ignored), malformed lines ignored, banner id must be an integer
- [ ] T074 [P] [US3] Unit tests in `tests/Meresoft.Umbraco.Banners.Tests.Unit/Statistics/CsvReportWriterTests.cs`: header `date,zone,banner,impressions,clicks`; one row per banner per zone per day; values starting with `=`, `+`, `-` or `@` prefixed with `'`; "(deleted banner)" and "(deleted zone)" names; and in `tests/Meresoft.Umbraco.Banners.Tests.Unit/Statistics/CtrTests.cs`: clicks / impressions rounded to one decimal percent, null for 0 impressions
- [ ] T075 [P] [US3] Integration tests in `tests/Meresoft.Umbraco.Banners.Tests.Integration/Delivery/ClickEndpointTests.cs`, one per row of the click table in contracts/public-endpoints.md: 302 to the stored LinkUrl with one click counted; 302 without counting for unpublished, missing, unknown or unassigned `z`, and bots; 404 for unknown banner, no Live version, Html banner, non-integer ids; `url`, `returnUrl` and `to` parameters ignored (no open redirect, US3 scenario 4); `Cache-Control: no-store` and `X-Robots-Tag: noindex, nofollow`; no `Set-Cookie`; a content node named `_banners` does not shadow the endpoint (R5 verify; contingency in T087)
- [ ] T076 [P] [US3] Integration tests in `tests/Meresoft.Umbraco.Banners.Tests.Integration/Delivery/SeenEndpointTests.cs`: 204 for every accepted body whatever the line validity; 413 above 16 KB without reading the rest; 405 for other methods; a valid line counts one impression for (today UTC, bannerId, token zone); one banner shown in two zones on one page and seen in both counts one impression in each zone; a repeated line, a banner id not in the token, an expired or tampered token, and a bot request count nothing; same headers and no cookies as T075
- [ ] T077 [P] [US3] Integration tests in `tests/Meresoft.Umbraco.Banners.Tests.Integration/Counting/FlushTests.cs`: flush adds deltas to existing rows (`Impressions = Impressions + @delta`); two flushers on one database (two "servers") inserting the same new row both end up counted (PK conflict retry); a failed write puts the counts back; `UmbracoApplicationStoppingNotification` flushes; serving a page and a seen request issue no database write (FR-012)
- [ ] T078 [P] [US3] Integration tests in `tests/Meresoft.Umbraco.Banners.Tests.Integration/Services/BannerStatisticsServiceTests.cs` and `tests/Meresoft.Umbraco.Banners.Tests.Integration/ManagementApi/ReportsApiTests.cs`: totals, rows per banner per zone, daily series with the imported flag, zone and banner filters, period over 366 days gives 400, `/reports/csv` content type `text/csv; charset=utf-8` and file name `banners-{from}-{to}.csv`; 100 impressions and 3 clicks give 3.0% (US3 scenario 1)
- [ ] T079 [P] [US3] Browser test `tests/e2e/us3-viewability.spec.ts`: below the fold without scrolling counts 0; scrolled into view at 50% for 1 s counts exactly 1 however often it scrolls in and out (quickstart scenario 7, US3 scenario 6); a hidden tab resets the timer; scripts off still shows the banner and the link works, nothing counted (scenario 8); the click redirects to the stored link (scenario 4); `/_banners/c/99999?url=https://evil.example` is 404 (scenario 11)
- [ ] T123 [P] [US3] Privacy test `tests/Meresoft.Umbraco.Banners.Tests.Integration/Privacy/PrivacyTests.cs`: with a captured log sink, no log line from the package holds an IP address or a user agent; no public endpoint or rendered zone sets a cookie; no package table has a column for IP, user agent or visitor id (FR-014)

### Implementation for User Story 3

- [ ] T080 [P] [US3] Implement `src/Meresoft.Umbraco.Banners/Tokens/ViewTokenPayload.cs`, `Tokens/IViewTokenProtector.cs` and `Tokens/ViewTokenProtector.cs` (`ITimeLimitedDataProtector`, purpose `Meresoft.Banners.ViewToken.v1`, lifetime `ViewTokenLifetime`, nonce from `RandomNumberGenerator`, compact binary payload, URL-safe text) (makes T069 pass)
- [ ] T081 [P] [US3] Implement `src/Meresoft.Umbraco.Banners/Tokens/ReplaySet.cs` (in-memory, expiring with the token lifetime, bounded memory, periodic purge) (makes T070 pass)
- [ ] T082 [P] [US3] Implement `src/Meresoft.Umbraco.Banners/Counting/BotPatterns.cs` (a list written for the package, no copied third-party list) and `src/Meresoft.Umbraco.Banners/Counting/BotMatcher.cs` (one `SearchValues<string>` matcher, case-insensitive; user agent read in memory, never stored or logged) (makes T071 pass)
- [ ] T083 [P] [US3] Implement `src/Meresoft.Umbraco.Banners/Counting/CounterBuffer.cs` (`ConcurrentDictionary<(DateOnly, int, int), Counter>` with `Interlocked` increments, swap and restore) (makes T072 pass)
- [ ] T084 [US3] Implement `src/Meresoft.Umbraco.Banners/Persistence/Repositories/IStatisticsRepository.cs` and `StatisticsRepository.cs`: add deltas in one transaction (update, insert when 0 rows, retry the update on a PK violation), report queries by period, zone and banner, totals per banner
- [ ] T085 [US3] Extend `src/Meresoft.Umbraco.Banners/Counting/BannersFlushJob.cs` to swap the counter buffer and write it, restoring on failure, and add `src/Meresoft.Umbraco.Banners/Counting/FlushOnStoppingHandler.cs` (`UmbracoApplicationStoppingNotification`) (depends on T083, T084; makes T077 pass)
- [ ] T086 [US3] Issue view tokens when rendering: `src/Meresoft.Umbraco.Banners/Rendering/BannerZoneHtmlWriter.cs` writes `data-mbz-token` on server mode zones with at least one banner, and image links point at `{PublicPath}/c/{bannerId}?z={zoneId}`; update `tests/Meresoft.Umbraco.Banners.Tests.Unit/Rendering/BannerZoneHtmlWriterTests.cs` first
- [ ] T087 [US3] Implement public routing in `src/Meresoft.Umbraco.Banners/Delivery/`: `BannersPublicRouteAttribute.cs`, `PublicPathRouteConvention.cs` (applies `PublicPath` to controllers marked with the attribute), `PublicResponseHeadersFilter.cs` (`Cache-Control: no-store`, `X-Robots-Tag: noindex, nofollow`, never sets cookies); if T075 shows that a content node shadows the endpoints, register them through `UmbracoPipelineFilter.Endpoints` in `src/Meresoft.Umbraco.Banners/Delivery/BannersPipelineFilter.cs` instead
- [ ] T088 [US3] Implement `src/Meresoft.Umbraco.Banners/Delivery/ClickController.cs` (`GET {base}/c/{bannerId:int}?z={zoneId}`, `[AllowAnonymous]`, target only the stored Live LinkUrl from the snapshot or a cached lookup of Live links for unpublished banners, counts only when published, assigned to `z` and not a bot) (depends on T087; makes T075 pass)
- [ ] T089 [US3] Implement `src/Meresoft.Umbraco.Banners/Delivery/SeenBodyParser.cs` and `src/Meresoft.Umbraco.Banners/Delivery/SeenController.cs` (`POST {base}/seen`, `text/plain`, 16 KB limit with 413, always 204, token and replay checks, bot filter) (depends on T080, T081, T087; makes T073 and T076 pass)
- [ ] T090 [US3] Write the package script `src/Meresoft.Umbraco.Banners/Client/script/banners.ts` for server mode per contracts/public-endpoints.md: find `[data-mbz]` zones, read `data-mbz-token` and `[data-mbb]` elements, one `IntersectionObserver` at threshold 0.5, 1,000 ms continuous visibility while `document.visibilityState === "visible"`, reset on leaving or hiding, at most one report per element per page view, queue sent every 2 s and on `pagehide` with `navigator.sendBeacon` (fallback `fetch` with `keepalive`), nothing when `IntersectionObserver` is missing; endpoint base path from the zone's `data-mb-base` attribute (contracts/markup.md)
- [ ] T091 [P] [US3] Add `src/Meresoft.Umbraco.Banners/Client/scripts/check-size.mjs`, run by `npm run build`, failing when `wwwroot/meresoft-banners/banners.js` is 5,120 bytes or more (FR-010)
- [ ] T092 [US3] Implement statistics: `src/Meresoft.Umbraco.Banners/Services/Models/BannerReport.cs` and `BannerReportQuery.cs`, `src/Meresoft.Umbraco.Banners/Services/IBannerStatisticsService.cs` and `BannerStatisticsService.cs` (names for deleted banners and zones, imported flag per day, max 366 days), `src/Meresoft.Umbraco.Banners/Statistics/CsvReportWriter.cs` and `src/Meresoft.Umbraco.Banners/Statistics/Ctr.cs` (makes T074 and T078 service parts pass)
- [ ] T093 [US3] Implement `src/Meresoft.Umbraco.Banners/ManagementApi/Reports/GetReportController.cs` and `GetReportCsvController.cs` with models in `ManagementApi/Reports/Models/` (makes T078 pass); register tokens, replay set, bot matcher, buffer, statistics and delivery in `BannersBuilderExtensions.cs`; regenerate the API client
- [ ] T094 [P] [US3] Build `src/Meresoft.Umbraco.Banners/Client/src/reports/reports-view.element.ts` (presets today, last 7 days, last 30 days, this month, last month and custom, computed in UTC days; zone and banner filters; totals and table with CTR or a dash; imported days marked; notes "statistics days are UTC days" and "counts may be up to one minute behind"; CSV download) and `src/Meresoft.Umbraco.Banners/Client/src/reports/daily-chart.element.ts` (SVG chart in Lit, no chart library, with an accessible name and the table as text alternative), with `manifests.ts`
- [ ] T095 [US3] Run `tests/e2e/us3-viewability.spec.ts` and quickstart scenario 10 (report and CSV match the counted views) on the sample site

**Checkpoint**: US1 to US3 give a complete first release for server mode sites: show, rotate, count, report.

---

## Phase 6: User Story 6 - Permissions and audit (Priority: P2)

**Goal**: HTML banners behind the separate HTML permission, 403 for users without section access, audit history per banner, administrator only access after install.

**Independent Test**: a user without the Banners permission does not see the section and gets 403 from every management endpoint; a user without the HTML permission can publish an HTML banner but not change its HTML (quickstart scenario 12).

### Tests for User Story 6 (write first, they must fail)

- [ ] T096 [P] [US6] Integration tests in `tests/Meresoft.Umbraco.Banners.Tests.Integration/ManagementApi/SectionPermissionTests.cs`: a user whose groups lack `Meresoft.Banners.Section` gets 403 from every package endpoint, enumerated from the `meresoft-banners` OpenAPI document so new endpoints are covered (US6 scenario 1)
- [ ] T097 [P] [US6] Integration tests in `tests/Meresoft.Umbraco.Banners.Tests.Integration/ManagementApi/HtmlPermissionTests.cs`: without `Meresoft.Banners.Html`, creating an Html banner or changing its `html` gives 403 `type: html-permission-required`, while saving other fields, publish, unpublish, copy, delete and reports succeed; with the verb in any of the user's groups all succeed (US6 scenario 2)
- [ ] T098 [P] [US6] Unit tests for Html banners in `tests/Meresoft.Umbraco.Banners.Tests.Unit/Rendering/BannerZoneHtmlWriterTests.cs`: `<div class="mb-banner mb-html" data-mbb="{id}">{stored html}</div>` written as stored, no click wrapper (FR-006); and in `tests/Meresoft.Umbraco.Banners.Tests.Unit/Caching/LiveSnapshotBuilderTests.cs`: Html banners need no media
- [ ] T099 [P] [US6] Integration tests in `tests/Meresoft.Umbraco.Banners.Tests.Integration/Services/AuditTests.cs`: create, save, publish, unpublish, copy and delete of banners and create, update and delete of zones each write one audit entry with the user key, the entity type and a comment holding only ids and action names; `GET /banners/{key}/history` and `GET /zones/{key}/history` return user name, action and date, paged (FR-018)
- [ ] T100 [P] [US6] Browser test `tests/e2e/us6-permissions.spec.ts`: the sample editor sees no Banners section (scenario 12); an administrator on a fresh install sees it (US6 scenario 3); a group given the section without the HTML permission sees the HTML field read-only; the history views of a banner and of a zone list the changes

### Implementation for User Story 6

- [ ] T101 [US6] Support the Html type end to end: validation in `src/Meresoft.Umbraco.Banners/Domain/BannerVersionValidator.cs`, candidates in `src/Meresoft.Umbraco.Banners/Caching/LiveSnapshotBuilder.cs`, markup in `src/Meresoft.Umbraco.Banners/Rendering/BannerZoneHtmlWriter.cs`, 404 for Html banners in `src/Meresoft.Umbraco.Banners/Delivery/ClickController.cs` (makes T098 pass)
- [ ] T102 [US6] Implement `src/Meresoft.Umbraco.Banners/ManagementApi/Authorization/HtmlPermissionChecker.cs` (any of the user's groups has the fallback permission `Meresoft.Banners.Html`) and use it in `ManagementApi/Banners/CreateBannerController.cs` and `UpdateBannerController.cs` only when the type is Html and the `html` value differs from the stored draft (makes T097 pass; T096 should pass with the base policy)
- [ ] T103 [US6] Implement `src/Meresoft.Umbraco.Banners/ManagementApi/Banners/GetBannerHistoryController.cs` (`IAuditService.GetItemsByEntityAsync`, filtered by entity type `meresoft-banner`, user names from `IUserService`) and `src/Meresoft.Umbraco.Banners/ManagementApi/Zones/GetZoneHistoryController.cs` (same, entity type `meresoft-banner-zone`) and check every service write calls `BannerAuditWriter` (makes T099 pass); regenerate the API client
- [ ] T104 [P] [US6] Add `src/Meresoft.Umbraco.Banners/Client/src/permissions/manifests.ts`: `entityUserPermission` with `forEntityTypes: ["meresoft-banner"]`, verb `Meresoft.Banners.Html`, label and description keys, group key `user_permissionsEntityGroup_meresoft-banner` in `src/lang/en.ts`
- [ ] T105 [US6] Add the HTML field to `src/Meresoft.Umbraco.Banners/Client/src/banners/banner-editor.element.ts` (code text area, editable only under `Umb.Condition.UserPermission.Fallback` with the verb, read-only otherwise), the type choice on create (Html only with the permission), `src/Meresoft.Umbraco.Banners/Client/src/banners/banner-history.element.ts` as a workspace view, and `src/Meresoft.Umbraco.Banners/Client/src/zones/zone-history.element.ts` as a history view of the zone editor

**Checkpoint**: access is controlled and every change is traceable; HTML banners render in server mode.

---

## Phase 7: User Story 4 - Pages served from a cache or CDN (Priority: P2)

**Goal**: client mode zones render a placeholder; the package script fetches banners and a token from the delivery endpoint, renders them and counts them.

**Independent Test**: set a zone to client mode on the cached sample page, open it in two browsers: each gets its own choice and both impressions count (quickstart scenario 9).

### Tests for User Story 4 (write first, they must fail)

- [ ] T106 [P] [US4] Integration tests in `tests/Meresoft.Umbraco.Banners.Tests.Integration/Delivery/ZoneDeliveryEndpointTests.cs` against contracts/public-endpoints.md: 200 JSON with `zone`, `token` and banners of type image (id, clickUrl, newWindow, imageUrl, width, height, alt, title) and html (id, html); never name, weight, schedule, limits, statistics, draft or user data; unknown alias gives `"banners": []`, no token, and is recorded as requested; no active banner gives `[]` and no token; an alias failing the rules gives 404; `DeliveryEndpointEnabled=false` gives 404; the returned token is accepted by `seen`; same headers and no cookies as T075
- [ ] T107 [P] [US4] Unit tests in `tests/Meresoft.Umbraco.Banners.Tests.Unit/Rendering/BannerZoneHtmlWriterTests.cs` for client mode: `<div class="mb-zone" data-mbz="{alias}" data-mbz-mode="client">` always written with the fallback inside and no token; with the delivery endpoint disabled only the fallback is written
- [ ] T108 [P] [US4] Browser test `tests/e2e/us4-client-mode.spec.ts`: two browser contexts on the cached page each get a banner and each impression counts (US4 scenario 1); scripts off shows the fallback or nothing, no error (US4 scenario 2); an Html banner in client mode renders and its inline scripts run; an Html banner in server mode and in client mode counts one impression when its box is 50% visible for 1 s (FR-006)

### Implementation for User Story 4

- [ ] T109 [US4] Implement `src/Meresoft.Umbraco.Banners/Delivery/ZoneDeliveryController.cs` and `src/Meresoft.Umbraco.Banners/Delivery/Models/ZoneDeliveryResponse.cs` (System.Text.Json source generation, camelCase), using `BannerSelector`, `ViewTokenProtector` and `RequestedZoneRecorder` (makes T106 pass)
- [ ] T110 [US4] Add client mode to `src/Meresoft.Umbraco.Banners/Rendering/BannerZoneHtmlWriter.cs` (makes T107 pass)
- [ ] T111 [US4] Add client mode to `src/Meresoft.Umbraco.Banners/Client/script/banners.ts`: fetch `{base}/zone/{alias}`, build the banner elements safely (attributes set through the DOM, not string concatenation), replace the fallback only when at least one banner came back, re-create `<script>` elements of Html banners so they run, then observe as in server mode; keep the script under 5 KB (T091)
- [ ] T112 [US4] Add the render mode choice with help text about cached pages to `src/Meresoft.Umbraco.Banners/Client/src/zones/zone-editor.element.ts`, and run `tests/e2e/us4-client-mode.spec.ts`

**Checkpoint**: cached and CDN pages rotate and count banners in client mode.

---

## Phase 8: User Story 5 - Limits per banner (Priority: P3)

**Goal**: impression and click limits across all zones; a banner over its limit stops showing within about one flush interval and is flagged "limit reached".

**Independent Test**: a banner with an impression limit of 3, viewed 5 times, stops showing after the next flush and is flagged (quickstart scenario 13).

### Tests for User Story 5 (write first, they must fail)

- [ ] T113 [P] [US5] Unit tests in `tests/Meresoft.Umbraco.Banners.Tests.Unit/Selection/LimitFilterTests.cs`: a banner with total impressions >= ImpressionLimit or total clicks >= ClickLimit is not a candidate; null limits never block; totals include imported rows
- [ ] T114 [P] [US5] Integration tests in `tests/Meresoft.Umbraco.Banners.Tests.Integration/Counting/LimitReachedTests.cs`: limit 3, 5 counted views across two zones, after the flush the banner is gone from the snapshot and the list flag is `limitReached`; raising the limit and publishing shows it again (US5 scenario 2); overshoot is at most the views of one flush interval
- [ ] T118 [P] [US5] Browser test `tests/e2e/us5-limits.spec.ts`, covering the limit fields and flag of T117: quickstart scenario 13

### Implementation for User Story 5

- [ ] T115 [US5] Load totals for banners with limits in `src/Meresoft.Umbraco.Banners/Caching/LiveSnapshotBuilder.cs` (from `StatisticsRepository` totals per banner, summed over all zones and days including imported rows) and implement `src/Meresoft.Umbraco.Banners/Selection/LimitFilter.cs` (makes T113 pass)
- [ ] T116 [US5] After each successful flush, `src/Meresoft.Umbraco.Banners/Counting/BannersFlushJob.cs` reloads the totals of limited banners and swaps the snapshot locally when a banner crosses its limit; `BannerStateCalculator` uses the same totals for the flag (makes T114 pass)
- [ ] T117 [P] [US5] Add the impression and click limit fields (click limit hidden for Html banners, FR-006) and the "Limit reached" flag to `src/Meresoft.Umbraco.Banners/Client/src/banners/banner-editor.element.ts` and `banners-list-view.element.ts`

**Checkpoint**: all six stories work independently and together.

---

## Phase 9: Polish and cross-cutting concerns

**Purpose**: import surface, performance proof, SQL Server runs, documentation, packaging and release checks.

- [ ] T119 [P] Integration tests in `tests/Meresoft.Umbraco.Banners.Tests.Integration/Services/BannerImportServiceTests.cs`: `Add` adds to existing rows, `Replace` overwrites the counts, both set `IsImported = true`; batches of 1,000 rows per transaction; unknown banner or zone keys and negative numbers are rejected and counted; the result reports inserted, updated and rejected (FR-016)
- [ ] T120 Implement `src/Meresoft.Umbraco.Banners/Services/IBannerImportService.cs`, `BannerImportService.cs` and models `Services/Models/DailyStatisticImport.cs`, `StatisticImportMode.cs`, `BannerImportResult.cs` (makes T119 pass)
- [ ] T121 [P] Benchmark `tests/Meresoft.Umbraco.Banners.Benchmarks/ZoneRenderBenchmarks.cs`: one zone with 20 active banners, p95 under 2 ms, with a check that no database command runs (SC-003)
- [ ] T122 [P] Load test `tests/Meresoft.Umbraco.Banners.Tests.Integration/Counting/CountingLoadTests.cs`: 100 views per second with 4 zones for 60 s across two in-process "servers", flushed totals equal reported totals with under 0.1% loss, at most one write batch per server per flush interval, and a burst of 400 reports per second per server (SC-004, SC-005)
- [ ] T124 Run the whole integration suite on SQL Server: base class or setup in `tests/Meresoft.Umbraco.Banners.Tests.Integration/TestSetup/DatabaseSetup.cs` marks the SQL Server variant `[Category("SqlServer")]`; fix any dialect issue in `src/Meresoft.Umbraco.Banners/Persistence/Repositories/` (SC-006)
- [ ] T125 Complete `.github/workflows/ci.yml`: client install, lint and build, script size check, unit tests, integration on SQLite, integration on SQL Server (service container, connection string from a secret), Playwright against the sample site, `dotnet pack`, and a pack check that the package holds the built `wwwroot` assets, README, icon and licence and no sample or test files
- [ ] T126 [P] Add a check `src/Meresoft.Umbraco.Banners/Client/scripts/check-localization.mjs` (run in lint) that fails on literal UI text in `src/Meresoft.Umbraco.Banners/Client/src/` outside `lang/` (FR-021)
- [ ] T127 [P] Accessibility test `tests/e2e/a11y.spec.ts` with `@axe-core/playwright` over the banners list, banner editor, zones and reports views (keyboard use, labels, contrast, chart text alternative)
- [ ] T128 Write `README.md`: install, placing a zone (tag helper and view component), editor guide (zones, banners, draft and publish, reports), first banner in under 15 minutes (SC-001), giving other groups access, HTML permission, configuration keys and defaults, client mode for cached pages, viewable impressions and why imported history differs, UTC days, limit overshoot, same banner in two zones on one page, multi-server note on view tokens, the import service with a short example, privacy statement, upgrade notes, how to report issues, uninstall (remove the zone markup and the `@addTagHelper` line before removing the package, then the documented table removal step)
- [ ] T129 [P] Add `CHANGELOG.md` with 1.0.0 release notes and `PackageReleaseNotes` in `src/Meresoft.Umbraco.Banners/Meresoft.Umbraco.Banners.csproj`; replace the placeholder icon `assets/icon.png` with original artwork and `umbraco-marketplace.json` at the repository root (FR-023)
- [ ] T130 Run `specs/001-banner-manager/quickstart.md` end to end on the sample site (all 14 scenarios and the performance checks), run `dotnet pack` and inspect the package, and grep the repository for em dashes and for real client names or data; fix anything found

---

## Dependencies and execution order

### Phase dependencies

- Setup (Phase 1): no dependencies.
- Foundation (Phase 2): after Setup; blocks every story.
- US1 (Phase 3): after Foundation. The MVP.
- US2 (Phase 4): after US1 (extends the selector, snapshot and editor).
- US3 (Phase 5): after US1 (tokens and click links go into the zone markup; the flush job from T053 is extended). Independent of US2.
- US6 (Phase 6): after US1 (banner service, editor and API). Independent of US2 to US5, except that the reports check in T097 needs US3.
- US4 (Phase 7): after US3 (view tokens, seen endpoint, package script) and US6 (Html banners in the delivery response).
- US5 (Phase 8): after US3 (statistics repository and flush job).
- Polish (Phase 9): after the stories it covers; T119 and T120 need US3 only.

### Story completion order

```text
Setup -> Foundation -> US1 -> { US2, US3, US6 } ; US3 -> US5 ; US3 + US6 -> US4 -> Polish
```

### Within each story

- Tests first, and they must fail before the implementation task that makes them pass.
- Models before services, services before controllers, controllers before the API client and back office views.
- Commit after each task (constitution, Development Workflow); never push without the owner's approval.

### Parallel opportunities

- Setup: T005 to T010, T012 and T013 touch different projects.
- Foundation: all tests T015 to T023 in parallel; T024, T025, T030, T036, T037, T038 in parallel.
- US1: tests T039 to T045 and T060 in parallel; T046 with them; T058 and T059 once the API client exists.
- After US1, US2, US3 and US6 can be worked on by different people; within US3, T080 to T083 and T091 are independent.

---

## Parallel example: User Story 3

```text
Tests together:
  T069 ViewTokenProtectorTests    T070 ReplaySetTests       T071 BotMatcherTests
  T072 CounterBufferTests         T073 SeenBodyParserTests  T074 CsvReportWriterTests
  T075 ClickEndpointTests         T076 SeenEndpointTests    T077 FlushTests
  T123 PrivacyTests
Then independent implementations together:
  T080 ViewTokenProtector   T081 ReplaySet   T082 BotMatcher   T083 CounterBuffer   T091 size check
```

## Parallel example: User Story 1

```text
Tests together: T039 T040 T041 T042 T043 T044 T045 T060, and models T046
Then: T047 -> T048 -> T049 -> T050 (services and caching), T051 and T052 alongside T049
Back office together once T057 is done: T058 zones views, T059 banner views
```

---

## Implementation strategy

### MVP first (User Story 1 only)

1. Setup and Foundation.
2. US1: zones, image banners, draft and publish, tag helper, back office views.
3. Stop and validate with quickstart scenarios 1 to 3, 5 and 14 (scenario 4 without the click address). A site can show managed banners.

### Incremental delivery

1. MVP (US1).
2. US2: rotation and schedules, the old site's slot behaviour.
3. US3: viewable impressions, clicks and reports. US1 to US3 are the first release for server mode sites.
4. US6: HTML banners and audit.
5. US4: client mode for cached pages.
6. US5: limits.
7. Polish: import service (needed by the first client's spec 009), performance proof, docs, pack.

### Parallel team strategy

After Foundation and US1, one developer takes US2, one US3 and one US6; US4 and US5 follow US3.

---

## Notes

- 130 tasks; every task names its files.
- Tasks marked [P] touch different files and do not wait for an unfinished task.
- Constitution checks for every task: no client names or data, invented test data, no em dashes, no connection strings or credentials in the repository.
- Stop at any checkpoint to validate the story on its own.
