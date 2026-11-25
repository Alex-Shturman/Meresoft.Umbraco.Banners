# Contract: management API (back office)

Routed under the Umbraco management API: `/umbraco/management/api/v1/meresoft-banners/...`,
documented in its own OpenAPI document `meresoft-banners` (research R4). Back office bearer auth
only.

Authorization:

- Every endpoint requires the user to have access to the Banners section
  (`Meresoft.Banners.Section`); otherwise 403 (US6 scenario 1).
- Changing the `html` field of an Html banner, or creating an Html banner, also requires the HTML
  banner permission (`Meresoft.Banners.Html`); otherwise 403 with problem details
  `type: html-permission-required` (FR-006, US6 scenario 2). Publish, unpublish, copy, delete and
  statistics of Html banners need only section access.

Errors use RFC 9457 problem details. Validation errors: 400 with `errors` per field.
Concurrency: `PUT` takes the `updateDate` the client loaded; a stale value gives 409
`type: changed-by-another-user` (concurrent edits by two editors).

Every write records an audit entry (FR-018).

## Zones

| Method and path | Body / query | Result |
|---|---|---|
| GET `/zones` | | 200 list: key, alias, name, description, showCount, imageWidth, imageHeight, renderMode, activeBannerCount |
| GET `/zones/{key}` | | 200 zone, 404 |
| POST `/zones` | alias, name, description?, showCount, imageWidth?, imageHeight?, renderMode | 201 + Location; 400 invalid; 409 alias taken. Removes a matching requested zone. |
| PUT `/zones/{key}` | same fields + updateDate | 200; 400; 404; 409 |
| DELETE `/zones/{key}` | | 204; 409 `type: zone-in-use` with the assigned banner keys (FR-007) |
| GET `/requested-zones` | | 200 list: alias, firstSeen, lastSeen (FR-003) |
| DELETE `/requested-zones/{alias}` | | 204 (dismiss) |

## Banners

| Method and path | Body / query | Result |
|---|---|---|
| GET `/banners` | `zone?`, `state?` (draft, published, changed, unpublished), `type?`, `skip`, `take` (max 100) | 200 page: key, name, type, state, flags (notPlaced, imageMissing, scheduled, expired, limitReached), zone keys, weight, updateDate |
| GET `/banners/{key}` | | 200: key, name, type, state, flags, `draft` and `live` (live null if never published), createDate, updateDate, publishDate |
| POST `/banners` | name, type, draft fields | 201 (state Draft) |
| PUT `/banners/{key}` | name, draft fields, updateDate | 200 (saves the draft only; the site does not change, US1 scenario 5) |
| POST `/banners/{key}/publish` | updateDate | 200; 400 if the draft is invalid or has no zone |
| POST `/banners/{key}/unpublish` | | 200 |
| POST `/banners/{key}/copy` | name? | 201: new banner in Draft with a copy of the draft fields and assignments, no statistics |
| DELETE `/banners/{key}` | | 204; statistics kept (FR-007) |
| GET `/banners/{key}/history` | `skip`, `take` | 200: audit entries (user name, action, date) |

Draft fields: mediaKey (image), html (html), linkUrl, openInNewWindow, altText, title, weight,
startUtc, endUtc, impressionLimit, clickLimit, zoneKeys[]. Dates are sent and returned in UTC; the
back office converts to and from the user's time zone.

Validation (also used by the import service): see data-model.md column rules. `linkUrl` must be an
absolute `http` or `https` URL (edge case "link not valid").

## Reports

| Method and path | Query | Result |
|---|---|---|
| GET `/reports` | `from`, `to` (UTC dates, inclusive, max 366 days), `zone?`, `banner?` | 200: totals {impressions, clicks, ctr}, per banner per zone rows {bannerKey or null, bannerName or "(deleted banner)", zoneAlias or "(deleted zone)", impressions, clicks, ctr}, daily series [{day, impressions, clicks, imported}] |
| GET `/reports/csv` | same | 200 `text/csv; charset=utf-8`, file name `banners-{from}-{to}.csv`, header `date,zone,banner,impressions,clicks`, one row per banner per zone per day (US3 scenario 5); values that start with `=`, `+`, `-` or `@` are prefixed with `'` (CSV injection) |

- CTR is clicks / impressions, rounded to one decimal percent; 0 impressions gives null (shown as
  a dash).
- Counts include only flushed rows; the report states "counts may be up to one minute behind".
- Presets (today, last 7 days, last 30 days, this month, last month) are computed by the back office
  in UTC days and sent as `from` and `to`.
