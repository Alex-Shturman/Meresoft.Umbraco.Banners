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
