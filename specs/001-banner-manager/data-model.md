# Data Model: Banner manager (001)

All tables are created by the package migration plan (see research R3) and never touch Umbraco core
tables. Names use the prefix `meresoftBanners`. Times are UTC. Types are given as portable types
that map to SQL Server and SQLite through the Umbraco persistence layer.

## Persistent entities

### Zone (`meresoftBannersZone`)

| Column | Type | Rules |
|---|---|---|
| Id | int, identity, PK | internal id, used in statistics and click addresses |
| Key | guid, unique | id used by the management API |
| Alias | string(100), unique | lowercase `a-z`, `0-9`, `-`, `_`; 1 to 100 chars; starts with a letter; used in templates (FR-001, FR-002) |
| Name | string(255) | required |
| Description | string(1000), null | |
| ShowCount | int | 1 to 10, default 1 (FR-001) |
| ImageWidth | int, null | 1 to 4000 |
| ImageHeight | int, null | 1 to 4000 |
| RenderMode | tinyint | 0 = Server, 1 = Client (FR-001, FR-010) |
| CreateDate, UpdateDate | datetime | UTC |

- Zones are global to the install (clarification 2).
- Delete is refused (409) while any banner version, draft or live, is assigned to the zone (FR-007).
  Statistics keep the zone id; reports show "(deleted zone)" for an id with no zone row.
- Renaming changes `Name` only; changing `Alias` is allowed and is shown with a warning that
  templates using the old alias stop showing the zone.

### Banner (`meresoftBannersBanner`)

| Column | Type | Rules |
|---|---|---|
| Id | int, identity, PK | used in statistics, click addresses and view tokens |
| Key | guid, unique | management API id |
| Name | string(255) | required, back office only |
| Type | tinyint | 0 = Image, 1 = Html; fixed after creation |
| IsPublished | bool | true while the live version is shown on the site |
| CreateDate, UpdateDate | datetime | |
| CreatedBy, UpdatedBy | guid | back office user keys |
| PublishDate | datetime, null | last publish |

### Banner version (`meresoftBannersBannerVersion`)

Each banner has at most two versions: one Draft and one Live (FR-004a).

| Column | Type | Rules |
|---|---|---|
| Id | int, identity, PK | |
| BannerId | int, FK Banner (cascade delete) | |
| Kind | tinyint | 0 = Draft, 1 = Live; unique (BannerId, Kind) |
| MediaKey | guid, null | required for Image banners; an Umbraco media item key |
| Html | text, null | required for Html banners; changed only with the HTML permission (FR-006) |
| LinkUrl | string(2048), null | required for Image banners; absolute `http` or `https` URL, otherwise save is refused |
| OpenInNewWindow | bool | default false |
| AltText | string(255) | required for Image banners |
| Title | string(255), null | |
| Weight | int | 1 to 100, default 1 |
| StartUtc, EndUtc | datetime, null | when both are set, End > Start |
| ImpressionLimit | bigint, null | >= 1 |
| ClickLimit | bigint, null | >= 1 |
| SavedDate | datetime | |
| SavedBy | guid | |

### Zone assignment (`meresoftBannersBannerZone`)

| Column | Type | Rules |
|---|---|---|
| BannerVersionId | int, FK Banner version (cascade delete) | composite PK |
| ZoneId | int, FK Zone (no cascade) | composite PK |

Assignments belong to a version, so the draft can change zones without affecting the site until
Publish. Weight and limits are on the version, so they are shared by all assigned zones
(clarification 1).

### Daily statistic (`meresoftBannersDailyStat`)

| Column | Type | Rules |
|---|---|---|
| Day | date | UTC day |
| BannerId | int | no FK: rows survive banner deletion (FR-007) |
| ZoneId | int | no FK: rows survive zone deletion |
| Impressions | bigint | viewable impressions (FR-011) |
| Clicks | bigint | |
| IsImported | bool | true when any part of the row came from the import service (FR-016); the report marks such days |

- PK (Day, BannerId, ZoneId); index (BannerId, Day).
- Written only by the counter flush and the import service, never by a page request (FR-012).
- Flush adds to existing values (`Impressions = Impressions + @delta`), so several servers add
  their counts to the same rows without loss or doubling (research R7).

### Requested zone (`meresoftBannersRequestedZone`)

| Column | Type | Rules |
|---|---|---|
| Alias | string(100), PK | normalised alias requested by a template |
| FirstSeen, LastSeen | datetime | |

Filled by the flush job from an in-memory set (FR-003); the row is removed when a zone with that
alias is created.

## Derived states

### Banner publish state (FR-004a)

| State | Rule |
|---|---|
| Draft | no Live version |
| Published | Live version exists, `IsPublished`, draft equal to live (`Draft.SavedDate <= PublishDate`) |
| Published with unpublished changes | Live version exists, `IsPublished`, `Draft.SavedDate > PublishDate` |
| Unpublished | Live version exists, not `IsPublished` |

Transitions:

```text
create ──> Draft ──publish──> Published ──save──> Published with changes ──publish──> Published
                                 │  ▲                       │
                          unpublish  publish            unpublish
                                 ▼  │                       ▼
                              Unpublished <─────────────────┘
any state ──delete──> (row removed, statistics kept)
```

- Publish copies the Draft version (fields and assignments) over the Live version in one scope,
  sets `IsPublished = true`, `PublishDate = now`, and triggers a distributed cache refresh (FR-009).
- Unpublish sets `IsPublished = false` and triggers a cache refresh; the Live row stays so a click
  on an old cached page still redirects to the stored link, but nothing is counted.
- Publish is refused if the draft fails validation or has no zone assignment.

### Banner runtime flags (shown in the back office list)

| Flag | Rule |
|---|---|
| Not placed | the live (or, for drafts, the draft) version has no zone assignment |
| Image missing | Image banner whose media item no longer exists in the published media cache |
| Scheduled | now < StartUtc |
| Expired | now >= EndUtc |
| Limit reached | total impressions >= ImpressionLimit or total clicks >= ClickLimit, totals summed over all zones and days including imported rows |

### Active banner (selection, FR-008)

A banner is active in zone Z when: `IsPublished`, Live version assigned to Z, inside
[StartUtc, EndUtc), not Limit reached, and for Image banners the media item exists.

## In-memory structures (not stored)

| Structure | Contents | Lifetime |
|---|---|---|
| Live snapshot | immutable map zone alias -> zone settings and active candidate banners with pre-built image URL, link, alt, title, weight, schedule; plus totals for limited banners | rebuilt on publish, unpublish, zone change, media change, limit check after flush, and a safety reload interval |
| Counter buffer | (Day, BannerId, ZoneId) -> impressions, clicks, updated with atomic increments | swapped and written at every flush and on clean shutdown |
| Requested zone set | aliases asked for by templates with no zone | written at flush |
| View token replay set | (token nonce, bannerId) pairs already counted | until the token expires (default 10 minutes) |

## View token

Issued once per rendered zone view (server mode render or client mode delivery response). It is a
data protection payload (time limited) containing: format version, zone id, the ids of the banners
shown in that view, a random 96 bit nonce and the expiry. It carries no visitor data and is not
stored. An impression report `(token, bannerId)` is counted only when the token is valid and not
expired, the banner id is one of the token's banners, and the pair is not in the replay set
(research R6).
