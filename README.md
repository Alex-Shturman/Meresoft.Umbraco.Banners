# Meresoft.Umbraco.Banners

A free, MIT licensed banner manager for Umbraco 17 (.NET 10), on SQL Server or SQLite. Editors manage banner
zones and banners in a back office section; templates place a zone with one tag. All examples in this document
use invented data.

What it does: weighted rotation of banners in a zone, schedules, impression and click limits, draft and
publish, viewable impression and click statistics with a report and CSV export, optional HTML banners behind
their own permission, a client mode for cached pages, and a documented C# service for importing history.

Contents: [Install](#install), [Place a zone](#place-a-zone), [Editor guide](#editor-guide),
[Your first banner in 15 minutes](#your-first-banner-in-15-minutes), [Giving other groups access](#giving-other-groups-access),
[Configuration](#configuration), [Counting and statistics](#counting-and-statistics), [Importing statistics](#importing-statistics),
[Privacy](#privacy-statement), [Upgrade notes](#upgrade-notes), [Uninstall](#uninstall), [Reporting issues](#reporting-issues),
[Details](#details) and [End-to-end tests](#end-to-end-tests).

## Install

Requirements: Umbraco 17.8 or later 17.x, on .NET 10.

```bash
dotnet add package Meresoft.Umbraco.Banners
```

The package registers itself (an Umbraco composer), so there is nothing to add to `Program.cs`. Then:

1. Add the tag helper to `Views/_ViewImports.cshtml`:

   ```cshtml
   @addTagHelper *, Meresoft.Umbraco.Banners
   ```

2. Start the site. Umbraco runs the package migration, which creates the package tables and one row in
   `umbracoLock` (see [The package migration and its lock row](#the-package-migration-and-its-lock-row)). On a first
   install the Administrators group is given the "Banners" section permission and the "Edit HTML banners" permission.
3. Sign in to the back office and open the **Banners** section.

## Place a zone

A zone is a named place in a template. Put it where the banners should appear, with the tag helper:

```cshtml
<banner-zone alias="home-top" class="my-banners">
    <a href="/offers">See our offers</a> @* optional: shown when no banner is active *@
</banner-zone>
```

or with the view component, which has no fallback content:

```cshtml
@await Component.InvokeAsync("BannerZone", new { alias = "home-top", cssClass = "my-banners" })
```

`alias` is lower case letters, digits and hyphens (it is normalised). The zone writes nothing at all when it has
no active banner and no fallback. When a template asks for an alias that does not exist yet, the alias is listed
in the back office under "Placed but not set up", so an editor can create it with one click.

On a site with several websites the same alias shows the same banners everywhere. Use separate aliases (for
example `site-a-top` and `site-b-top`) to show different banners per site.

The package adds one script tag per page (`/meresoft-banners/banners.js`, under 5 KB) that reports viewable
impressions. Without JavaScript the banner is still shown and the link works, but no impression is counted.

The same banner can be assigned to two zones on one page: it is shown in both, and each zone counts its own
impression.

## Editor guide

**Zones.** Open Banners, then Zones, and create a zone: an alias (what the template uses), a name, how many banners
to show at once, an optional image width and height, and the render mode (see [Cached pages](#cached-pages)). A zone
that still has banners assigned cannot be deleted; the message lists them.

**Banners.** Create a banner of type Image (a picture from the media library, a link address, alt text) or Html
(needs the HTML permission). Set a weight (a banner with weight 3 is shown three times as often as one with
weight 1), the zones it appears in, an optional schedule (start and end, in UTC), and optional impression and click
limits. The list shows the state of each banner (draft, published, published with unpublished changes, scheduled,
expired) and the flag "Limit reached".

**Draft and publish.** Saving only stores a draft: the site does not change. Publish makes the draft live. Editing a
published banner and saving keeps the old version live until you publish again. The live site follows a publish
within a few seconds, also on several servers. Copy makes a draft copy of a banner; Unpublish takes it off the site.

**Reports.** Open Banners, then Reports. Choose a period (today, last 7 days, last 30 days, this month, last month
or a custom period of at most 366 days), optionally one zone or one banner. You get totals, a table per banner and
zone with impressions, clicks and click rate, a daily chart (with a text alternative for screen readers) and a CSV
export. Counts are a minute or so behind the site, because they are written in batches. Days are UTC days. Days
that were imported are marked.

**History.** Every change is written to Umbraco's audit log and shown on the History tab of a banner or a zone.

## Your first banner in 15 minutes

1. Install the package and add the `@addTagHelper` line (2 minutes).
2. Put `<banner-zone alias="home-top"></banner-zone>` in a template and open the page once (1 minute).
3. In Banners, open "Placed but not set up" and create the zone `home-top` from the list (1 minute).
4. Create an Image banner: pick an image, enter `https://example.org/show` as the link, add alt text, assign
   `home-top` (3 minutes).
5. Publish. Reload the page: the banner is there (1 minute).
6. After a minute, open Reports for today: the impression you caused is in the totals.

## Giving other groups access

Only Administrators get the package permissions on a first install, and the package never changes group permissions on
later upgrades. To give another group access, open Users, then User groups, and:

- under Sections, tick **Banners**: the group sees the section and can manage zones and banners;
- under the **Banners** permissions, tick **Edit HTML banners** only for people you trust with the code of your
  pages. Without it an editor can still publish, unpublish, copy and delete HTML banners and read their statistics,
  but cannot create them or change their HTML.

HTML banners are shown on the site exactly as stored, scripts included.

## Configuration

Settings live in the `Meresoft:Banners` section of `appsettings.json`. All keys are optional; an invalid value stops
the site at start with a message that names the key.

```json
{
  "Meresoft": {
    "Banners": {
      "PublicPath": "/_banners",
      "DeliveryEndpointEnabled": true,
      "FlushInterval": "00:01:00",
      "ViewTokenLifetime": "00:10:00",
      "CacheSafetyReloadInterval": "00:05:00",
      "Bots": { "AdditionalUserAgentPatterns": [ "my-monitoring-agent" ] }
    }
  }
}
```

| Key | Default | Rules |
|---|---|---|
| `PublicPath` | `/_banners` | Starts with `/`, no trailing `/`, no `//`, no `{ } ? # %` and no whitespace, and must not start with `/umbraco`. The click, seen and zone delivery endpoints live under it. A site in a virtual directory (`Request.PathBase`) gets the path base in front of every public link, the script address and `data-mb-base`. |
| `DeliveryEndpointEnabled` | `true` | `false` turns off `GET {PublicPath}/zone/{alias}`; client mode zones then show only their fallback. |
| `FlushInterval` | 1 minute | 5 seconds to 1 minute. How often counted impressions and clicks are written to the database. |
| `ViewTokenLifetime` | 10 minutes | 1 to 60 minutes. How long after a page was rendered an impression can still be reported. |
| `CacheSafetyReloadInterval` | 5 minutes | 1 to 60 minutes, or `0` to switch off. A periodic rebuild of the live banner list, in case a refresh message was missed. |
| `Bots:AdditionalUserAgentPatterns` | none | Case-insensitive parts of user agents added to the shipped bot list. |

## Counting and statistics

- **Viewable impressions.** An impression counts when the banner has been on screen for one second, reported by the
  package script. Visitors without JavaScript are not counted, so the numbers are lower than "times sent with a page".
  History imported from another system is comparable only when that system used a similar viewability rule (a system
  that counted sends is not comparable). Reports mark imported days.
- **UTC days.** Statistics are grouped by UTC day, not by the day of the visitor or the site.
- **Limits can be overshot a little.** Impression and click limits are checked after each flush, so a banner can pass
  its limit by up to one flush interval of views (a minute by default). On several servers each server flushes by itself.
- **Writes are one flush pass per interval.** Each server writes its counts in one flush pass per flush interval. Inside
  a pass every counted row (one banner in one zone for one day) is written in its own short transaction, so a failure on
  one row, for example two servers inserting the same new row at once, does not roll back or lose the others; a row that
  cannot be written is kept for the next pass. So "one write batch per interval" means one pass, not one transaction.
- **The same banner in two zones** is counted per zone and per banner; a report row is one banner in one zone.
- **Counts can be faked.** Anyone can get fresh valid view tokens for free, from any page view or from the zone
  address that client mode uses, and the click address is a plain GET. The replay set stops accidental repeats; it
  does not defend against token farming. The defences are the bot filter and the viewability rule (one second on
  screen, reported by the script), and none of them stops a determined person. Treat the numbers as a measure for
  honest traffic, not as proof for billing.

The sections [Clicks and click rate](#clicks-and-click-rate), [View tokens on several servers and after a restart](#view-tokens-on-several-servers-and-after-a-restart)
and [Cached pages](#cached-pages) below say what the counts can and cannot tell you.

## Importing statistics

The package ships no importer for any specific CMS. A site writes a small importer of its own and feeds history to
`IBannerImportService`. Create the zones and banners first with `IBannerZoneService` and `IBannerService` (create, save
draft, publish), which gives the same validation and audit as the back office, then import the daily counts:

```csharp
public sealed class OldStatisticsImporter(IBannerImportService import)
{
    public async Task RunAsync(Guid bannerKey, Guid zoneKey)
    {
        DailyStatisticImport[] rows =
        [
            new() { Day = new DateOnly(2026, 3, 1), BannerKey = bannerKey, ZoneKey = zoneKey, Impressions = 1200, Clicks = 18 },
            new() { Day = new DateOnly(2026, 3, 2), BannerKey = bannerKey, ZoneKey = zoneKey, Impressions = 1340, Clicks = 21 },
        ];

        BannerImportResult result = await import.ImportStatisticsAsync(rows, StatisticImportMode.Add);
        // result.Inserted, result.Updated and result.Rejected (unknown banner or zone key, negative numbers, null entries)
    }
}
```

`Add` adds to the rows that exist, `Replace` overwrites their counts. Both mark the rows as imported. Rows are written
in batches of 1,000, one transaction per batch; if the database fails, the exception is thrown and the batches that
were already committed stay, so rerun with `Replace` to fix a half done import. Limits count imported totals too, and
they apply right after the import: an `Add` import that raises a banner over its limit takes it off the site at once,
and a `Replace` import rebuilds the live banner list, so a banner whose totals went down below its limit shows again.

## Privacy statement

The package does not store or log IP addresses, user agents, cookies, member or visitor identifiers, and it sets no
cookies. Request headers (user agent, fetch metadata) are read in memory to filter bots and prefetches and are
not kept. What is stored: per day, banner and zone, the number of impressions and clicks. The audit log holds the
user, time and action of back office changes, and no banner content. The view token on a page is signed with the
site's data protection keys and carries no visitor data. The server keeps, in memory only and until the token
expires, a random token number and which of its banners were counted; this holds no visitor data and is never
written to disk or to the log.

## Upgrade notes

- Upgrading applies new package migrations when Umbraco runs its package migrations; nothing in Umbraco core data
  changes. Start the site once after an upgrade before editors save anything.
- The package never changes user group permissions on an upgrade. A new permission is given to groups by an
  administrator in the user group settings.
- Release notes are in `CHANGELOG.md`. Version 1.0.0 is the first release.

## Uninstall

The package has no automatic uninstall. In this order:

1. Remove the zone markup (`<banner-zone ...>` and `Component.InvokeAsync("BannerZone", ...)`) from the templates and
   the `@addTagHelper *, Meresoft.Umbraco.Banners` line from `_ViewImports.cshtml`.
2. Remove the package (`dotnet remove package Meresoft.Umbraco.Banners`) and deploy.
3. Remove the package tables and the lock row, as described in
   [The package migration and its lock row](#the-package-migration-and-its-lock-row).
4. Remove what the package left in Umbraco's own tables. Removing the package does not delete these, and they do no harm
   if they stay (Umbraco ignores a section or permission it does not know), but you can clear them:

   ```sql
   DELETE FROM umbracoUserGroup2App WHERE app = 'Meresoft.Banners.Section';
   DELETE FROM umbracoUserGroup2Permission WHERE permission = 'Meresoft.Banners.Html';
   DELETE FROM umbracoLog WHERE entityType IN ('meresoft-banner', 'meresoft-banner-zone') AND NodeId < -1000000000;
   ```

   The first two are the section access and the "Edit HTML banners" permission of the user groups. The last one is the
   audit history of banners and zones; keep it if you want to keep that record. The package stores these rows with a
   node id below -1,000,000,000 (the banner id counted down from there), so they never show in the history of a
   document or media item that has the same number as a banner.

## Reporting issues

Report bugs and ask for features at <https://github.com/meresoft/Meresoft.Umbraco.Banners/issues>. Say which Umbraco
and package version you use, whether it is SQL Server or SQLite, and what you did and saw. Do not paste connection
strings, keys or visitor data.

## Details

### Image addresses

The image address of a banner is resolved when the banner snapshot is built (on publish, on media
changes and by a periodic safety reload), not while a page renders. The snapshot is built outside a
web request, so the package asks each registered Umbraco media URL provider (`IMediaUrlProvider`) for
the address of the media file in relative mode, and uses the first answer. A provider that returns an
absolute address, for example a CDN provider, is honoured. When no provider answers, the stored
`umbracoFile` path is used as it is.

Limits: the package does not use the aggregate `UrlProvider`, because that needs a request URL. A
provider that needs the current request (for example one that picks a host by the visitor's domain)
cannot answer for a snapshot that is shared by all visitors. Address changes of such a provider show
after the next snapshot rebuild.

### Clicks and click rate

A banner link points at the package's click address (`{PublicPath}/c/{bannerId}?z={zoneId}`), which
redirects to the stored link. A click is counted only when the request looks like a navigation by a
person: the browser sends no fetch metadata, or `Sec-Fetch-Mode: navigate` and
`Sec-Fetch-Dest: document`, and it is not a prefetch (`Sec-Purpose` or `Purpose` is `prefetch`), a
known bot, and the banner is published in that zone. Other requests are redirected but not counted.
The package reads these headers in memory only and stores nothing from them.

The click address is a plain GET, so anyone can call it directly (a script, a link checker that
looks like a browser, or someone pasting the address many times). Such requests can inflate the
click count and so the click rate. Impressions are protected by a signed, single use view token; clicks
are not, because a click must work without JavaScript and on cached pages.

### View tokens on several servers and after a restart

An impression is reported with a signed view token that the page carries. Each server remembers, in
memory only, which banners of which tokens it has counted (one entry per token, with a small bit mask for
its banners), so a repeated report counts once. That memory
has three consequences:

- A report replayed to a different server is counted once more per server. Replaying needs deliberate
  effort and is bounded by the number of servers and the token lifetime (10 minutes by default).
- A restart empties the memory. A token that was already counted can be counted again after the
  restart, as long as it has not expired. The same bound applies.
- The memory holds at most 100,000 tokens, which is a few megabytes. When it is full, the entries closest to
  expiry are dropped to make room, so a flood of tokens cannot keep real impressions out. The price is that a
  dropped token that is replayed before it expires can be counted once more. This is a deliberate trade: fresh
  tokens are free, so replaying a dropped one gives an attacker nothing they could not get anyway, while refusing
  new tokens when the memory is full would let the same attacker suppress every real impression. Only the entries
  that expire soonest are dropped, never the newest. The log gets one warning (counts
  only) each time the memory fills up.

Tokens are checked with the site's data protection keys, so servers behind a load balancer must share
the key ring, which Umbraco needs for back office sign-in anyway.

### Cached pages

Right after a site start, before the first banner list is built, a zone waits for the build for up to 2 seconds. If it
is still not built after that, the zone is left empty and the page is sent with `Cache-Control: no-store` (when its
headers have not started), so a cache does not keep the empty answer.

In server mode a zone is written into the page, together with its view token. When the whole page is
cached (output cache, a CDN), every visitor receives the same markup and the same token, so the token is
replayed. The first report counts and the later ones are refused as repeats, so impressions on a cached
page are counted once per token lifetime and per server, not once per visitor. Use client mode for
cached pages: the browser asks for banners and a fresh token on every view. Choose it in the zone's
"Render mode" setting. Without JavaScript, or in a browser without `IntersectionObserver` and `fetch`,
visitors see the fallback content of the zone (nothing, if the tag has none).

Client zones have no reserved height: the placeholder is empty until the banner arrives, so the page
moves when it does. Set a `min-height` for the zone to avoid the layout shift, for example:

```css
.mb-zone[data-mbz-mode="client"] { min-height: 90px; } /* the height of your banner */
```

Zones that differ in height can use their own selector, for example `.mb-zone[data-mbz="sidebar"]`.

### The package migration and its lock row

The package writes its own tables and takes a write lock before every write, so that the flush job and
editors never wait on each other in a way that stalls SQLite. The lock is a row in Umbraco's `umbracoLock`
table, named `Meresoft.Banners.Write` (id -8201001), added by the package migration. The migration must run:
let Umbraco run package migrations (unattended, or from the back office). If it has not run, every write of
the package stops with an error that names the migration, and nothing is written.

The package has no automatic uninstall. To remove it, take the zone markup and the `@addTagHelper` line out
of the templates first (see [Uninstall](#uninstall)), then remove the package tables and the lock row. The lock row
belongs to the package and is the only row it adds to an Umbraco table:

```sql
DROP TABLE meresoftBannersZoneAssignment;
DROP TABLE meresoftBannersDailyStat;
DROP TABLE meresoftBannersBannerVersion;
DROP TABLE meresoftBannersRequestedZone;
DROP TABLE meresoftBannersBanner;
DROP TABLE meresoftBannersZone;
DELETE FROM umbracoLock WHERE id = -8201001;
```

The migration state is kept by Umbraco in `umbracoKeyValue` under a key that ends with the plan name
`Meresoft.Umbraco.Banners`; remove that row too if you plan to install the package again on the same database.

### History and permissions

Every change to a banner or a zone (created, saved, published, unpublished, copied, deleted) is written
to Umbraco's own audit log, with the user, the time and the action only. No banner content goes into
it. The back office shows it as the History tab of a banner and of a zone, newest change first.

**How long history is kept.** Umbraco's log cleanup job (the log scrubber, every few hours, using
`Umbraco:CMS:Logging:MaxLogAge`, one day by default) removes only audit rows whose header is `open` or
`system`. The rows of this package use the headers `New`, `Save`, `Publish`, `Unpublish`, `Copy` and
`Delete`, so they are not removed by it: editors see the full history of a banner or zone for as long
as it exists (an integration test pins this for the Umbraco version in use). History is kept after a
banner is deleted but is no longer shown in the back office. Only a manual clean-up of the `umbracoLog`
table, or a change in a later Umbraco version, shortens it. The history view reads up to 10,000 entries
back, 100 per request.

**Permissions.** Access to the section is the user group setting "Banners" (section access). Creating
HTML banners and changing their HTML needs the extra "Edit HTML banners" permission, shown in the user
group editor under "Banners". Without it an editor can still publish, unpublish, copy and delete HTML
banners and read their statistics. On a fresh install only the Administrators group gets both. The package never
changes group permissions on later upgrades, so other groups are given access in the user group settings.

HTML banners are shown on the site exactly as stored, scripts included, so only give the HTML
permission to people you trust with the code of your pages.

## Integration tests on SQL Server

The integration tests run on SQLite. Set `MB_TEST_SQLSERVER` to the connection string of the master database of a SQL
Server (never commit it) and the same suite runs on SQL Server, every test carrying the category `SqlServer`:

```bash
MB_TEST_SQLSERVER="<connection string>" dotnet test tests/Meresoft.Umbraco.Banners.Tests.Integration --filter Category=SqlServer
```

Without the variable the category belongs only to the test that needs SQL Server and is skipped on SQLite.

## End-to-end tests

The browser tests in `tests/e2e` (Playwright) run against the sample site in `samples/`, never against a real
site. Start the sample site (see `samples/README.md`), then run `npx playwright test --workers=1` in `tests/e2e`.

Environment variables:

- `MB_SAMPLE_BASE_URL`: the sample site, `http://localhost:5100` by default.
- `MB_SAMPLE_ADMIN_EMAIL` and `MB_SAMPLE_ADMIN_PASSWORD`: the sample site's unattended administrator.
- `MB_SAMPLE_BANNER_EDITOR_EMAIL`, `MB_SAMPLE_BANNER_EDITOR_PASSWORD`, `MB_SAMPLE_EDITOR_EMAIL` and
  `MB_SAMPLE_EDITOR_PASSWORD`: the permission tests' users, the ones the sample site creates.

**The run is destructive.** Before the first spec, `tests/e2e/support/global-reset.ts` deletes every banner and
zone of the site, so a rerun on a used sample database starts like a fresh one. Statistics stay.

**The guard.** The run refuses to start (nothing is started, not even the sample site) unless `MB_SAMPLE_BASE_URL` is
on this machine: `localhost`, `127.0.0.1` or `[::1]`. A remote sample site needs the explicit override
`MB_E2E_ALLOW_REMOTE_RESET=1`. Never point the tests at a site whose banners you want to keep.

**One site instance per database.** Never run two instances of the sample site on the same database and folder. A second
instance, also one that Playwright starts because `MB_SAMPLE_BASE_URL` points where no site runs, takes over Umbraco's
main domain. The first site then keeps serving pages, but its background jobs stop (the flush job that writes requested
zones and counts), and the specs that wait for those time out. The configuration refuses to load without the
administrator variables, so a mistyped run starts nothing.
