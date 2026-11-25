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
| PublicPath | `/_banners` | starts with `/`, no trailing `/`, must not start with `/umbraco`; validated at startup |
| DeliveryEndpointEnabled | true | false disables `GET {base}/zone/{alias}`; client mode zones then render only their fallback |
| FlushInterval | 1 minute | 5 seconds to 1 minute (FR-012 "at least once a minute") |
| ViewTokenLifetime | 10 minutes | 1 to 60 minutes; how long after rendering an impression can still be reported |
| CacheSafetyReloadInterval | 5 minutes | 1 to 60 minutes; periodic snapshot rebuild in case a refresh message was missed; 0 disables |
| Bots:AdditionalUserAgentPatterns | empty | case-insensitive substrings added to the shipped list |

Options are validated on start (`ValidateOnStart`); an invalid value stops the site with a clear
message naming the key.

## Public C# services (namespace `Meresoft.Umbraco.Banners.Services`)

Registered by the package composer; part of the stable public surface (constitution VI). All
methods are async and run inside an Umbraco scope.

### `IBannerZoneService`

- `Task<IReadOnlyList<BannerZone>> GetAllAsync()`
- `Task<BannerZone?> GetByAliasAsync(string alias)`
- `Task<Attempt<BannerZone, BannerOperationStatus>> CreateAsync(BannerZoneModel model, Guid userKey)`
- `Task<Attempt<BannerZone, BannerOperationStatus>> UpdateAsync(Guid key, BannerZoneModel model, Guid userKey)`
- `Task<BannerOperationStatus> DeleteAsync(Guid key, Guid userKey)`

### `IBannerService`

- `Task<Banner?> GetAsync(Guid key)`
- `Task<PagedModel<BannerListItem>> GetPagedAsync(BannerQuery query)`
- `Task<Attempt<Banner, BannerOperationStatus>> CreateAsync(BannerCreateModel model, Guid userKey)`
- `Task<Attempt<Banner, BannerOperationStatus>> SaveDraftAsync(Guid key, BannerDraftModel model, Guid userKey)`
- `Task<Attempt<Banner, BannerOperationStatus>> PublishAsync(Guid key, Guid userKey)`
- `Task<Attempt<Banner, BannerOperationStatus>> UnpublishAsync(Guid key, Guid userKey)`
- `Task<Attempt<Banner, BannerOperationStatus>> CopyAsync(Guid key, string? newName, Guid userKey)`
- `Task<BannerOperationStatus> DeleteAsync(Guid key, Guid userKey)`

The HTML permission is checked by the management API layer, not by the service, so a site's own
code (for example an importer) can create Html banners.

### `IBannerStatisticsService`

- `Task<BannerReport> GetReportAsync(BannerReportQuery query)`
- `Task WriteCsvAsync(BannerReportQuery query, Stream output)`

### `IBannerImportService` (FR-016)

- `Task<BannerImportResult> ImportStatisticsAsync(IEnumerable<DailyStatisticImport> rows, StatisticImportMode mode)`
  - `DailyStatisticImport`: `DateOnly Day`, `Guid BannerKey`, `Guid ZoneKey`, `long Impressions`,
    `long Clicks`.
  - `StatisticImportMode.Add` adds to existing rows; `Replace` overwrites the row's counts. Both set
    `IsImported = true`. Runs in batches of 1,000 rows per transaction; returns counts of inserted,
    updated and rejected rows (unknown banner or zone key, negative numbers).
- Banners and zones are imported with `IBannerZoneService` and `IBannerService` (create, save
  draft, publish), so validation and audit are the same as in the back office.

The package ships no importer for any specific CMS (constitution I).

### `IBannerSelector` (advanced use)

- `IReadOnlyList<SelectedBanner> Select(string zoneAlias)`: the same weighted choice the zone uses,
  for sites that render banners themselves. Callers must render the returned view token for
  impressions to count.
