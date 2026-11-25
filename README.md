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
cached pages: the browser asks for banners and a fresh token on every view. Client mode comes with the
next release phase (the zone setting is not offered yet). Until then, do not cache pages that carry a
banner zone if impression counts matter.
