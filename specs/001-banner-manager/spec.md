# Feature Specification: Banner manager for Umbraco (first release)

**Feature Branch**: `001-banner-manager`

**Created**: 2026-11-25

**Status**: Draft

**Input**: User description: "A free, MIT licensed Umbraco package by Meresoft, published on NuGet and the
Umbraco Marketplace, that lets editors manage banners in named zones, rotates them on the site and counts
impressions and clicks per day, with a report in the back office. It replaces the banner module of the
old Kentico site of the first client (a few banners in a few slots of one page,
millions of shows and thousands of clicks), but the package itself stays generic: no client code or data."

## Clarifications

### Session 2026-11-25

- Q: Can one banner show in several zones, or does each banner belong to exactly one zone? → A: One banner can be assigned to several zones; its weight is the same in every zone and its impression and click limits are shared across all its zones.
- Q: On an install that hosts several sites, are zones shared by all sites or does each site have its own? → A: Zones are global to the install; sites that need different banners use different zone aliases.
- Q: Does saving a banner make it live at once, or is there a draft and a separate publish step? → A: Draft and publish, as for Umbraco content: Save stores a draft, Publish makes it live, Unpublish takes it off the site; the live version does not change until Publish.
- Q: When does an impression count: when the banner is sent with the page, or only when it is actually seen on screen? → A: Only when seen: at least 50% of the banner is visible in the browser for at least one continuous second (IAB display viewability rule), reported by the package script.
- Q: Which back office user groups get the Banners section right after install? → A: Only the Administrators group, with both the Banners and the HTML banner permissions; other groups are granted access by an administrator in the standard user group settings.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Show a banner in a zone (Priority: P1)

A developer installs the package and places a banner zone (for example "Search page, top") in a
template with one line of markup. An editor opens the new Banners section in the back office, sees
the zone, adds a banner to it (image, link, alternative text, open in a new window or not) and
publishes it. Visitors now see that banner where the zone sits.

**Why this priority**: showing a managed banner in a chosen place is the core value; everything
else builds on it.

**Independent Test**: install the package on an empty Umbraco site, place a zone, add one banner,
open the page: the banner image shows and links to the banner URL.

**Acceptance Scenarios**:

1. **Given** a zone placed in a template and one active banner in it, **When** a visitor opens the
   page, **Then** the banner shows with its image, alternative text and link.
2. **Given** a zone with no active banner, **When** a visitor opens the page, **Then** nothing is
   rendered for the zone (no empty box, no error), unless the zone has fallback content set by the
   developer in the markup, which then shows instead.
3. **Given** a template that names a zone that does not exist yet, **When** the page is opened,
   **Then** nothing is rendered and the zone is listed in the back office as "placed but not set
   up", so the editor can create it in one step.
4. **Given** an editor publishes a change to a banner, **When** the next visitor opens the page,
   **Then** the change shows within a few seconds, without a site restart.
5. **Given** a published banner and an editor who changes it and only saves (does not publish),
   **When** a visitor opens the page, **Then** the previously published version still shows, and
   the back office marks the banner "unpublished changes".

---

### User Story 2 - Rotation and scheduling (Priority: P1)

An editor puts several banners in one zone, gives each a weight and optionally a start and end
date and time. The site shows them in turn according to their weights, and only inside their
dates. A zone can be set to show one banner or several at once (for example a side column of
three).

**Why this priority**: the old site rotates banners per slot; without rotation and dates the
package cannot replace it.

**Independent Test**: one zone with banners of weight 1 and 3: over 4,000 page views the second
shows about three times as often; a banner with an end date in the past never shows.

**Acceptance Scenarios**:

1. **Given** two active banners with weights 1 and 3 in a zone that shows one banner, **When** the
   page is viewed many times, **Then** each banner's share of views is within 5 percentage points of
   its weight share (25% and 75%) over 4,000 views.
2. **Given** a banner whose start date is in the future or whose end date has passed, **When** the
   page is viewed, **Then** that banner is never shown.
3. **Given** a zone set to show 3 banners and 5 active banners, **When** the page is viewed,
   **Then** 3 different banners show, chosen by weight, never the same banner twice in one view.
4. **Given** a banner unpublished by the editor, **When** the page is viewed, **Then** it is not
   shown, and it keeps its statistics.

---

### User Story 3 - Impression and click statistics (Priority: P1)

The site counts how often each banner was shown and clicked, per day. In the back office the editor
sees, for a chosen period, per zone and per banner: impressions, clicks and click-through rate, as
a table and a simple chart, and can export the table as CSV.

**Why this priority**: the old site keeps these numbers; banners are a commercial asset and their
owners ask "how did it do".

**Independent Test**: bring a banner into view 100 times and click it 3 times; the report for today shows 100
impressions, 3 clicks and 3.0%.

**Acceptance Scenarios**:

1. **Given** a banner seen 100 times and clicked 3 times today, **When** the editor opens the
   report for today, **Then** it shows 100 impressions, 3 clicks and a click-through rate of 3.0%
   (counts may appear with a delay of up to one minute).
2. **Given** a visitor clicks a banner, **When** the click goes through the package's click
   address, **Then** the click is counted and the visitor lands on the banner's link.
3. **Given** a request whose user agent is a known bot or crawler, **When** it shows or clicks a
   banner, **Then** nothing is counted.
4. **Given** the click address is called with a banner id that does not exist or with any
   attempt to pass another target address, **When** it is requested, **Then** the visitor is not
   redirected anywhere outside the stored link of an existing banner (unknown banner: 404).
5. **Given** a report period and zone filter, **When** the editor exports, **Then** a CSV file with
   one row per banner per day (date, zone, banner, impressions, clicks) downloads.
6. **Given** a banner placed below the fold, **When** a visitor opens the page and leaves without
   scrolling to it, **Then** no impression is counted; **When** the visitor scrolls so that at least
   half of it is visible for one second, **Then** exactly one impression is counted for that page
   view, however often it scrolls in and out afterwards.
7. **Given** a visitor with scripts off or a blocker that stops the package script, **When** the
   page opens, **Then** server mode banners still show and links still work, but no impression is
   counted (clicks through the click address still count).

---

### User Story 4 - Pages served from a cache or CDN (Priority: P2)

A site that caches whole pages (output cache or CDN) can still rotate banners and count them,
because a zone can be rendered in the browser: the page holds an empty zone placeholder and a small
script asks the site for the banners of that zone.

**Why this priority**: many Umbraco sites cache pages; without this option every visitor of a cached
page would see the same banner and impressions would not be counted.

**Independent Test**: set the zone to client mode, cache the page, load it twice: different banners
can appear and both impressions are counted.

**Acceptance Scenarios**:

1. **Given** a zone in client mode on a cached page, **When** two visitors open the page, **Then**
   each gets a banner chosen for that view and each impression is counted.
2. **Given** a visitor with scripts off, **When** the page opens, **Then** the zone shows its
   fallback content if the developer set one, otherwise nothing, and no error.

---

### User Story 5 - Limits per banner (Priority: P3)

An editor can give a banner a maximum number of impressions and/or clicks (for a paid campaign of
"10,000 shows"). When a limit is reached the banner stops showing by itself, and the back office
shows it as "limit reached".

**Why this priority**: useful for sold campaigns; the old Kentico module had it, but the first
client does not depend on it.

**Independent Test**: a banner with a limit of 50 impressions stops showing after about 50 views.

**Acceptance Scenarios**:

1. **Given** a banner with a limit of 50 impressions, **When** it has been seen 50 times in total
   across all its zones, **Then**
   it stops showing within one minute and is marked "limit reached"; small overshoot caused by
   batching is allowed and stated in the documentation (at most the views of one minute).
2. **Given** an editor raises the limit, **When** the banner is published, **Then** it shows again.

---

### User Story 6 - Permissions and audit (Priority: P2)

Only back office users with the package's permission see the Banners section. A separate permission
allows editing HTML banners (see FR-006). Changes are visible in the back office history: who
created, changed, published, unpublished or deleted a banner or zone, and when.

**Why this priority**: banners link off-site and can carry ad network code; agencies need control.

**Independent Test**: a user without the permission does not see the section and gets 403 from the
management API.

**Acceptance Scenarios**:

1. **Given** a back office user without the Banners permission, **When** they open the back office
   or call the management API, **Then** the section is hidden and the API answers 403.
2. **Given** a user with the Banners permission but without the HTML permission, **When** they edit
   an HTML banner, **Then** they can publish or unpublish it and see its statistics but cannot change
   its HTML.
3. **Given** a fresh install, **When** an administrator opens the back office, **Then** the Banners
   section is visible to the Administrators group (with HTML banner editing) and to no other group.

### Edge Cases

- The image media item of a banner is deleted: the banner is not shown and the back office marks it
  "image missing".
- Two zones with the same name on one page: each renders independently; the same banner may appear
  in both unless the developer marks them as one group (out of scope for the first release; stated in
  the docs).
- A zone set to show 3 banners has only 1 active banner: it shows 1.
- A banner assigned to two zones on the same page may show in both zones in one page view; each
  one that is seen counts as one impression in its zone.
- Impression reports are sent from the browser, so they can be faked: a report is accepted only
  with a short-lived, single-use view token issued when the zone was rendered for that page view
  (tokens are held in memory, carry no visitor data and are not stored); invalid, expired or
  repeated reports are ignored. A server mode zone on a page served from a cache would replay an
  old token, so impressions on cached pages are counted only in client mode (stated in the docs).
- A banner with no zone assignment is never shown; the back office marks it "not placed".
- Clock and time zones: start and end times are entered and shown in the back office user's time
  zone and stored in UTC; statistics days are UTC days, stated in the report.
- The site runs on several servers: each server counts in memory and adds its counts to the shared
  daily rows; no counts are lost or doubled by having more servers.
- The package is uninstalled: its tables stay until the developer runs a documented removal step;
  the site keeps working with zones rendering nothing.
- Very high traffic (millions of impressions a month, as on the first client site): counting adds no
  database write to the page request and at most one batched write per server per minute.
- A banner link is not a valid http or https address: saving is refused with a clear message.
- An image is very large: the zone renders it through the Umbraco image pipeline with the size the
  zone defines, so the page does not load the original file.

## Requirements *(mandatory)*

### Functional Requirements

**Zones**

- **FR-001**: Editors MUST be able to create, rename, describe and delete zones. Zones are global
  to the Umbraco install (shared by all sites in it). A zone has a unique alias used in templates, a display name, the number of banners it shows at once (1 to 10),
  an optional image size (width and/or height) and a rendering mode (server or client).
- **FR-002**: Developers MUST be able to place a zone with one line of markup (a tag helper and an
  equivalent view component or partial), optionally with fallback content and extra CSS classes.
- **FR-003**: The back office MUST list zones that templates request but that do not exist yet (seen
  at runtime) and let the editor create them in one step.

**Banners**

- **FR-004**: Editors MUST be able to create, edit, copy, publish, unpublish and delete banners, and
  assign each banner to one or more zones. The weight is the same in every assigned zone, and
  the impression and click limits count the banner's totals across all its zones. A banner has: name (back office
  only), image (an Umbraco media item), link (http or https),
  open in a new window (yes or no), alternative text, optional title, weight (1 to 100, default 1),
  optional start and end date and time, optional impression limit and click limit.
- **FR-004a**: Banners MUST follow a draft and publish workflow like Umbraco content. Save stores
  a draft of all banner fields (content, link, weight, schedule, limits, zone assignments); Publish
  makes the draft the live version; Unpublish removes the banner from the site at once and keeps
  the draft. Visitors only ever see the live version. The back office shows each banner's state:
  draft (never published), published, published with unpublished changes, or unpublished.
- **FR-005**: Image banners MUST render as an accessible link with the image, the alternative text,
  `rel="sponsored noopener"` on the link, and the image size set by the zone.
- **FR-006**: The package MUST support an HTML banner type (for ad network code or custom markup),
  editable only by users with the separate HTML permission. HTML banners are rendered as stored;
  their clicks are not counted (the package cannot wrap foreign code); their impressions are
  counted by the visibility of the box the package wraps around them.
- **FR-007**: Deleting a banner MUST keep its past statistics, shown in reports as "(deleted
  banner)"; deleting a zone requires that no banner is assigned to it any more (removing a banner
  from a zone keeps the statistics it collected there).

**Selection and delivery**

- **FR-008**: For each view of a zone the package MUST choose from the active banners (published,
  inside their dates, under their limits, image present) by weighted random choice, without
  repeating a banner within one zone view.
- **FR-009**: Banner data used for selection MUST come from an in-memory cache refreshed on publish and unpublish (on
  all servers), not from the database per request.
- **FR-010**: Every zone MUST include the package script (no third-party libraries, under 5 KB),
  which measures visibility and reports impressions in both modes. In client mode the zone MUST
  also render a placeholder, and the same script requests the zone's banners from a public delivery endpoint and
  renders them; the endpoint returns only public banner fields.

**Counting and reports**

- **FR-011**: A banner MUST count one impression per page view when at least 50% of it has been
  visible in the browser for at least one continuous second (IAB display viewability rule); the
  script reports it to a public impression endpoint that accepts only valid view tokens (see Edge
  Cases). Banners that are rendered but never seen count nothing; each click through the click address
  MUST count one click and redirect (302) to the banner's stored link only.
- **FR-012**: Counts MUST be kept in memory and written to the database in batches at least once a
  minute and on clean shutdown, as daily totals per banner and zone; page requests MUST NOT write to
  the database.
- **FR-013**: Requests from known bots and crawlers MUST NOT be counted. The bot list ships with the
  package and can be extended in configuration.
- **FR-014**: The package MUST NOT store or log IP addresses, user agents, cookies, member or visitor
  identifiers, and MUST NOT set cookies.
- **FR-015**: The back office MUST show a report for a chosen period (presets: today, last 7 days,
  last 30 days, this month, last month, custom), filterable by zone and banner, with impressions,
  clicks, click-through rate, a daily chart and CSV export.
- **FR-016**: The package MUST offer an import surface for statistics and banners (a documented C#
  service), so a site can bring in history from another system; the package itself ships no
  importer for any specific CMS.

**Back office, permissions, lifecycle**

- **FR-017**: The package MUST add a Banners section to the back office, visible only to user groups
  with the package permission, built with the official back office extension model. On first
  install the package MUST grant the Banners permission and the HTML banner permission to the
  Administrators group only, and never change group permissions on later upgrades; other groups
  get access through the standard user group settings (described in the README).
- **FR-018**: All changes MUST be recorded in the back office audit with user, time and action.
- **FR-019**: Installing MUST create the package tables through package migrations; upgrading MUST
  apply new migrations; nothing in Umbraco core data changes.
- **FR-020**: The package MUST work on SQL Server and SQLite, on Umbraco 17 with .NET 10.
- **FR-021**: All texts in the back office MUST be translatable (English shipped; other languages can
  be added through the standard localization files).
- **FR-022**: Configuration (batch interval, bot list additions, delivery endpoint on or off, cache
  refresh) MUST be available through the standard settings file with documented defaults.

**Distribution**

- **FR-023**: The package MUST be published as a NuGet package named `Meresoft.Umbraco.Banners`,
  MIT licensed, with Umbraco Marketplace metadata, an icon, a README and release notes.

### Key Entities

- **Zone**: a named place on the site where banners show; alias, name, description, how many banners
  it shows, image size, rendering mode.
- **Banner**: an item shown in one or more zones; type (image or HTML), content, link, weight, schedule,
  limits, publish state, audit information. It has a live (published) version and a draft version.
- **Zone assignment**: links a banner to a zone; a banner has at least one assignment to be shown.
- **Daily statistic**: the number of impressions (seen banners) and clicks of one banner in one zone on one UTC day;
  kept after the banner is deleted.
- **Requested zone**: a zone alias seen in templates at runtime that is not set up yet.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer goes from install to a first banner on a page in under 15 minutes, using
  only the README.
- **SC-002**: An editor creates a zone, adds two banners with weights and dates and publishes them
  in under 5 minutes without help.
- **SC-003**: Rendering a page with banner zones adds less than 2 ms per zone at the 95th
  percentile on the server, and adds no database query to the request.
- **SC-004**: At 100 page views per second with 4 zones each, impressions are counted with less
  than 0.1% loss in normal operation, with at most one batched database write per server per minute.
- **SC-005**: Report figures match the true number of non-bot views and clicks in a controlled test
  (exact after the batch is written).
- **SC-006**: The package installs and passes its tests on SQL Server and SQLite on Umbraco 17.
- **SC-007**: The first client site replaces its old banner module with the package,
  showing its 4 banners in its 4 slots, with the old statistics imported through the import surface
  (work done in the site repository, not in the package).
- **SC-008**: The package is listed on the Umbraco Marketplace within one week of the first release.

## Assumptions

- Target: Umbraco 17 (LTS) and .NET 10 only for the first release; Umbraco 18 support follows in a
  later release.
- Editors are trusted back office users; HTML banners are a deliberate, separately permitted feature.
- Banners are images from the Umbraco media library; video and animated formats are out of scope
  except as HTML banners.
- On an install with several sites, the same zone alias shows the same banners on every site;
  separate banners per site are achieved with separate zone aliases (documented in the README).
- Targeting by site, page, culture, device, member group or visitor segment, A/B testing, advertiser
  logins, invoicing and selling of zones are out of scope for the first release (candidates for a
  later release).
- Statistics are kept indefinitely (daily rows are small); a retention setting may come later.
- Impressions mean viewable impressions. Visitors without scripts are not counted, so numbers are
  lower than "times sent with a page"; history imported from systems that counted sends (such as
  the old Kentico module) is not directly comparable, and the report marks imported days.
- A small overshoot of limits caused by batching (up to one minute of views) is acceptable.
- The click address lives under a fixed path of the package that the developer can change in
  configuration.
- Multi-server sites use Umbraco's standard cache refresh between servers.
- The first client's migration (banners, images, statistics from Kentico) is done in the first client's
  site as its spec 009 and is not part of this package.
