# Meresoft Banners

Meresoft Banners lets you manage banners on your website without leaving Umbraco. Editors create banner zones and
banners in their own section of the back office, schedule them, rotate several banners in one zone by weight, set
impression and click limits, and see how each banner performs in a daily report with CSV export.

The goal is not just to put an image on a page, but to let editors handle banners the way they handle their content:
with drafts, publishing, permissions and a history of changes. Saving a banner never changes the live site, only
publishing does, and HTML banners have their own permission, so not everyone can put scripts on your pages.

## Privacy first

The package sets no cookies and stores no IP addresses, user agents or visitor identifiers. All it keeps is a daily
count of impressions and clicks per banner and zone. An impression only counts when a banner has actually been on
screen for one second, so the numbers reflect what visitors saw, not just what the server sent.

## What you get

- Image banners from the media library, or HTML banners behind their own permission
- Weighted rotation and several banners per zone
- Schedules, impression limits and click limits
- Draft and publish, copy, unpublish, and every change in the Umbraco audit log
- Reports per day, zone and banner, with a chart and CSV export
- A client mode for pages behind output cache or a CDN
- Works with load balancing, on SQL Server or SQLite
- `IBannerImportService` to bring in statistics history from another system

## Get started

Requires Umbraco 17.8 or later 17.x on .NET 10.

```bash
dotnet add package Meresoft.Umbraco.Banners
```

Add the tag helper to `Views/_ViewImports.cshtml`:

```cshtml
@addTagHelper *, Meresoft.Umbraco.Banners
```

Place a zone in a template:

```cshtml
<banner-zone alias="home-top">
    <a href="/offers">See our offers</a> @* optional: shown when no banner is active *@
</banner-zone>
```

Start the site and open the **Banners** section. A zone that a template uses but that does not exist yet is listed
under "Placed but not set up", so an editor can create it with one click.

The full documentation, including configuration, permissions, counting rules and upgrade notes, is in the
[README on GitHub](https://github.com/Alex-Shturman/Meresoft.Umbraco.Banners#readme).

Free and MIT licensed. Questions and ideas are welcome on the
[Umbraco forum](https://forum.umbraco.com/t/meresoft-banners-a-free-banner-manager-for-umbraco-17/8769), bugs on the
[issue tracker](https://github.com/Alex-Shturman/Meresoft.Umbraco.Banners/issues).
