# Contract: configuration and public C# services

## Configuration (`appsettings.json`, section `Meresoft:Banners`) (FR-022)

```json
{
  "Meresoft": {
    "Banners": {
      "PublicPath": "/_banners",
      "DeliveryEndpointEnabled": true,
      "FlushInterval": "00:01:00",
      "ViewTokenLifetime": "00:10:00",
      "CacheSafetyReloadInterval": "00:05:00",
      "Bots": {
        "AdditionalUserAgentPatterns": [ "my-monitoring-agent" ]
      }
    }
  }
}
```

| Key | Default | Rules |
|---|---|---|
| PublicPath | `/_banners` | starts with `/`, no trailing `/`, must not start with `/umbraco`, no `//`, no `{ } ? # %`, no whitespace or control characters; validated at startup |
| DeliveryEndpointEnabled | true | false disables `GET {base}/zone/{alias}`; client mode zones then render only their fallback |
| FlushInterval | 1 minute | 5 seconds to 1 minute (FR-012 "at least once a minute") |
| ViewTokenLifetime | 10 minutes | 1 to 60 minutes; how long after rendering an impression can still be reported |
| CacheSafetyReloadInterval | 5 minutes | 1 to 60 minutes; periodic snapshot rebuild in case a refresh message was missed; 0 disables |
| Bots:AdditionalUserAgentPatterns | empty | case-insensitive substrings added to the shipped list |

Options are validated on start (`ValidateOnStart`); an invalid value stops the site with a clear
message naming the key.

## Public C# services

Registered by the package composer; part of the stable public surface (constitution VI). The
signatures below are those of the interfaces in `Services/`, `Selection/` and `Tokens/`. Service
methods are async and each call runs in its own Umbraco scope.

### `IBannerZoneService` (namespace `Meresoft.Umbraco.Banners.Services`)

- `Task<IReadOnlyList<BannerZone>> GetAllAsync()`: all zones ordered by name.
- `Task<BannerZone?> GetAsync(Guid key)`: a zone by its management API key.
- `Task<BannerZone?> GetByAliasAsync(string alias)`: the alias is normalised first.
- `Task<Attempt<BannerZone, BannerOperationStatus>> CreateAsync(BannerZoneModel model, Guid userKey)`:
  also removes the matching requested zone.
- `Task<Attempt<BannerZone, BannerOperationStatus>> UpdateAsync(Guid key, BannerZoneModel model, Guid userKey)`:
  `BannerZoneModel.UpdateDate` guards against a concurrent change.
- `Task<BannerOperationStatus> DeleteAsync(Guid key, Guid userKey)`: `ZoneInUse` while any banner
  version is assigned to the zone.
- `Task<IReadOnlyList<RequestedZone>> GetRequestedZonesAsync()`: aliases that templates asked for
  but that do not exist yet.
- `Task DismissRequestedZoneAsync(string alias)`: removes an alias from the requested zones; it
  comes back when a template asks for it again.
- `Task<IReadOnlyList<Guid>> GetAssignedBannerKeysAsync(Guid zoneKey)`: keys of the banners with a
  draft or live version assigned to the zone.

### `IBannerService`

- `Task<Banner?> GetAsync(Guid key)`
- `Task<PagedModel<BannerListItem>> GetPagedAsync(BannerQuery query)`
- `Task<Attempt<Banner, BannerOperationStatus>> CreateAsync(BannerCreateModel model, Guid userKey)`
- `Task<Attempt<Banner, BannerOperationStatus>> SaveDraftAsync(Guid key, BannerDraftModel model, Guid userKey)`
- `Task<Attempt<Banner, BannerOperationStatus>> PublishAsync(Guid key, Guid userKey, DateTime? expectedUpdateDate = null)`:
  a stored update date that differs from `expectedUpdateDate` gives `ChangedByAnotherUser`; null
  skips the check.
- `Task<Attempt<Banner, BannerOperationStatus>> UnpublishAsync(Guid key, Guid userKey)`
- `Task<Attempt<Banner, BannerOperationStatus>> CopyAsync(Guid key, string? newName, Guid userKey)`
- `Task<BannerOperationStatus> DeleteAsync(Guid key, Guid userKey)`: statistics stay.

The HTML permission is checked by the service for the acting user (`userKey`): creating an Html
banner or changing its html needs a user who has the permission. Site code such as an importer
passes the key of such a user (for example an administrator). A failed attempt for invalid data
carries a `BannerValidationException` in its `Exception`.

### `IBannerStatisticsService`

Only flushed counts are included, so they can be about a minute behind.

- `Task<Attempt<BannerReport, BannerOperationStatus>> GetReportAsync(BannerReportQuery query)`:
  `InvalidData` when the end is before the start or the period is longer than 366 days.
- `Task<Attempt<string, BannerOperationStatus>> GetCsvAsync(BannerReportQuery query)`: the same
  period as CSV text, one line per banner per zone per day; same failure statuses.

### `IBannerImportService` (FR-016)

- `Task<BannerImportResult> ImportStatisticsAsync(IEnumerable<DailyStatisticImport> rows, StatisticImportMode mode)`
  - `DailyStatisticImport` (a class with settable properties): `DateOnly Day` (UTC), `Guid BannerKey`,
    `Guid ZoneKey`, `long Impressions`, `long Clicks`.
  - `StatisticImportMode.Add` adds to existing rows; `Replace` overwrites the row's counts. Imported
    rows are marked as imported in reports.
  - Runs in batches of 1,000 rows, one transaction per batch. Returns
    `BannerImportResult(int Inserted, int Updated, int Rejected)`; rows with an unknown banner or
    zone key or a negative count are skipped and counted as rejected. If the database fails the
    exception is thrown; batches already committed stay.
  - After a `Replace` import the live banners are rebuilt, so a banner whose totals went down below
    its limit shows again.
- Banners and zones are imported with `IBannerZoneService` and `IBannerService` (create, save
  draft, publish), so validation and audit are the same as in the back office.

The package ships no importer for any specific CMS (constitution I).

### `IBannerSelector` (advanced use; namespace `Meresoft.Umbraco.Banners.Selection`)

- `IReadOnlyList<SelectedBanner> Select(string zoneAlias)`: up to the zone's show count of different
  banners, chosen from the in-memory snapshot only; empty when the zone is unknown or has no
  candidate. The alias is trimmed and lowercased first.
- `SelectedBanner` (record): `int BannerId`, `int ZoneId`, `string ImageUrl`, `int? Width`,
  `int? Height`, `string AltText`, `string? Title`, `string LinkUrl`, `bool OpenInNewWindow`,
  `string? Html` (stored markup of an Html banner, null for an image banner).

`Select` returns no token. A site that renders banners itself gets one from `IViewTokenProtector`.

### `IViewTokenProtector` (namespace `Meresoft.Umbraco.Banners.Tokens`)

The signed, expiring token that proves a banner was rendered; the public impression endpoint
accepts only a valid token (FR-011).

- `string Issue(int zoneId, IReadOnlyList<int> bannerIds)`: a token for the banners rendered in one
  zone. A custom renderer calls it once per zone view with `SelectedBanner.ZoneId` and the
  `BannerId` of every banner it writes, and puts the result in the `data-mbz-token` attribute of
  the zone element, as the built-in markup does (see `markup.md`). Without the token the
  package script reports nothing and no impression counts.
- `bool TryValidate(string? token, [NotNullWhen(true)] out ViewTokenPayload? payload)`: never
  throws; any bad, tampered or expired token gives false.
- `ViewTokenPayload(byte Version, int ZoneId, IReadOnlyList<int> BannerIds, byte[] Nonce)`: carries
  no visitor data.

### Rendering types (namespace `Meresoft.Umbraco.Banners.Rendering`)

The 1.0 surface, 1.0.0 being unpublished:

- `BannerZoneTagHelper` (`<banner-zone alias="...">`).
- `BannerZoneViewComponent`: `Task<IViewComponentResult> InvokeAsync(string alias, string? cssClass = null)`,
  called as `@await Component.InvokeAsync("BannerZone", new { alias = "search-top" })`.
- `BannerZoneRenderer.Render(requestItems, selection, cssClass, fallbackHtml, PathString pathBase = default)` and
  `BannerZoneHtmlWriter.Render(view, requestItems, PathString pathBase = default)`: the optional `pathBase` is
  `Request.PathBase`; every public address written (data-mb-base, click link, script src) starts with it.

### Plumbing types: public for DI only, not covered by SemVer

Every other public type is public only so that the dependency injection container, the Umbraco
composition, the controllers and the test projects can reach it. They are not extension points and
can change in any release: `ILiveSnapshotAccessor`, `LiveSnapshot`, `SnapshotZone`, `SnapshotBanner`,
the cache refresher types (`Caching`); `CounterBuffer`, `BotMatcher`, `RequestedZoneRecorder`
(`Counting`); `ReplaySet`, `ViewTokenProtector` (`Tokens`); `IBannerLinkLookup`, `ClickTarget`,
`SeenBodyParser`, the public endpoint controllers and models (`Delivery`); the management API
controllers and models (`ManagementApi`); migrations (`Persistence`). The main ones carry an XML
doc remark saying so.
