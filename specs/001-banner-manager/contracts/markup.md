# Contract: template markup (tag helper and view component)

Both forms are part of the stable public surface (constitution VI, FR-002).

## Tag helper

Registration once in `Views/_ViewImports.cshtml`:

```cshtml
@addTagHelper *, Meresoft.Umbraco.Banners
```

Use:

```cshtml
<banner-zone alias="search-top" />

<banner-zone alias="sidebar" class="my-ads">
    <p>Fallback content shown when no banner is active.</p>
</banner-zone>
```

| Attribute | Required | Meaning |
|---|---|---|
| `alias` | yes | zone alias; normalised (trimmed, lowercased) |
| `class` | no | extra CSS classes added to the zone element |
| child content | no | fallback content, rendered when the zone has no active banner, when the zone does not exist, and inside the client mode placeholder until banners arrive |

## View component

```cshtml
@await Component.InvokeAsync("BannerZone", new { alias = "search-top", cssClass = "my-ads" })
```

Same behaviour as the tag helper without fallback content.

## Rendered HTML (server mode, one image banner)

```html
<div class="mb-zone my-ads" data-mbz="search-top" data-mb-base="/_banners" data-mbz-token="CfDJ8...">
  <div class="mb-banner" data-mbb="12">
    <a href="/_banners/c/12?z=3" rel="sponsored noopener" target="_blank">
      <img src="/media/abc/banner.png?width=728&amp;height=90" alt="Invented boat show 2027"
           width="728" height="90" loading="lazy" decoding="async">
    </a>
  </div>
</div>
<script src="/meresoft-banners/banners.js" defer></script>
```

- `target="_blank"` only when the banner is set to open in a new window (FR-005).
- `title` attribute on the link only when the banner has a title.
- Html banners render `<div class="mb-banner mb-html" data-mbb="15">{stored html}</div>` (FR-006).
- The `<script>` tag is written once per page, by the first zone.
- `data-mb-base` holds the configured `Meresoft:Banners:PublicPath` (default `/_banners`) on every
  zone element; the package script reads its endpoint base from it, because the script itself is
  served from the fixed static path `/meresoft-banners/banners.js`.
- No active banner and no fallback: nothing at all is written (no empty element, no script).

## Rendered HTML (client mode)

```html
<div class="mb-zone" data-mbz="search-top" data-mb-base="/_banners" data-mbz-mode="client">
  <!-- fallback content, if any -->
</div>
<script src="/meresoft-banners/banners.js" defer></script>
```

The placeholder is always written in client mode, because the server does not know which banners
the browser will get. The page itself can be cached.

## CSS

The package ships no stylesheet. The classes `mb-zone`, `mb-banner` and `mb-html` are stable hooks
for site styles.
