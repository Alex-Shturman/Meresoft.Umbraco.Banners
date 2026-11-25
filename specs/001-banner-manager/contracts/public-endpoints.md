# Contract: public endpoints (anonymous, front end)

Base path: `Meresoft:Banners:PublicPath`, default `/_banners` (see configuration.md). All three
endpoints are part of the stable public surface (constitution VI). None of them sets cookies, reads
cookies or logs IP addresses or user agents (FR-014). The user agent is read only in memory to apply
the bot filter (FR-013).

All responses carry `Cache-Control: no-store` and `X-Robots-Tag: noindex, nofollow`.

## GET `{base}/c/{bannerId}?z={zoneId}` : click

| Case | Response | Counted |
|---|---|---|
| Banner exists, has a Live version with a link, published, Live version assigned to zone `z`, not a bot | 302 `Location: <stored LinkUrl>` | 1 click for (today, banner, zone) |
| Banner exists with a Live link but unpublished, or `z` missing, unknown or not assigned, or a bot | 302 `Location: <stored LinkUrl>` | nothing |
| Banner id unknown, or banner has no Live version, or Html banner | 404 | nothing |

- The target is always the stored `LinkUrl` of that banner. Any other query parameter (for example
  `url`, `returnUrl`, `to`) is ignored; there is no way to redirect elsewhere (no open redirect).
- `bannerId` and `zoneId` are integers; anything else is 404.

## POST `{base}/seen` : impression reports

Sent by the package script with `navigator.sendBeacon` (fallback `fetch` with `keepalive`).

- Content type `text/plain;charset=UTF-8` (a "simple" request: no CORS preflight, no cookies).
- Body: up to 50 lines, each `{token} {bannerId}`; max body size 16 KB, larger bodies are rejected
  with 413 without reading further.
- Response: always 204 for accepted requests, whatever the validity of individual lines (the
  endpoint does not reveal which reports counted). 413 for oversized bodies, 405 for other methods.
- A line is counted as one impression of (today UTC, bannerId, token zone) when: the token
  validates and is not expired, `bannerId` is in the token's banner list, the pair (token nonce,
  bannerId) has not been counted before on this server, and the request is not from a bot.
  Everything else is ignored silently.

## GET `{base}/zone/{alias}` : client mode delivery

Enabled when `Meresoft:Banners:DeliveryEndpointEnabled` is true (default). When disabled: 404.

Query: none (the zone settings decide how many banners and the image size).

200 response, `application/json`:

```json
{
  "zone": "search-top",
  "token": "CfDJ8...",
  "banners": [
    {
      "id": 12,
      "type": "image",
      "clickUrl": "/_banners/c/12?z=3",
      "newWindow": true,
      "imageUrl": "/media/abc/banner.png?width=728&height=90",
      "width": 728,
      "height": 90,
      "alt": "Invented summer promo 2027",
      "title": null
    },
    {
      "id": 15,
      "type": "html",
      "html": "<div>...</div>"
    }
  ]
}
```

- Only public fields are returned: never the banner name, weights, schedule, limits, statistics,
  draft content or user data.
- Unknown alias: 200 with `"banners": []` and no token, and the alias is recorded as a requested
  zone (FR-003). A zone with no active banner: 200 with `"banners": []` and no token.
- Alias is normalised (trimmed, lowercased); an alias that fails the alias rules is 404.

## Package script

- Served as a static web asset of the package: `/meresoft-banners/banners.js`
  (research R10), under 5 KB minified, no third-party code (FR-010).
- Included once per page by the first zone rendered (tag helper or view component), with `defer`.
- Finds every `[data-mbz]` zone element and takes the endpoint base path from its `data-mb-base`
  attribute (contracts/markup.md):
  - Server mode: reads `data-mbz-token` and the `[data-mbb]` banner elements inside.
  - Client mode: calls the delivery endpoint, renders the banners into the placeholder (replacing
    fallback content only if at least one banner came back), then treats them as above.
- Visibility rule (FR-011): an `IntersectionObserver` with threshold 0.5 per banner element; a
  banner qualifies after 1,000 ms of continuous visibility at >= 50% while the page is visible
  (`document.visibilityState === "visible"`); leaving the viewport or hiding the tab resets the
  timer. Each banner element reports at most once per page view.
- Reports are queued and sent at most every 2 seconds and on `pagehide`.
- Without `IntersectionObserver` (very old browsers) nothing is counted.
