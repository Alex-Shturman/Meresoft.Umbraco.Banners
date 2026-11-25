# Meresoft.Umbraco.Banners

A free, MIT licensed banner manager for Umbraco 17 (.NET 10). Editors manage banner zones and
banners in a back office section; templates place a zone with one tag.

This package is under development. Install, configuration and editor guide will be added before
the first release. See `specs/001-banner-manager/` for the specification.

## Configuration notes

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
memory only, which token and banner pairs it has counted, so a repeated report counts once. That memory
has three consequences:

- A report replayed to a different server is counted once more per server. Replaying needs deliberate
  effort and is bounded by the number of servers and the token lifetime (10 minutes by default).
- A restart empties the memory. A token that was already counted can be counted again after the
  restart, as long as it has not expired. The same bound applies.
- The memory holds at most 250,000 pairs. When it is full it refuses new reports until entries expire,
  and the log gets one warning (counts only) each time it fills up.

Tokens are checked with the site's data protection keys, so servers behind a load balancer must share
the key ring, which Umbraco needs for back office sign-in anyway.

### Cached pages

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
of the templates first, then remove the package tables and the lock row, which belongs to the package and
is the only row it adds to an Umbraco table:

```sql
DELETE FROM umbracoLock WHERE id = -8201001;
```

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
