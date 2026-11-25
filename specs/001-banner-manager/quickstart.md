# Quickstart: validating the banner manager (001)

A run guide that proves the feature end to end. Commands assume the repository root. Test data is
invented (constitution V). Details of endpoints and markup are in `contracts/`.

## Prerequisites

- .NET 10 SDK, Node.js 22 LTS (back office build and browser tests)
- For SQL Server runs: a local SQL Server or the container used in CI (connection string in an
  environment variable, never in the repository)

## 1. Build and automated tests

```bash
dotnet build Meresoft.Umbraco.Banners.slnx
npm ci --prefix src/Meresoft.Umbraco.Banners/Client && npm run build --prefix src/Meresoft.Umbraco.Banners/Client
dotnet test tests/Meresoft.Umbraco.Banners.Tests.Unit
dotnet test tests/Meresoft.Umbraco.Banners.Tests.Integration                 # SQLite
MB_TEST_SQLSERVER="<connection string>" dotnet test tests/Meresoft.Umbraco.Banners.Tests.Integration --filter Category=SqlServer
npx --prefix tests/e2e playwright test                                        # browser tests against the sample site
```

Expected: all green. A failing test blocks a release (constitution V).

## 2. Sample site

```bash
dotnet run --project samples/Meresoft.Umbraco.Banners.Sample
```

The sample site is an Umbraco 17 site on SQLite with unattended install, a home page, a long page
(zone below the fold) and a cached page (output cache 60 s). It references the package project.

## 3. Manual scenarios

| # | Steps | Expected | Spec |
|---|---|---|---|
| 1 | Log in as the sample administrator, open Banners | section visible, zones list empty, "placed but not set up" lists `home-top` (seen by the template) | US1-3, US6-3, FR-003 |
| 2 | Create zone `home-top` from the requested list, show 1, 728 x 90, server mode | zone created, requested entry gone | FR-001 |
| 3 | Create an image banner (invented image, `https://example.org/show`), assign `home-top`, Save only | home page shows nothing; banner state "Draft" | FR-004a |
| 4 | Publish | within 5 seconds the home page shows the banner; link goes to `/_banners/c/{id}?z={id}` and redirects to example.org | US1-1, US1-4, US3-2 |
| 5 | Change alt text, Save only | site still shows the old alt text; state "Published with unpublished changes" | US1-5 |
| 6 | Add a second banner, weights 1 and 3, publish both, reload the page 40 times | both appear, the second more often | US2-1 |
| 7 | Open the long page, do not scroll, wait 5 s, leave; open again, scroll to the zone, wait 2 s, scroll away and back | report (after 1 minute) shows 1 impression for that page, not 2 | US3-6 |
| 8 | Disable JavaScript, open home | banner shows and the link works; no impression counted | US3-7 |
| 9 | Create zone `cached-top` from the requested list in client mode, assign both banners and publish, open the cached page in two browsers | each gets its own choice; 2 impressions | US4-1 |
| 10 | Open Reports, today, export CSV | totals match the counts from steps 7 to 9; CSV has date, zone, banner, impressions, clicks | US3-1, US3-5 |
| 11 | Call `/_banners/c/99999?url=https://evil.example` | 404, no redirect | US3-4 |
| 12 | Log in as a sample editor without the Banners permission | no Banners section; management API returns 403 | US6-1 |
| 13 | Give a banner an impression limit of 3, view it 5 times, wait 1 minute | stops showing, flag "Limit reached" | US5-1 |
| 14 | Try to delete `home-top` while banners are assigned | refused with the list of banners | FR-007 |

## 4. Performance checks

- `dotnet run -c Release --project tests/Meresoft.Umbraco.Banners.Benchmarks`: zone render p95 under
  2 ms with 20 active banners in the zone, and no database query (SC-003).
- Integration test `CountingLoadTests`: 100 views per second with 4 zones for 60 s across two
  in-process "servers"; flushed totals equal reported totals, at most one write batch per server per
  flush interval (SC-004, SC-005).
