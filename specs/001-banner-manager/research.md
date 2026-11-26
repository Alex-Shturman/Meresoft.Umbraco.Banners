# Research: Banner manager (001)

Facts below were checked against the Umbraco source at tag `release-17.8.2`, the v17 docs source
(UmbracoDocs `17/umbraco-cms`), NuGet and npm on 2026-11-25. "Verify" marks a point that is inferred
and gets a test early in implementation.

## R1. Supported Umbraco range

- **Decision**: depend on `Umbraco.Cms.*` `[17.8.0, 18.0.0)`, target `net10.0`.
- **Rationale**: 17 is the LTS (17.0.0 on 2025-11-27, current 17.8.2) and targets .NET 10.
  `RunJobAsync(CancellationToken)` and `RecurringBackgroundJobBase` (needed for clean shutdown
  flushing) are only present from 17.6 to 17.8; starting at 17.8 avoids obsolete-API warnings and
  double code paths. LTS sites take minor updates routinely.
- **Alternatives**: `[17.0.0, 18.0.0)`, rejected: forces the obsolete parameterless job API and
  older behaviour; can be widened later without breaking anyone.

## R2. Data access: NPoco with Umbraco scopes

- **Decision**: NPoco through `IScopeProvider` (`Umbraco.Cms.Infrastructure.Scoping`) and
  `scope.Database`, DTO classes with NPoco attributes, SQL built with `Sql<ISqlContext>` so the same
  code runs on SQL Server and SQLite.
- **Rationale**: the documented path for package tables in 17; migrations and repositories share
  one model; no `dotnet ef` step for package users.
- **Alternatives**: EF Core via `IEFCoreScopeProvider<TDbContext>`, rejected: extra migration tool
  chain, two persistence stacks in one site.

## R3. Package migrations

- **Decision**: one `PackageMigrationPlan` (`Umbraco.Cms.Core.Packaging`) named
  `Meresoft.Umbraco.Banners`, with steps derived from `AsyncMigrationBase` (override `MigrateAsync`).
  Tables are created with `Create.Table<TSchemaDto>()` from frozen schema DTOs kept inside the
  migration namespace (not the runtime DTOs), guarded by `TableExists`.
- **First install step** also grants the Banners section and the HTML permission to the
  Administrators group: `IUserGroupService.GetAsync(Constants.Security.AdminGroupKey)`,
  `AddAllowedSection("Meresoft.Banners.Section")`, add `Meresoft.Banners.Html` to `Permissions`,
  `UpdateAsync(group, Constants.Security.SuperUserKey)`. Later steps never touch groups
  (FR-017, clarification 5).
- Migrations run automatically at startup (`PackageMigrationsUnattended` defaults to true);
  migrations are created by the container, so constructor injection works.
- **Alternatives**: `MigrationBase` and `PackageMigrationBase`, rejected: obsolete, removal in 18.
  `AsyncPackageMigrationPlan` does not exist.

## R4. Back office section, permissions and management API

- **Section**: manifest `type: "section"`, alias `Meresoft.Banners.Section`, with condition
  `Umb.Condition.SectionUserPermission` (must be added explicitly). Section sidebar and views use
  `Umb.Condition.SectionAlias`.
- **HTML permission**: manifest `type: "entityUserPermission"`, `forEntityTypes:
  ["meresoft-banner"]`, verb `Meresoft.Banners.Html`. It shows in the user group editor (grouped
  under localization key `user_permissionsEntityGroup_meresoft-banner`) and is stored in the group's
  fallback permissions (`IUserGroup.Permissions`). Client check: condition
  `Umb.Condition.UserPermission.Fallback`; server check: any of the user's groups has the verb.
- **Management API**: controllers derive from `ManagementApiControllerBase` with
  `[VersionedApiBackOfficeRoute("meresoft-banners/...")]`, `[ApiExplorerSettings(GroupName = ...)]`
  and `[MapToApi("meresoft-banners")]`; a separate Swagger document `meresoft-banners` is registered
  through `IConfigureOptions<SwaggerGenOptions>` with an operation filter derived from
  `BackOfficeSecurityRequirementsOperationFilterBase`. 17 still uses Swashbuckle 10 with
  `Microsoft.OpenApi` 2.x (v18 moves to `Microsoft.AspNetCore.OpenApi`; isolate this in one file).
- **Section authorization**: Umbraco's `AllowedApplicationRequirement` is internal, so the package
  defines policy `Meresoft.Banners.SectionAccess`: OpenIddict validation scheme plus a requirement
  handled by a `MustSatisfyRequirementAuthorizationHandler<T>` that checks
  `user.AllowedSections.Contains("Meresoft.Banners.Section")` (user from
  `IAuthorizationHelper.TryGetUmbracoUser`). HTML permission is checked in the controller on writes
  that change HTML.
- **Alternatives**: a second section for HTML editing, rejected: wrong UX; role based policies
  with group aliases, rejected: group aliases in role claims are unverified.

## R5. Public endpoints

- **Decision**: attribute-routed controllers (`[Route("{PublicPath}/...")]` applied through a route
  convention so the path is configurable), `[AllowAnonymous]`, no anti-forgery on `seen` (simple
  `text/plain` POST, no cookies involved).
- **Rationale**: Umbraco maps controllers (`MapControllers`) and states it does not interfere with
  user routes; the content route is a dynamic catch-all with lower precedence. Default path
  `/_banners` stays out of the reserved `~/umbraco/` and `~/app_plugins/` paths and of CDN rules that
  usually bypass `/umbraco`.
- **Verify**: an integration test proves a content node named `_banners` does not shadow the
  endpoints. Fallback if it does: register endpoints in an `UmbracoPipelineFilter.Endpoints`.

## R6. Viewable impressions and view tokens

- **Decision**: the package script uses `IntersectionObserver` (threshold 0.5) and a 1,000 ms timer
  per banner, reset on leaving the viewport or hidden tab; queued reports go by `sendBeacon` every
  2 s and on `pagehide` (contracts/public-endpoints.md).
- **Token**: `ITimeLimitedDataProtector` (purpose `Meresoft.Banners.ViewToken.v1`) protects
  {zone id, banner ids, 96 bit nonce}; lifetime 10 minutes. Validation is stateless on any server;
  single use is enforced by an in-memory replay set per server keyed by (nonce, banner id) and
  expiring with the token.
- **Multi-server note**: a report replayed to a different server could count once more per server.
  This needs deliberate faking, is bounded by the server count and the token lifetime, and is
  documented. Load-balanced Umbraco sites already share the data protection key ring (needed for
  back office sign-in), which token validation relies on.
- **Rationale**: meets the spec (in memory, no visitor data, not stored) without a shared store.
- **Alternatives**: a token table in the database, rejected: writes per page view (constitution
  IV); counting on render, rejected by clarification 4.

## R7. Counting and flushing

- **Decision**: a `ConcurrentDictionary<(DateOnly, int banner, int zone), Counter>` with
  `Interlocked` increments; the flush swaps the dictionary and writes the rows in one flush pass, one short
  transaction per counted row (a failing row does not lose the others):
  `UPDATE ... SET Impressions = Impressions + @i, Clicks = Clicks + @c WHERE PK`, and on 0 rows
  `INSERT`; a primary key violation (another server inserted first) retries the update. Portable to
  SQL Server and SQLite.
- **Job**: `RecurringBackgroundJobBase` with `ServerRoles` = all roles (every server flushes its own
  counts), `Period` = `FlushInterval`, `Delay` = 30 s; plus a flush in an
  `UmbracoApplicationStoppingNotification` handler for clean shutdown. A failed write puts the
  counts back into the buffer for the next run; a crash loses at most one interval (constitution IV).
- After each flush the job also writes requested zones and reloads the totals of banners with
  limits; banners over a limit are dropped from the snapshot (limit reached within about one
  interval, documented overshoot, US5).
- **Alternatives**: `IDistributedBackgroundJob`, rejected: runs on one server only, but counts
  live in each server's memory.

## R8. Live snapshot and cache refresh

- **Decision**: an immutable snapshot (zones by alias, active candidates per zone with pre-built
  image URL, width, height, alt, title, link, weight, schedule) swapped atomically. Rebuilt by a
  `JsonCacheRefresherBase<BannersCacheRefresherNotification, Payload>` triggered with
  `DistributedCache.RefreshByPayload` on publish, unpublish, delete and zone changes (all servers),
  by a handler of `MediaCacheRefresherNotification` when a referenced media item changes or is
  deleted ("image missing"), and by the safety reload interval.
- **Image URLs**: resolved at snapshot build with `IPublishedMediaCache.GetByIdAsync(Guid)` and
  `IImageUrlGenerator` (width and height from the zone). The 17 HybridCache media cache can hit the
  database on a miss, so resolving at build time, not per request, keeps page requests query-free
  (SC-003).
- **Schedule** is evaluated per request against the snapshot (start and end times change state
  without a publish).

## R9. Weighted selection

- **Decision**: per zone view, weighted random sampling without replacement over active candidates
  (draw, remove, repeat `ShowCount` times) using `Random.Shared`; an injectable random source for
  tests. With at most tens of candidates this is microseconds.
- **Test**: seeded statistical test, 4,000 draws, weights 1 and 3, shares within 5 points (US2-1).

## R10. Back office and front end assets

- **Decision**: Razor Class Library (`Microsoft.NET.Sdk.Razor`, `StaticWebAssetBasePath=/`) laid out
  like the `dotnet new umbraco-extension` template: `Client/` with Vite 7, TypeScript, Lit,
  `@umbraco-cms/backoffice` `^17` as an external, output to `wwwroot/App_Plugins/MeresoftBanners/`,
  `umbraco-package.json` with one `bundle` extension; API client generated with `@hey-api` from the
  package's Swagger document; English `localization` manifest (FR-021).
- **Report chart**: a small SVG chart written as a Lit element, no chart library.
- **Package script**: plain TypeScript compiled and minified by a separate Vite build to
  `wwwroot/meresoft-banners/banners.js`; because `StaticWebAssetBasePath=/`, it is served at
  `/meresoft-banners/banners.js` (not under `/_content/...`). The tag helper takes the path from one
  constant. A CI check fails the build above 5 KB (FR-010).

## R11. Bot filter

- **Decision**: ship a curated list of case-insensitive user agent substrings (common crawlers,
  link previewers, uptime monitors, headless browsers, empty user agent) compiled into one
  `SearchValues<string>` matcher; extended by `Bots:AdditionalUserAgentPatterns`. The list is
  written for the package (no copied third-party list, so no licence issue).

## R12. Audit and history

- **Writing**: `IAuditService.AddAsync(AuditType, userKey, bannerId or zoneId, entityType
  "meresoft-banner" or "meresoft-banner-zone", comment)` with `AuditType.New`, `Save`, `Publish`,
  `Unpublish`, `Copy`, `Delete`. Comments hold ids and action names only.
- **Reading**: not through `IAuditService.GetItemsByEntityAsync`. That method filters by entity id only, and
  banners, zones and content nodes share the id space, so the entity type would have to be filtered after
  paging, which breaks the total and the pages. Its `customFilter` cannot help: Umbraco's `AuditItemMapper`
  has no map for `EntityType` (a query on it throws "No map defined ... for property EntityType"). The package
  reads the `umbracoLog` table itself with a read-only repository (`AuditHistoryRepository`, own NPoco mapping
  `AuditLogRowDto`, because Umbraco's `LogDto` is internal): `WHERE NodeId = @id AND entityType = @type`,
  newest first then by id, paged in SQL, with a count for the total. It works on SQL Server and SQLite and
  writes nothing. User names come from `IUserIdKeyResolver` (id to key) and one `IUserService.GetAsync`; a user
  that no longer exists gives a null name and the back office shows its own text.
- **Retention**: Umbraco's log scrubber (`IAuditService.CleanLogsAsync(MaxLogAge)`) deletes only rows with the
  header `open` or `system`, so the rows of the package stay (pinned by `AuditRetentionTests`; README, "History
  and permissions"). The history view reads at most 10,000 entries back, 100 per request.

## R13. Testing

- **Unit**: NUnit 3.14 (same as Umbraco's integration package), NSubstitute; selection, schedule,
  limits, state derivation, validation, bot matcher, token, counter buffer, CSV.
- **Integration**: `Umbraco.Cms.Tests.Integration` 17.8.x with `UmbracoIntegrationTest` (services,
  migrations, flush) and `UmbracoTestServerTestBase` / `ManagementApiTest` (HTTP endpoints, 403s).
  SQLite by default; SQL Server via `Tests:Database:DatabaseType=SqlServer` and the master connection
  string from an environment variable in CI (`Category=SqlServer`).
- **Browser**: Playwright (Node) against the sample site for viewability (above and below the
  fold, scroll in and out, hidden tab, scripts off), client mode on a cached page, and back office
  smoke flows.
- **Performance**: BenchmarkDotNet for zone render (SC-003); an in-process load test for counting
  (SC-004).

## R14. Repository, CI and distribution

- `Meresoft.Umbraco.Banners.slnx`, central package management (`Directory.Packages.props`),
  `Directory.Build.props` with nullable, warnings as errors, deterministic builds and SourceLink.
- GitHub Actions: build, back office build and lint, unit, integration on SQLite and on SQL Server
  (service container), Playwright, script size check, `dotnet pack`. Publishing to NuGet is a
  manual, owner-approved step (constitution: no push without approval).
- NuGet metadata: id `Meresoft.Umbraco.Banners`, MIT, icon, README, release notes, tags
  `umbraco umbraco-marketplace banners ads`, plus `umbraco-marketplace.json` at the repository root.
