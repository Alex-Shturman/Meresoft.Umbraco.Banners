# Manual test cases: banner manager (001)

Manual and end-to-end test cases for the first release of `Meresoft.Umbraco.Banners`, to be run by QA against the
local sample site. Sources: `quickstart.md` (14 scenarios and the performance checks), `spec.md` (US1 to US6),
`contracts/public-endpoints.md`, `contracts/markup.md`, `contracts/management-api.md`, the root `README.md` and the
sample site in `samples/Meresoft.Umbraco.Banners.Sample/`. All test data in this document is invented.

## 1. Environment

| Item | Value |
|---|---|
| Sample site | `http://localhost:5100`, started from the main checkout (see below) |
| Back office | `http://localhost:5100/umbraco` |
| Banners section | `http://localhost:5100/umbraco/section/meresoft-banners/banners`, `.../zones`, `.../reports` |
| Administrator | `admin@sample.invalid` / `SampleAdmin-2027-xK` (unattended administrator, group Administrators) |
| Plain editor, no Banners section | `editor@sample.invalid` / `SampleEditor-2027-xK` (built-in group Editors) |
| Banner editor, no HTML permission | `bannereditor@sample.invalid` / `SampleBannerEditor-2027-xK` (group "Sample banner editors", created by the sample) |
| Public path | `/_banners` (the sample `appsettings.json` does not set `Meresoft:Banners:PublicPath`, so the default applies) |
| Flush interval | **5 seconds** (`Meresoft:Banners:FlushInterval` = `00:00:05` in `samples/Meresoft.Umbraco.Banners.Sample/appsettings.json`). The flush job starts 30 seconds after the site starts. It writes counts and "placed but not set up" zones. |
| View token lifetime | 10 minutes (default; not set in the sample) |
| Delivery endpoint | on (default; not set in the sample) |
| Package script | `http://localhost:5100/meresoft-banners/banners.js` |

Start command (only needed for the optional restart cases TC-COUNT-07 and TC-SEC-11), from the repository root of the
main checkout:

```bash
export Umbraco__CMS__Unattended__UnattendedUserName="Sample Admin"
export Umbraco__CMS__Unattended__UnattendedUserEmail="admin@sample.invalid"
export Umbraco__CMS__Unattended__UnattendedUserPassword="SampleAdmin-2027-xK"
export MB_SAMPLE_EDITOR_EMAIL="editor@sample.invalid"
export MB_SAMPLE_EDITOR_PASSWORD="SampleEditor-2027-xK"
export MB_SAMPLE_BANNER_EDITOR_EMAIL="bannereditor@sample.invalid"
export MB_SAMPLE_BANNER_EDITOR_PASSWORD="SampleBannerEditor-2027-xK"
dotnet run --project samples/Meresoft.Umbraco.Banners.Sample --urls http://localhost:5100
```

Never run a second instance of the site on the same database (README, "One site instance per database"): stop the
first one (Ctrl+C in its terminal) before starting another.

### Pages and zones of the sample site

| Page | URL | Zones in the template (in page order) | Notes |
|---|---|---|---|
| Home ("Sample Gazette") | `http://localhost:5100/` | `home-top` | links to the long and cached pages |
| Long page ("A long page") | `http://localhost:5100/long` | `long-html` (top), `long-bottom` (below 40 paragraphs, below the fold) | |
| Cached page ("A cached page") | `http://localhost:5100/cached` | `cached-top`, `cached-html` | output cache 60 s; shows "Rendered at HH:mm:ss UTC" |

None of the sample templates has fallback content inside `<banner-zone>`, so fallback content cannot be checked on
the sample site without editing a view. That part of US1-2 and US4-2 is covered by the automated tests only.

The sample seeds one image in the Media root: **"Invented sample banner image"** (a 1 x 1 PNG at
`/media/sample/banner.png`; the zone size scales it). Every image banner in this document uses it.

### Rules for QA

- **QA must not run SQL, touch the database files (`umbraco/Data/*.sqlite.db` or anything under `umbraco/`), or use
  docker.** Use only the browser UI (back office and public pages), plain HTTP requests (curl) and the browser devtools
  network tab.
- Do not edit any file of the repository. Settings for the optional restart cases are given as environment variables.
- `Security:AllowConcurrentLogins` is `false`: one user signed in twice logs out the first session. Use a separate
  browser profile (or a private window) per user.
- curl's default user agent (`curl/...`) is on the shipped bot list, so requests made with it are **never counted**.
  Every curl request that is meant to count must send the browser user agent `$UA` below. Requests that only check
  status codes or headers can use the default.
- Counts reach the report after a flush. After every counting action, wait **at least 10 seconds**, then press
  **Reload** in Reports (or choose the period again) before reading numbers.
- Statistics are never deleted, also not when banners are deleted. On a database used before, compare **deltas**
  (record the numbers before the step) and filter the report by banner. Banners created in this run get new ids, so
  their numbers start at 0.
- While a counting case runs, keep no other tab open on the public page it uses, or that tab adds impressions.
- To call the management API with a user's sign-in: in that user's back office tab open devtools, Network, filter
  `management/api`, right-click any request and choose **Copy > Copy as cURL**. That command carries whatever
  credentials the back office uses. Change its URL, method or body as each step says.

### Shell variables used in the steps

```bash
BASE=http://localhost:5100
P=/_banners
UA='Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36'
W=$(mktemp -d)   # work folder for token files
```

Helper to post a file of seen lines (`{token} {bannerId}` per line) in parts of 25 lines:

```bash
post_seen() { split -l 25 "$1" "$1.part-"; for f in "$1".part-*; do curl -s -o /dev/null -w '%{http_code} %{time_total}\n' -A "$UA" -X POST -H 'Content-Type: text/plain;charset=UTF-8' --data-binary @"$f" "$BASE$P/seen"; done; rm -f "$1".part-*; }
```

### Data sheet (fill in while running)

Numeric ids are read from the page source or from JSON: a banner element is `data-mbb="{bannerId}"`, a click link is
`/_banners/c/{bannerId}?z={zoneId}`.

| Name in this document | Back office name | Type | Zones | Id |
|---|---|---|---|---|
| B1 | QA Spring Leaflets | Image | home-top, later cached-top | |
| B2 | QA Autumn Calendars | Image | home-top, later cached-top | |
| B3 | QA Below Fold | Image | long-bottom | |
| B4 | QA Limit Three | Image | home-top | |
| B5 | QA Ratio Check | Image | cached-html | |
| H1 | QA Html Notice | HTML | long-html, cached-html | |
| L1 | QA Link Rules | Image | none (stays a draft) | |
| CA, CB, CC | QA Copy A, QA Copy B, QA Copy C | Image (copies of B1) | home-top | |

| Zone alias | Name | Shown at once | Image size | Render mode | Zone id |
|---|---|---|---|---|---|
| home-top | Home page, top | 1 | 728 x 90 | Server | |
| long-bottom | Long page, bottom | 1 | 300 x 250 | Server | |
| long-html | Long page, html | 1 | none | Server | |
| cached-top | Cached page, top | 1 | 728 x 90 | Client | |
| cached-html | Cached page, html | 1 | 300 x 250 | Client | |
| qa-spare | QA spare zone | 1 | none | Server | |

---

## 2. Test cases

Format: **Covers** names the requirement (FR, SC), the acceptance scenario (US*n*-*m*) or the quickstart scenario
(QS*n*). Expected results are numbered like the steps they belong to.

### SHOW: zones, banners and the draft and publish workflow

#### TC-SHOW-01 Banners section, empty start and "Placed but not set up"

- **Covers**: QS1, US1-3, US6-3, FR-003, FR-017
- **Preconditions**: the site has run for at least 30 seconds. No zones and no banners exist. If some exist from an
  earlier run, sign in as administrator and delete every banner (Banners, open each, **Delete**), then every zone
  (Zones, open each, **Delete**). Statistics of earlier runs stay; that is expected.
- **Steps**:
  1. In a private window, without signing in, open `http://localhost:5100/`, `http://localhost:5100/long` and
     `http://localhost:5100/cached` once each. View the source of the home page (Ctrl+U / Cmd+Option+U).
  2. Wait 10 seconds. Sign in at `http://localhost:5100/umbraco` as `admin@sample.invalid`.
  3. Open the **Banners** section, then **Zones**.
  4. Open **Banners** in the section menu.
- **Expected**:
  1. The pages show their headings and text, and nothing for the zones. The home page source contains no element with
     `class="mb-zone"`, no `data-mbz` attribute and no `<script src="/meresoft-banners/banners.js">`.
  2. Sign-in succeeds.
  3. The section menu shows **Banners**, **Zones** and **Reports**. The zones table is empty with the text "No zones
     yet. Create a zone, or set up one that a template already asks for." The box **Placed but not set up** ("Templates
     ask for these zones, but they do not exist yet.") lists `home-top`, `long-html`, `long-bottom`, `cached-top` and
     `cached-html`, each with a **Last asked for** time, a create button and a **Dismiss** button.
  4. The list shows "No banners match. Create a banner, or change the filters."

#### TC-SHOW-02 Create a zone from the requested list, and dismiss a requested zone

- **Covers**: QS2, FR-001, FR-003
- **Preconditions**: TC-SHOW-01 done; signed in as administrator; Banners > Zones open.
- **Steps**:
  1. In the **Placed but not set up** box press the create button of `home-top` (accessible name "Create zone
     home-top").
  2. Check that **Alias** holds `home-top`. Enter **Name** `Home page, top`, **Description** `Invented top slot of the
     sample gazette`, **Banners shown at once** `1`, **Image width** `728`, **Image height** `90`, **Render mode**
     **Server**. Press **Save**.
  3. Go back to Zones.
  4. Press **Dismiss** for `long-html`.
  5. In the private window open `http://localhost:5100/long` again. Wait 10 seconds. In Zones press **Reload**.
- **Expected**:
  2. Notification "Zone saved".
  3. The zones table has one row: alias `home-top`, name "Home page, top", **Banners shown at once** 1,
     **Active banners** 0. `home-top` is no longer in **Placed but not set up**.
  4. `long-html` disappears from the box.
  5. `long-html` is listed again (a template asked for it again).

#### TC-SHOW-03 Zone validation and alias rules in the back office

- **Covers**: FR-001, data-model rules, "invalid aliases"
- **Preconditions**: TC-SHOW-02 done; Banners > Zones open.
- **Steps**:
  1. Press **Create zone** (the button at the top of the list). Enter **Name** `QA invalid zone`, **Banners shown at
     once** `1`, **Render mode** Server, then try each **Alias** below and press **Save** after each:
     `9-top`, `home top`, `-top`, `top.slot`, `café-top`, and 101 times the letter `a`.
  2. Enter **Alias** `Home-Top` and press **Save**.
  3. Enter **Alias** `qa-invalid`, then try **Banners shown at once** `0` and `11`, **Image width** `5000`,
     **Image height** `0`, and an empty **Name**, pressing **Save** after each.
  4. Leave without saving and go back to Zones.
- **Expected**:
  1. Each save is refused with the message on the Alias field: "The alias must be 1 to 100 characters: lowercase
     letters, digits, '-' and '_', starting with a letter." No zone is created.
  2. Refused: the alias is normalised to `home-top`, which is taken. No second zone appears.
  3. Refused with, in turn: "The number of banners shown must be 1 to 10.", "The image width must be 1 to 4000.",
     "The image height must be 1 to 4000.", "The name is required and can be at most 255 characters."
  4. The zones table still holds only `home-top`.

#### TC-SHOW-04 A zone without active banners writes nothing

- **Covers**: US1-2 (without fallback), US1-3, contracts/markup.md "No active banner and no fallback"
- **Preconditions**: TC-SHOW-02 done; `home-top` exists with no banner.
- **Steps**:
  1. In the private window open `http://localhost:5100/` and view the source.
  2. Run `curl -s $BASE/ | grep -c 'mb-zone\|data-mbz\|banners.js'`.
  3. Open `http://localhost:5100/long` (its zones are not set up) and view the source.
- **Expected**:
  1. The page renders normally; no empty box, no error. The source holds no `mb-zone` element and no script tag of the
     package.
  2. Prints `0`.
  3. No zone markup and no package script for `long-html` and `long-bottom`.

#### TC-SHOW-05 Image banner saved as a draft is not shown

- **Covers**: QS3, FR-004, FR-004a
- **Preconditions**: TC-SHOW-02 done.
- **Steps**:
  1. Banners > **Create banner**. **Type** Image. **Name** `QA Spring Leaflets`. **Image**: press **Choose**, pick
     "Invented sample banner image", press **Choose** in the picker. **Link** `https://example.org/show`. Leave **Open
     in a new window** unticked. **Alternative text** `Invented spring leaflet offer`. **Weight** `1`. Leave **Show
     from**, **Show until**, **Impression limit** and **Click limit** empty. Under **Zones** tick "Home page, top".
  2. Press **Save** (not Publish).
  3. Open the Banners list.
  4. Open `http://localhost:5100/` in the private window and view the source.
- **Expected**:
  2. Notification "Banner saved". The editor shows state **Draft**.
  3. The row "QA Spring Leaflets" shows **State** "Draft", **Type** Image, zone "Home page, top".
  4. The page shows no banner; no `mb-zone` element in the source. Zones list: `home-top` **Active banners** 0.

#### TC-SHOW-06 Banner field validation, and a banner without a zone

- **Covers**: edge case "link not valid", FR-004, edge case "banner with no zone assignment"
- **Preconditions**: TC-SHOW-05 done.
- **Steps**:
  1. **Create banner**, Type Image, **Name** `QA Link Rules`, pick the sample image, **Alternative text** `Invented
     link rules check`, no zone. Try each **Link** below and press **Save** after each: `example.org/show`,
     `ftp://example.org/file`, `javascript:alert(1)`, `https://example.org/qa path` (with a space).
  2. Set **Link** `https://example.org/rules`. Try **Weight** `0`, then `101`. Then set Weight `1`.
  3. Set **Show from** to tomorrow 10:00 and **Show until** to tomorrow 09:00. Press Save. Then clear both.
  4. Clear **Alternative text** and press Save. Then enter `Invented link rules check` again.
  5. Press **Save**.
  6. Press **Publish**.
  7. Open the Banners list.
- **Expected**:
  1. Each save is refused; the Link field shows "The link must be an absolute http or https address of at most 2048
     characters." Nothing is stored (the banner does not appear in the list until step 5).
  2. Refused with "The weight must be 1 to 100."
  3. Refused with "The end must be after the start."
  4. Refused with "An image banner needs alternative text."
  5. "Banner saved", state **Draft**.
  6. Refused with an error (a banner without a zone cannot be published). The state stays **Draft**.
  7. "QA Link Rules" shows state "Draft" and the flag **Not placed**. It is never shown on the site.

#### TC-SHOW-07 Publish: the banner shows within 5 seconds and the link goes through the click address

- **Covers**: QS4, US1-1, US1-4, US3-2, FR-005, FR-009, FR-011
- **Preconditions**: TC-SHOW-05 done (B1 is a draft).
- **Steps**:
  1. Open B1 "QA Spring Leaflets" and press **Publish**. Start a stopwatch.
  2. Reload `http://localhost:5100/` in the private window until the banner shows. Stop the stopwatch.
  3. View the source. Write B1's id and the zone id of `home-top` in the data sheet.
  4. Run `curl -s -D - -o /dev/null -A "$UA" "$BASE$P/c/<B1>?z=<home-top>"`.
  5. In the private window click the banner.
- **Expected**:
  1. "Banner published"; state **Published**. Zones list: `home-top` **Active banners** 1.
  2. The banner image shows within 5 seconds of the publish, without a restart.
  3. The source has
     `<div class="mb-zone" data-mbz="home-top" data-mb-base="/_banners" data-mbz-token="...">` with
     `<div class="mb-banner" data-mbb="<B1>">`, a link `href="/_banners/c/<B1>?z=<home-top>"`, an `<img>` with
     `src="/media/sample/banner.png?width=728&amp;height=90"` (an image pipeline address), `alt="Invented spring
     leaflet offer"`, `width="728"`, `height="90"`. After the zone, exactly one
     `<script src="/meresoft-banners/banners.js" defer></script>`.
  4. `HTTP/1.1 302` with `Location: https://example.org/show`.
  5. The browser goes to `/_banners/c/<B1>?z=<home-top>` and lands on `https://example.org/show`.

#### TC-SHOW-08 Save only keeps the live version; the next publish replaces it

- **Covers**: QS5, US1-5, FR-004a
- **Preconditions**: TC-SHOW-07 done.
- **Steps**:
  1. Open B1, change **Alternative text** to `Invented spring leaflet offer, version two`, press **Save** only.
  2. Reload the home page several times over 15 seconds and view the source.
  3. Open the Banners list.
  4. Open B1 and press **Publish**. Reload the home page within 5 seconds.
- **Expected**:
  1. "Banner saved"; the editor shows **Published with unpublished changes**.
  2. The site still shows `alt="Invented spring leaflet offer"` (old text).
  3. The B1 row shows "Published with unpublished changes".
  4. State **Published**; within 5 seconds the source shows `alt="Invented spring leaflet offer, version two"`.

#### TC-SHOW-09 Copy makes a draft copy with its assignments and no statistics

- **Covers**: FR-004 (copy), FR-007
- **Preconditions**: TC-SHOW-08 done. This case creates the copies that TC-ROT-02 uses; run it right before.
- **Steps**:
  1. Open B1 and press **Copy**.
  2. Open the new banner. Change its **Name** to `QA Copy A` and press **Save**.
  3. Repeat steps 1 and 2 twice for `QA Copy B` and `QA Copy C`.
  4. Open Reports, **Today**, **Banner** filter `QA Copy A`.
  5. Reload the home page 10 times.
- **Expected**:
  1. "Banner copied". A new banner named "QA Spring Leaflets (copy)" appears in state **Draft**, with the same image,
     link, alt text, weight 1 and zone "Home page, top" as B1.
  2. "Banner saved".
  3. Three draft copies exist. B1 is unchanged and still Published.
  4. "No impressions or clicks were counted in this period." (a copy has no statistics).
  5. Only B1 is ever shown (drafts are not live).

#### TC-SHOW-10 A zone with assigned banners cannot be deleted

- **Covers**: QS14, FR-007
- **Preconditions**: Phase 7 (B1, B2 and B4 are assigned to `home-top` as drafts, live or unpublished).
- **Steps**:
  1. Zones, open `home-top`, press **Delete**, confirm.
  2. Zones list.
  3. **Create zone**: Alias `qa-spare`, Name `QA spare zone`, Banners shown at once 1, Server. **Save**.
  4. Open `qa-spare`, press **Delete**.
  5. Confirm.
- **Expected**:
  1. Refused with a dialog "Banners are still assigned to this zone", "Remove the zone from these banners, then delete
     it.", listing QA Spring Leaflets, QA Autumn Calendars and QA Limit Three.
  2. `home-top` still exists; the home page is unchanged.
  3. "Zone saved".
  4. Confirmation "Delete QA spare zone?" with "This cannot be undone. Statistics already collected are kept."
  5. "Zone deleted"; `qa-spare` is gone from the list.

### ROT: rotation and scheduling

#### TC-ROT-01 Weighted rotation 1 to 3

- **Covers**: QS6, US2-1, FR-008
- **Preconditions**: TC-SHOW-08 done; B1 is the only live banner in `home-top` (the copies are drafts).
- **Steps**:
  1. **Create banner**: Type Image, **Name** `QA Autumn Calendars`, sample image, **Link** `https://example.org/autumn`,
     **Alternative text** `Invented autumn calendar sale`, **Weight** `3`, zone "Home page, top". **Publish**. Note
     B2's id.
  2. In the private window reload the home page 40 times; note which banner shows (alt text) each time.
  3. Run:
     `for i in $(seq 1 4000); do curl -s -A "$UA" $BASE/ | grep -o 'data-mbb="[0-9]*"'; done | sort | uniq -c`
- **Expected**:
  1. "Banner published"; zones list `home-top` **Active banners** 2.
  2. Both banners appear; "Invented autumn calendar sale" appears clearly more often (about 30 of 40).
  3. Two lines. B1 between 800 and 1200, B2 between 2800 and 3200 (25% and 75%, within 5 percentage points).

#### TC-ROT-02 A zone that shows 3 banners never repeats one in a view

- **Covers**: US2-3, FR-008
- **Preconditions**: TC-SHOW-09 and TC-ROT-01 done.
- **Steps**:
  1. Open QA Copy A, QA Copy B and QA Copy C in turn and press **Publish** on each.
  2. Zones, open `home-top`, set **Banners shown at once** `3`, **Save**.
  3. Run:
     `for i in $(seq 1 200); do H=$(curl -s -A "$UA" $BASE/); echo "$(printf '%s' "$H" | grep -o 'data-mbb="[0-9]*"' | wc -l) $(printf '%s' "$H" | grep -o 'data-mbb="[0-9]*"' | sort -u | wc -l)"; done | sort | uniq -c`
  4. Run the 200 requests again, printing the ids, and count them:
     `for i in $(seq 1 200); do curl -s -A "$UA" $BASE/ | grep -o 'data-mbb="[0-9]*"'; done | sort | uniq -c`
  5. In the private window open the home page 5 times and keep it on screen 3 seconds each time.
- **Expected**:
  1. All three show **Published**. Zones list: `home-top` **Active banners** 5.
  2. "Zone saved".
  3. One line: `200 3 3` (every view has 3 banners, all different). Leading spaces may differ.
  4. All five ids appear; B2 (weight 3) appears most often.
  5. Three banners stacked in the zone each time.

#### TC-ROT-03 Expired and not yet started banners are never shown, and the active count follows the schedule

- **Covers**: US2-2, FR-008, edge cases "schedule expired or not yet started", "zone active banner counts that respect
  the schedule"
- **Preconditions**: TC-ROT-02 done (5 active, zone shows 3).
- **Steps**:
  1. Open QA Copy A. Note the time zone named under **Show from** ("Times are in your time zone (...)"). Set **Show
     from** to tomorrow 09:00. **Publish**.
  2. Open QA Copy B. Set **Show until** to yesterday 18:00 (leave Show from empty). **Publish**.
  3. Open the Banners list.
  4. Zones list, press **Reload**.
  5. Run the id count of TC-ROT-02 step 4 again (200 requests).
- **Expected**:
  1. and 2. "Banner published" each time; state **Published**.
  3. QA Copy A has the flag **Scheduled**; QA Copy B has the flag **Expired**.
  4. `home-top` **Active banners** 3.
  5. The ids of QA Copy A and QA Copy B never appear. Each view still has 3 banners (B1, B2, QA Copy C).

#### TC-ROT-04 A banner whose start time arrives shows without a new publish

- **Covers**: US2-2, FR-008
- **Preconditions**: TC-ROT-03 done.
- **Steps**:
  1. Open QA Copy A. Set **Show from** to the current time plus 3 minutes (in the time zone shown). **Publish**.
     Note the start time.
  2. Before the start time, run 50 requests:
     `for i in $(seq 1 50); do curl -s -A "$UA" $BASE/ | grep -o 'data-mbb="[0-9]*"'; done | sort | uniq -c`
  3. Wait until 1 minute after the start time. Do not publish anything. Run the 50 requests again.
  4. Banners list (reload the view) and Zones list (**Reload**).
- **Expected**:
  2. QA Copy A's id does not appear. Copy A shows **Scheduled**.
  3. QA Copy A's id appears.
  4. The **Scheduled** flag is gone from QA Copy A; `home-top` **Active banners** 4.

#### TC-ROT-05 Unpublish removes a banner and keeps its statistics; 3 slots with 1 active banner shows 1

- **Covers**: US2-4, FR-004a, edge case "zone set to show 3 has only 1 active banner"
- **Preconditions**: TC-ROT-04 done.
- **Steps**:
  1. Reports, **Today**, **Banner** filter `QA Autumn Calendars`. Note impressions and clicks.
  2. Open B2 and press **Unpublish**. Then unpublish QA Copy A and QA Copy C. (QA Copy B is expired.)
  3. Within 5 seconds run:
     `for i in $(seq 1 50); do curl -s -A "$UA" $BASE/ | grep -o 'data-mbb="[0-9]*"' | wc -l; done | sort | uniq -c`
  4. Reports, Today, Banner filter `QA Autumn Calendars`, **Reload**.
  5. Restore: open B2 and press **Publish**. Set `home-top` **Banners shown at once** back to `1` and **Save**. Delete
     QA Copy A, QA Copy B and QA Copy C (**Delete**, confirm each).
- **Expected**:
  2. "Banner unpublished"; state **Unpublished** for B2.
  3. One line `50 1`: each view shows only B1, once.
  4. The numbers are at least those of step 1 (statistics are kept; they can only grow by a pending flush).
  5. "Banner published", "Zone saved", "Banner deleted" three times. Zones list `home-top` **Active banners** 2.

### COUNT: impressions, clicks and reports

#### TC-COUNT-01 Viewability: below the fold counts only when seen, and only once per page view

- **Covers**: QS7, US3-6, FR-011
- **Preconditions**: TC-SHOW-02 done; `long-bottom` is in **Placed but not set up**. Browser window of normal size
  (at most 1200 px high) so `long-bottom` is below the fold.
- **Steps**:
  1. Create `long-bottom` from the requested list: Name `Long page, bottom`, Banners shown at once 1, Image width 300,
     height 250, Server. **Save**.
  2. **Create banner**: Type Image, **Name** `QA Below Fold`, sample image, **Link** `https://example.org/below`, tick
     **Open in a new window**, **Alternative text** `Invented below the fold offer`, **Title** `Invented title text`,
     zone "Long page, bottom". **Publish**. Note B3's id.
  3. Reports, Today, Banner `QA Below Fold`: note impressions (0 for a new banner).
  4. In the private window open `http://localhost:5100/long` with devtools Network open. Do not scroll. Wait 5 seconds.
     Navigate to `http://localhost:5100/`.
  5. Open `/long` again. Scroll down until the banner is fully visible, wait 2 seconds, scroll back to the top, wait
     2 seconds, scroll down to the banner again, wait 2 seconds. Navigate away.
  6. Wait 10 seconds. Reports, Reload.
- **Expected**:
  4. No `POST /_banners/seen` request carries B3's id during or at the end of the first view (a seen request may appear
     on leaving but its body holds no line, or no request appears).
  5. One `POST /_banners/seen` with status 204 and request body `<token> <B3>` (one line for B3), sent within about 2
     seconds of the first 1 second of visibility or on leaving the page. No second line for B3 in this page view.
  6. QA Below Fold: **Impressions** 1 more than in step 3 (exactly 1, not 2), clicks unchanged.

#### TC-A11Y-01 Accessible image banner markup

- **Covers**: FR-005, contracts/markup.md
- **Preconditions**: TC-SHOW-08 and TC-COUNT-01 done.
- **Steps**:
  1. View the source of `http://localhost:5100/` (B1, new window off, no title).
  2. View the source of `http://localhost:5100/long` (B3, new window on, title set).
  3. On the home page press Tab from the address bar until the banner link has focus; press Enter.
- **Expected**:
  1. `<a href="/_banners/c/<B1>?z=<home-top>" rel="sponsored noopener">` with no `target` and no `title`; the `<img>`
     has a non empty `alt`, `width` and `height`, `loading="lazy"` and `decoding="async"`.
  2. The B3 link has `rel="sponsored noopener"`, `target="_blank"` and `title="Invented title text"`; the image has
     `alt="Invented below the fold offer"`, `width="300"` and `height="250"`.
  3. The link receives a visible focus outline, and Enter follows it to `https://example.org/show`.

#### TC-COUNT-02 Scripts off: the banner shows and its link works, no impression is counted, the click is counted

- **Covers**: QS8, US3-7, FR-011
- **Preconditions**: TC-SHOW-08 done. A second browser profile (or another browser) in which JavaScript is blocked for
  `http://localhost:5100` (Chrome: Settings > Privacy and security > Site settings > JavaScript > "Not allowed to use
  JavaScript", add `http://localhost:5100`). Do not block it in the profile that holds the back office.
- **Steps**:
  1. Reports, Today, Zone `home-top`: note impressions and clicks per banner.
  2. In the scripts-off profile, with devtools Network open, open `http://localhost:5100/` and keep it on screen
     5 seconds. Reload twice more, 5 seconds each.
  3. Click the banner once.
  4. Wait 10 seconds. Reports, Reload.
- **Expected**:
  2. The banner image shows each time. No request to `/_banners/seen` is made.
  3. The browser goes through `/_banners/c/<id>?z=<home-top>` (302) to the stored link.
  4. Impressions unchanged for every `home-top` banner. Clicks +1 for the banner that was clicked.

#### TC-COUNT-03 Clicks count only for people: bots, fetches and prefetches are redirected but not counted

- **Covers**: US3-2, US3-3, FR-011, FR-013, README "Clicks and click rate"
- **Preconditions**: TC-SHOW-07 done; B1 live in `home-top`.
- **Steps**:
  1. Reports, Today, Banner `QA Spring Leaflets`: note impressions and clicks.
  2. Three counted clicks: run 3 times
     `curl -s -o /dev/null -w '%{http_code}\n' -A "$UA" "$BASE$P/c/<B1>?z=<home-top>"`.
  3. Bot clicks: run each once, all with `-o /dev/null -w '%{http_code}\n'` and the same address:
     `-A 'Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)'`,
     `-A 'Mozilla/5.0 (compatible; bingbot/2.0)'`, the default curl agent (no `-A`), and `-A ''` (empty).
  4. Non navigation requests with `-A "$UA"`: add `-H 'Sec-Fetch-Mode: cors' -H 'Sec-Fetch-Dest: empty'`; then
     `-H 'Sec-Purpose: prefetch'`; then `-H 'Purpose: prefetch'`.
  5. Without zone: `curl -s -o /dev/null -w '%{http_code}\n' -A "$UA" "$BASE$P/c/<B1>"`.
  6. Bot impression: fetch a token
     `H=$(curl -s -A "$UA" $BASE/); T=$(printf '%s' "$H" | grep -o 'data-mbz-token="[^"]*"' | cut -d'"' -f2); ID=$(printf '%s' "$H" | grep -o 'data-mbb="[0-9]*"' | cut -d'"' -f2); echo "$T $ID" > $W/bot.txt`
     and post it as a bot:
     `curl -s -o /dev/null -w '%{http_code}\n' -A 'Googlebot/2.1' -X POST -H 'Content-Type: text/plain;charset=UTF-8' --data-binary @$W/bot.txt $BASE$P/seen`.
  7. Post the same file with `post_seen $W/bot.txt`.
  8. Wait 10 seconds. Reports, Reload; also check the banner with id `$ID`.
- **Expected**:
  2. `302` three times.
  3. `302` each time.
  4. `302` each time.
  5. `302`.
  6. `204`.
  7. `204`.
  8. QA Spring Leaflets: clicks exactly +3 (only step 2 counted). The banner `$ID` has impressions exactly +1 (the bot
     post did not count and did not use up the token; step 7 counted).

#### TC-COUNT-04 100 impressions and 3 clicks give a click rate of 3.0%

- **Covers**: US3-1, SC-005, FR-011
- **Preconditions**: Phase 4. `cached-top` exists (TC-CLIENT-01). `cached-html` is in **Placed but not set up**. No
  browser tab shows `/cached` during this case.
- **Steps**:
  1. Create `cached-html` from the requested list: Name `Cached page, html`, Banners shown at once 1, width 300, height
     250, **Render mode** **Client**. Save.
  2. **Create banner**: Image, **Name** `QA Ratio Check`, sample image, **Link** `https://example.org/ratio`, **Alternative
     text** `Invented ratio check offer`, zone "Cached page, html". **Publish**.
  3. Collect 100 tokens from the delivery endpoint:
     ```bash
     : > $W/ratio.txt
     for i in $(seq 1 100); do J=$(curl -s -A "$UA" "$BASE$P/zone/cached-html"); T=$(printf '%s' "$J" | grep -o '"token":"[^"]*"' | cut -d'"' -f4); ID=$(printf '%s' "$J" | grep -o '"id":[0-9]*' | head -1 | cut -d: -f2); echo "$T $ID" >> $W/ratio.txt; done
     cut -d' ' -f2 $W/ratio.txt | sort | uniq -c
     ```
     Note B5's id.
  4. `post_seen $W/ratio.txt`
  5. Read `clickUrl` from one response (`curl -s "$BASE$P/zone/cached-html"`) and run 3 times
     `curl -s -o /dev/null -w '%{http_code}\n' -A "$UA" "$BASE<clickUrl>"`.
  6. Wait 10 seconds. Reports, **Today**, **Banner** `QA Ratio Check`.
- **Expected**:
  3. One line: `100 <B5>`; every line of the file has a token.
  4. Four lines `204 ...`.
  5. `302` three times.
  6. **Totals**: Impressions 100, Clicks 3, **Click rate** 3.0%. One row in **By banner and zone**: QA Ratio Check,
     cached-html, 100, 3, 3.0%. The notes "Statistics days are UTC days." and "Counts may be up to one minute behind."
     are shown.

#### TC-COUNT-05 Reports: periods, filters, chart and CSV export

- **Covers**: QS10, US3-1, US3-5, FR-015
- **Preconditions**: Phases 1 to 6 done.
- **Steps**:
  1. Reports. Choose **Period** **Today**, no filters.
  2. Compare the **Totals** with the sum of the rows in **By banner and zone**, and with today's value in **Per day**.
  3. Filter **Zone** `home-top`, then **Banner** `QA Below Fold`, then **Zone** All and **Banner** All.
  4. Press **Download CSV** for Today with no filter. Open the file in a text editor.
  5. Choose **Last 7 days**, **Last 30 days**, **This month**, **Last month**.
  6. Choose **Custom**: **From** today, **To** yesterday. Then From 2025-01-01, To 2026-11-26 (more than 366 days).
  7. Choose Last month with Banner `QA Ratio Check`.
  8. Compare the per banner numbers with the deltas recorded in TC-COUNT-01 (B3 impressions), TC-COUNT-02 (clicks) and
     TC-CLIENT-01 (cached-top impressions).
- **Expected**:
  2. Impressions and clicks of Totals equal the row sums and today's daily value. Click rate is clicks / impressions
     with one decimal; a row with 0 impressions shows a dash.
  3. The table, totals and chart follow the filter.
  4. File name of the form `banners-<from>-<to>.csv`. First line exactly `date,zone,banner,impressions,clicks`. One row
     per banner per zone per day; the rows and numbers equal the table.
  5. Each preset loads without error; the days are UTC days.
  6. Both are refused with "Choose a period that ends on or after its start and covers at most 366 days."
  7. "No impressions or clicks were counted in this period."
  8. The report matches the recorded deltas.

#### TC-COUNT-06 A deleted banner keeps its statistics; a banner in two zones counts per zone

- **Covers**: FR-007, edge case "banner assigned to two zones", README "The same banner in two zones"
- **Preconditions**: TC-CLIENT-01, TC-COUNT-04 and TC-SEC-09 done (B5 renamed).
- **Steps**:
  1. Reports, Today, no filter. Note the rows of B1 and B2 and the row of B5 (now `=QA formula 2027`).
  2. Open B5 and press **Delete**; confirm "Delete =QA formula 2027?".
  3. Reports, Today, Reload.
  4. Download CSV again.
- **Expected**:
  1. B1 and B2 each have separate rows for `home-top` and `cached-top`.
  2. "Banner deleted". B5 is gone from the Banners list.
  3. A row "(deleted banner)" in zone `cached-html` with exactly B5's earlier impressions and clicks. Totals unchanged.
  4. The CSV has the same row with banner "(deleted banner)".

#### TC-COUNT-07 (optional, restart) Counts are written on a clean shutdown

- **Covers**: FR-012 ("and on clean shutdown")
- **Preconditions**: Phase 8. H1 is live in `long-html`. You can stop and start the site (see Environment).
- **Steps**:
  1. Reports, Today, Banner `QA Html Notice`: note impressions.
  2. Collect 5 tokens of `long-html`:
     `: > $W/stop.txt; for i in 1 2 3 4 5; do H=$(curl -s -A "$UA" $BASE/long); T=$(printf '%s' "$H" | grep -o 'data-mbz="long-html"[^>]*' | grep -o 'data-mbz-token="[^"]*"' | cut -d'"' -f2); ID=$(printf '%s' "$H" | grep -o 'data-mbb="[0-9]*"' | head -1 | cut -d'"' -f2); echo "$T $ID" >> $W/stop.txt; done`
  3. `post_seen $W/stop.txt` and, within 2 seconds, stop the site with Ctrl+C in its terminal.
  4. Start the site again with the start command. Wait until the home page loads, then 40 seconds more.
  5. Reports, Today, Banner `QA Html Notice`.
- **Expected**:
  3. `204`; the site stops cleanly (no crash).
  5. Impressions exactly +5 compared with step 1 (none lost by the stop).

### CLIENT: client mode on cached pages

#### TC-CLIENT-01 Client mode on a cached page: each visitor gets its own choice and its own impression

- **Covers**: QS9, US4-1, FR-010
- **Preconditions**: Phases 1 to 3 done; B1 and B2 live in `home-top`. Two browsers or profiles, not signed in (for
  example a private window and the scripts-on second profile).
- **Steps**:
  1. Create `cached-top` from the requested list: Name `Cached page, top`, Banners shown at once 1, width 728, height
     90, **Render mode** **Client**. Save.
  2. Open B1, tick "Cached page, top" in addition to "Home page, top", **Publish**. Do the same for B2.
  3. Wait 60 seconds (so the output cache of `/cached` from earlier visits expires).
  4. Reports, Today, Zone `cached-top`: note the numbers (0 for a new zone).
  5. In browser 1 open `http://localhost:5100/cached` with devtools Network open; keep it on screen 3 seconds. Note the
     "Rendered at" time, the banner shown and the `token` in the response of `GET /_banners/zone/cached-top`.
  6. Within 60 seconds do the same in browser 2.
  7. Wait 10 seconds. Reports, Reload.
- **Expected**:
  2. "Banner published" twice; zones list `cached-top` **Active banners** 2.
  5. `GET /_banners/zone/cached-top` returns 200 JSON with one banner; the banner renders in the placeholder; one
     `POST /_banners/seen` (204) follows.
  6. Same "Rendered at" time as browser 1 (the page came from the output cache), but its own `GET
     /_banners/zone/cached-top` with a different `token`. The banner is chosen for this view (it may be the same
     banner; repeat in a new private window until the two differ at least once).
  7. Zone `cached-top`: impressions +2 for the two views in steps 5 and 6 (plus one for each repeat in step 6), split
     over the banners that were shown.

#### TC-CLIENT-02 Client mode markup, and two zones on one page

- **Covers**: contracts/markup.md (client mode), FR-010, edge case "two zones on one page"
- **Preconditions**: TC-COUNT-04 done (`cached-top` and `cached-html` both exist in client mode). Wait 60 seconds
  after TC-COUNT-04 step 1 so the cached page is rendered again.
- **Steps**:
  1. `curl -s $BASE/cached | grep -o '<div class="mb-zone[^>]*>\|<script src="[^"]*banners.js[^"]*"[^>]*>'`
  2. In a private window open `/cached` with devtools Network open; keep it on screen 3 seconds.
- **Expected**:
  1. Two placeholders
     `<div class="mb-zone" data-mbz="cached-top" data-mb-base="/_banners" data-mbz-mode="client">` and the same for
     `cached-html`, with no `data-mbz-token` and no banner inside, and exactly one script tag
     `<script src="/meresoft-banners/banners.js" defer>`.
  2. `banners.js` is loaded once. One `GET /_banners/zone/cached-top` and one `GET /_banners/zone/cached-html`, both 200.
     Each zone shows its own banner (cached-top: B1 or B2; cached-html: B5). One or two `POST /_banners/seen` with a
     line for each banner seen.

#### TC-CLIENT-03 Client mode with scripts off shows nothing and fails quietly

- **Covers**: US4-2, README "Cached pages"
- **Preconditions**: TC-CLIENT-01 done; the scripts-off profile of TC-COUNT-02.
- **Steps**:
  1. Reports, Today, Zone `cached-top`: note impressions.
  2. In the scripts-off profile open `http://localhost:5100/cached` with devtools Network open; wait 5 seconds.
  3. Wait 10 seconds. Reports, Reload.
- **Expected**:
  2. The page shows "A cached page" and the "Rendered at" line; no banner, no empty box, no error text. No request to
     `/_banners/zone/...` or `/_banners/seen`.
  3. Impressions unchanged.

### REP: view tokens and replays

#### TC-REP-01 A replayed impression report counts once

- **Covers**: edge case "single-use view token", contracts/public-endpoints.md (replay set)
- **Preconditions**: TC-COUNT-04 done; B5 is the only banner of `cached-html`.
- **Steps**:
  1. Reports, Today, Banner `QA Ratio Check`: note impressions.
  2. One token: `J=$(curl -s -A "$UA" "$BASE$P/zone/cached-html"); T=$(printf '%s' "$J" | grep -o '"token":"[^"]*"' | cut -d'"' -f4); printf '%s <B5>\n%s <B5>\n' "$T" "$T" > $W/replay.txt`
  3. `post_seen $W/replay.txt`, then run it again twice.
  4. Wait 10 seconds. Reports, Reload.
- **Expected**:
  3. `204` each time.
  4. Impressions exactly +1.

#### TC-REP-02 Forged, mismatched and malformed reports are ignored without telling the caller

- **Covers**: contracts/public-endpoints.md (POST seen), edge case "reports can be faked"
- **Preconditions**: TC-REP-01 done.
- **Steps**:
  1. Note B5 impressions (Reports, Today, Banner `QA Ratio Check`).
  2. Get a fresh token `T` as in TC-REP-01 step 2. Build `$W/forged.txt` with these lines:
     `garbage <B5>`; `$T <B1>` (a banner that is not in the token); `$T` with its 20th character changed, followed by
     ` <B5>`; `$T` alone; `$T abc`; an empty line.
  3. `post_seen $W/forged.txt`. Also post an empty body:
     `curl -s -o /dev/null -w '%{http_code}\n' -A "$UA" -X POST -H 'Content-Type: text/plain;charset=UTF-8' --data-binary '' $BASE$P/seen`
  4. Now post the valid line: `printf '%s <B5>\n' "$T" > $W/ok.txt; post_seen $W/ok.txt`
  5. Wait 10 seconds. Reports, Reload.
- **Expected**:
  3. `204` for both (the answer does not reveal which lines counted).
  4. `204`.
  5. Impressions exactly +1 (only step 4). B1's numbers did not change because of step 3.

#### TC-REP-03 (optional, long wait) An expired token is not counted

- **Covers**: edge case "invalid, expired or repeated reports are ignored", ViewTokenLifetime 10 minutes
- **Preconditions**: TC-COUNT-04 done. Start step 1 at the beginning of phase 4 and do step 2 at its end.
- **Steps**:
  1. Get a token for B5 as in TC-REP-01 step 2 and save the line `<token> <B5>` to `$W/old.txt`. Note the time.
  2. At least 11 minutes later note B5 impressions, then `post_seen $W/old.txt`.
  3. Get a fresh token and post it the same way (control).
  4. Wait 10 seconds. Reports, Reload.
- **Expected**:
  2. `204`.
  3. `204`.
  4. Impressions exactly +1 (only the fresh token counted).

### LIM: impression and click limits

#### TC-LIM-04 Limit fields accept only whole numbers of 1 or more

- **Covers**: FR-004, FR-006 (no click limit for HTML banners, checked in TC-HTML-01)
- **Preconditions**: L1 "QA Link Rules" exists as a draft (TC-SHOW-06).
- **Steps**:
  1. Open L1. Enter **Impression limit** `0` and press **Save**. Repeat with `-3`, `2.5`.
  2. Clear Impression limit; enter **Click limit** `0`, press Save.
  3. Set **Impression limit** `500` and **Click limit** `20`; Save. Then clear both and Save.
- **Expected**:
  1. Each is refused with "Enter a whole number of 1 or more, or leave the field empty." (or, from the server, "The
     impression limit must be a whole number, 1 or more.").
  2. Refused in the same way.
  3. "Banner saved" both times; after reopening, the fields hold 500 and 20, then empty.

#### TC-LIM-01 The banner stops after its impression limit and shows "Limit reached"

- **Covers**: QS13, US5-1, README "Limits can be overshot a little"
- **Preconditions**: Phases 1 to 5 done. The home page tab is closed.
- **Steps**:
  1. Unpublish B1 and B2 (so `home-top` has no other live banner).
  2. **Create banner**: Image, **Name** `QA Limit Three`, sample image, **Link** `https://example.org/limited`, **Alternative
     text** `Invented limited campaign`, zone "Home page, top", **Impression limit** `3`. **Publish**. Note B4's id.
  3. In the private window open the home page 5 times; each time keep the banner on screen 2 seconds, then reload.
     Note at which view the banner stopped showing, if it did.
  4. Wait 10 seconds. Reload the home page and view the source.
  5. Banners list; Zones list (**Reload**); Reports, Today, Banner `QA Limit Three`.
- **Expected**:
  1. Both show **Unpublished**.
  3. The banner shows at least on the first 3 views.
  4. The zone writes nothing (no `mb-zone` element, no package script).
  5. B4 has the flag **Limit reached**. `home-top` **Active banners** 0. Impressions between 3 and 5 (the overshoot is at
     most the views of one flush interval, 5 seconds here).

#### TC-LIM-02 Raising the limit and publishing brings the banner back

- **Covers**: US5-2
- **Preconditions**: TC-LIM-01 done.
- **Steps**:
  1. Open B4, set **Impression limit** `10`, press **Save** only. Reload the home page.
  2. Press **Publish**. Reload the home page within 5 seconds.
  3. Banners list.
- **Expected**:
  1. State "Published with unpublished changes"; the home page still shows nothing.
  2. The banner shows again.
  3. The **Limit reached** flag is gone.

#### TC-LIM-03 Click limit

- **Covers**: US5, FR-004 (click limit)
- **Preconditions**: TC-LIM-02 done.
- **Steps**:
  1. Open B4, set **Click limit** `2`, **Publish**.
  2. Run 3 times `curl -s -o /dev/null -w '%{http_code}\n' -A "$UA" "$BASE$P/c/<B4>?z=<home-top>"`.
  3. Wait 10 seconds. Run `curl -s $BASE/ | grep -c 'data-mbb="<B4>"'`.
  4. Banners list; Reports, Today, Banner `QA Limit Three`.
- **Expected**:
  2. `302` three times (a link always works).
  3. `0`.
  4. Flag **Limit reached**; clicks between 2 and 3.

### PERM: permissions

#### TC-PERM-01 After install only Administrators have the package permissions

- **Covers**: US6-3, FR-017
- **Preconditions**: signed in as administrator. No earlier run of TC-PERM-04 left a change.
- **Steps**:
  1. Users > **User groups** > **Administrators**.
  2. Open **Editors**, **Writers**, **Translators** and **Sensitive data** in turn.
  3. Open **Sample banner editors**.
- **Expected**:
  1. **Sections** include **Banners**. Under the **Banners** permissions, **Edit HTML banners** ("Create HTML banners
     and change their HTML. Without it, HTML banners can still be published, unpublished, copied and deleted.") is on.
  2. None of them has the Banners section or Edit HTML banners.
  3. Banners section on, Edit HTML banners off (this group is created by the sample composer, not by the package).

#### TC-PERM-02 A user without the Banners permission sees no section and gets 403 from the API

- **Covers**: QS12, US6-1, contracts/management-api.md
- **Preconditions**: a separate browser profile.
- **Steps**:
  1. Sign in as `editor@sample.invalid`.
  2. Open `http://localhost:5100/umbraco/section/meresoft-banners/zones` directly.
  3. With devtools Network open, Copy as cURL any `management/api` request. Change the URL to, in turn:
     `$BASE/umbraco/management/api/v1/meresoft-banners/zones`, `.../meresoft-banners/banners`,
     `.../meresoft-banners/reports?from=2026-11-26&to=2026-11-26`, `.../meresoft-banners/requested-zones`. Add
     `-s -o /dev/null -w '%{http_code}\n'`.
  4. Call `curl -s -o /dev/null -w '%{http_code}\n' $BASE/umbraco/management/api/v1/meresoft-banners/zones` without
     any credentials.
- **Expected**:
  1. The section bar has no **Banners**.
  2. No zones list and no banner data; the back office shows that the section is not available (or goes to an allowed
     section).
  3. `403` for each.
  4. `401`.

#### TC-PERM-04 An administrator gives another group access in the user group settings

- **Covers**: FR-017, README "Giving other groups access"
- **Preconditions**: TC-PERM-02 done; administrator and editor in separate profiles.
- **Steps**:
  1. As administrator: Users > User groups > **Editors**, tick section **Banners**, leave **Edit HTML banners** off,
     **Save**.
  2. As editor: sign out and sign in again. Open **Banners**, then **Create banner**.
  3. Leave without saving. As administrator untick **Banners** in **Editors** and **Save**.
  4. As editor: sign out and in; repeat TC-PERM-02 step 3 for `.../meresoft-banners/zones`.
- **Expected**:
  2. The **Banners** section is visible; the type choice offers only **Image** (no **HTML**).
  3. Saved.
  4. No Banners section; `403`.

#### TC-PERM-03 A banner editor without the HTML permission can only create image banners

- **Covers**: US6-2, FR-006
- **Preconditions**: a separate profile; signed in as `bannereditor@sample.invalid`.
- **Steps**:
  1. Open **Banners** > **Create banner**.
  2. Create `QA Editor Image`: sample image, Link `https://example.org/editor`, Alternative text `Invented editor
     banner`, no zone. Press **Save** with devtools Network open. Keep the request (used in TC-HTML-03).
- **Expected**:
  1. Banners, Zones and Reports are visible; **Type** offers only **Image**.
  2. "Banner saved", state Draft.

### HTML: HTML banners and their permission

#### TC-HTML-01 HTML banner: create, render as stored, count by the wrapper box, no click limit

- **Covers**: FR-006, contracts/markup.md (Html banners), edge case "two zones on one page" (server mode)
- **Preconditions**: Phase 5; administrator; `long-html` is in **Placed but not set up**.
- **Steps**:
  1. Create `long-html` from the requested list: Name `Long page, html`, Banners shown at once 1, no image size,
     Server. Save.
  2. **Create banner**: **Type** **HTML**. Check the fields offered.
  3. **Name** `QA Html Notice`. **HTML code**:
     `<div class="qa-html" style="padding:12px;border:1px solid #888">Invented HTML notice for the sample gazette 2027</div>`.
     Zones "Long page, html" and "Cached page, html". **Publish**. Note H1's id.
  4. Reports, Today, Banner `QA Html Notice`: note impressions (0).
  5. View the source of `http://localhost:5100/long`.
  6. Open `/long` in the private window with devtools Network open; stay at the top 3 seconds; leave. Wait 10 seconds.
     Reports, Reload.
- **Expected**:
  2. **HTML code** field with "Shown on the site exactly as written, so scripts from an ad network work. Only add code
     you trust.", **Impression limit** shown, **Click limit** not shown. No image picker.
  3. "Banner published".
  5. Zone `long-html` holds `<div class="mb-banner mb-html" data-mbb="<H1>">` followed by the stored HTML exactly as
     typed. Zone `long-bottom` still holds B3. The page has exactly one `<script src="/meresoft-banners/banners.js"
     defer>`.
  6. The HTML notice shows; a `POST /_banners/seen` has a line for H1. H1 impressions +1 in zone `long-html`.

#### TC-HTML-02 Banner editor: HTML read only, but publish, unpublish, copy and statistics work

- **Covers**: US6-2, FR-006, README "Permissions"
- **Preconditions**: TC-HTML-01 done; banner editor signed in (TC-PERM-03).
- **Steps**:
  1. As banner editor open "QA Html Notice".
  2. Try to type in **HTML code**.
  3. Press **Unpublish**. Reload `/long` in the private window within 5 seconds.
  4. Press **Publish**. Reload `/long` within 5 seconds.
  5. Open **Reports**, Today, Banner `QA Html Notice`.
- **Expected**:
  1. The HTML code is shown with the note "You need the HTML banner permission to change this code."
  2. The field is read only.
  3. "Banner unpublished"; the HTML notice is gone from `/long`.
  4. "Banner published"; the notice is back.
  5. The numbers of H1 are shown.

#### TC-HIST-03 History records the acting user, and Copy is recorded on the copy

- **Covers**: FR-018, US6 (audit)
- **Preconditions**: TC-HTML-02 done; banner editor signed in.
- **Steps**:
  1. As banner editor open "QA Html Notice" and press **Copy**. Open the new banner "QA Html Notice (copy)".
  2. Open its **History** tab.
  3. Open "QA Html Notice", **History** tab.
  4. Delete "QA Html Notice (copy)" (allowed without the HTML permission).
- **Expected**:
  1. "Banner copied"; the copy is a Draft with the same HTML (read only for this user).
  2. One entry: **Change** "Copied", **Who** "Sample Banner Editor", **When** now.
  3. Newest first: "Published" and "Unpublished" by "Sample Banner Editor", below them "Published" and "Created" by the
     administrator.
  4. "Banner deleted".

#### TC-HTML-03 The management API refuses HTML changes without the HTML permission

- **Covers**: US6-2, FR-006, contracts/management-api.md (`html-permission-required`)
- **Preconditions**: TC-PERM-03 and TC-HTML-02 done; banner editor signed in, devtools Network open.
- **Steps**:
  1. Open "QA Html Notice". Change only the **Name** to `QA Html Notice renamed`, **Save**.
  2. Press **Reload** in the editor; in Network open the response of `GET .../meresoft-banners/banners/<key>` and note
     its `updateDate`.
  3. Copy as cURL the `PUT .../meresoft-banners/banners/<key>` request of step 1. In its body change `"html"` to
     `"<div>QA changed by banner editor</div>"` and `"updateDate"` to the value from step 2. Run it with
     `-s -w '\n%{http_code}\n'`.
  4. Copy as cURL the `POST .../meresoft-banners/banners` request of TC-PERM-03 step 2. In its body set `"type"` to
     `"Html"`, add `"html":"<div>QA html by banner editor</div>"`, set `"mediaKey"` to `null`. Run it.
  5. Reload the editor of QA Html Notice; view the source of `/long`.
  6. Delete "QA Editor Image".
- **Expected**:
  1. "Banner saved" (renaming an HTML banner needs no HTML permission).
  3. `403`, problem details with `"type"` containing `html-permission-required`.
  4. `403`, same type. No new banner appears in the list.
  5. The HTML is unchanged.
  6. "Banner deleted".

### HIST: history

#### TC-HIST-01 Banner history

- **Covers**: FR-018, US6 (audit)
- **Preconditions**: Phase 7 (B1 was created, saved, published several times and unpublished in TC-LIM-01).
- **Steps**:
  1. Open B1 "QA Spring Leaflets", tab **History**.
  2. Compare with the actions done on B1 in this run.
- **Expected**:
  1. A table **History** with columns **When**, **Who**, **Change**, newest first. Entries "Unpublished",
     "Published" (several), "Saved", "Created". **Who** is the administrator's name ("Sample Admin"). Times are plausible
     and in the user's time zone.
  2. Every save, publish and unpublish of the run has an entry. No banner content (link, alt text) is shown in the
     history.

#### TC-HIST-02 Zone history

- **Covers**: FR-018
- **Preconditions**: Phase 7.
- **Steps**:
  1. Zones, open `home-top`, tab **History**.
- **Expected**:
  1. Newest first: "Saved" twice (show count to 3 and back to 1 in TC-ROT-02 and TC-ROT-05), then "Created", all by the
     administrator.

### IMPORT: import surface

#### TC-IMPORT-01 Live counts are not marked as imported

- **Covers**: FR-016, README "Importing statistics" (report marks imported days)
- **Preconditions**: Phase 7; nothing has been imported on this site.
- **Steps**:
  1. Reports, **Last 30 days**, no filter. Look at the **Per day** chart, the daily table ("Show the daily values as a
     table") and the area below the totals.
  2. Look at the CSV of TC-COUNT-05.
- **Expected**:
  1. No day is marked "imported"; no "Days with imported numbers" list is shown.
  2. No import column; header exactly `date,zone,banner,impressions,clicks`.
  - Note: importing itself is a C# service (`IBannerImportService`) with no UI and no HTTP endpoint, so the positive
    path (imported days marked, Add and Replace modes, limits after an import) cannot be run with browser or curl. It
    is covered by the integration tests; record this case as "partial" in the report.

#### TC-IMPORT-02 No import endpoint is exposed over HTTP

- **Covers**: FR-016 (import is a documented C# service only), attack surface
- **Preconditions**: administrator signed in.
- **Steps**:
  1. `curl -s -o /dev/null -w '%{http_code}\n' -X POST $BASE$P/import` and the same with `GET`.
  2. With the administrator's Copy as cURL, call `GET` and `POST`
     `$BASE/umbraco/management/api/v1/meresoft-banners/import` and `.../meresoft-banners/statistics/import`.
  3. If `http://localhost:5100/umbraco/swagger/` is available (Development), choose the `meresoft-banners` definition.
- **Expected**:
  1. `404` both times.
  2. `404` (or `405`) for each; never `2xx`.
  3. Only zones, requested-zones, banners and reports endpoints; none for import.

### SEC: security of the public endpoints and of banner data

#### TC-SEC-01 No open redirect

- **Covers**: QS11, US3-4, contracts/public-endpoints.md (click)
- **Preconditions**: TC-SHOW-07 done.
- **Steps**:
  1. `curl -s -D - -o /dev/null "$BASE$P/c/99999?url=https://evil.example"`
  2. `curl -s -D - -o /dev/null "$BASE$P/c/<B1>?z=<home-top>&url=https://evil.example&returnUrl=https://evil.example&to=//evil.example&redirect=https%3A%2F%2Fevil.example"`
  3. `curl -s -D - -o /dev/null -H 'Referer: https://evil.example/' -H 'Host: evil.example' "$BASE$P/c/<B1>?z=<home-top>"`
  4. `curl -s -o /dev/null -w '%{http_code} %{redirect_url}\n' "$BASE$P/c/<B1>/https:%2F%2Fevil.example"`
  5. `curl -s -L -o /dev/null -w '%{url_effective}\n' "$BASE$P/c/<B1>?z=<home-top>&url=https://evil.example"`
- **Expected**:
  1. `404`, no `Location` header.
  2. `302`, `Location: https://example.org/show`.
  3. `302`, `Location: https://example.org/show`.
  4. `404`, empty redirect URL.
  5. Ends at `https://example.org/show`, never at `evil.example`.

#### TC-SEC-02 Click ids: non numeric and unknown ids give 404; a scan stays bounded

- **Covers**: contracts/public-endpoints.md ("ids are integers; anything else is 404"; unknown id 404), README "A scan
  of the click address"
- **Preconditions**: TC-SHOW-07 done.
- **Steps**:
  1. For each path run `curl -s -o /dev/null -w '%{http_code}\n' "$BASE$P/<path>"`: `c/abc`, `c/12abc`, `c/-1`,
     `c/+1`, `c/1.0`, `c/0x1F`, `c/%201`, `c/2147483648`, `c/0`, `c/99999`, `c/<B1>?z=abc`, `c/<B1>?z=-1`,
     `c/<L1>?z=<home-top>` (draft, never published).
  2. Scan 1,000 unknown ids:
     `for i in $(seq 900001 901000); do curl -s -o /dev/null -w '%{http_code} %{time_total}\n' "$BASE$P/c/$i"; done > $W/scan.txt; cut -d' ' -f1 $W/scan.txt | sort | uniq -c; cut -d' ' -f2 $W/scan.txt | sort -n | awk '{a[NR]=$1} END{print "p95", a[int(NR*0.95)]}'`
  3. Right after, load `$BASE/` with `curl -s -o /dev/null -w '%{http_code} %{time_total}\n'`.
- **Expected**:
  1. `404` for every path, including the draft L1 (no live version).
  2. `1000 404`; p95 below 0.05 s on this machine.
  3. `200`, well under 1 second.

#### TC-SEC-03 Public endpoints set no cookies, send no-store and noindex

- **Covers**: FR-014, contracts/public-endpoints.md (headers)
- **Preconditions**: Phase 4 (B1 live, `cached-top` exists).
- **Steps**:
  1. For each request run it with `curl -s -D - -o /dev/null` and look at the headers:
     a. `GET $BASE$P/c/<B1>?z=<home-top>` (302)
     b. `GET $BASE$P/c/99999` (404)
     c. `GET $BASE$P/zone/cached-top` (200)
     d. `GET $BASE$P/zone/qa-junk-alias` (200, empty)
     e. `GET $BASE$P/zone/9bad` (404)
     f. `GET $BASE$P/seen` (405)
     g. `POST $BASE$P/seen` with `-H 'Content-Type: text/plain;charset=UTF-8' --data-binary ''` (204)
  2. In a new private window, with devtools Network open, visit `/`, `/long` and `/cached`, wait 3 seconds on each,
     click a banner. Then open the site settings of `localhost:5100` (padlock or info icon > Cookies and site data).
  3. In Network, select the `POST /_banners/seen` request and look at its request headers.
- **Expected**:
  1. Every response has `Cache-Control: no-store` and `X-Robots-Tag: noindex, nofollow`, and none has `Set-Cookie`.
  2. No response from `/_banners/...` or `/meresoft-banners/...` sets a cookie; the site settings list no cookie
     created by these responses. If any cookie exists, record its name and the response that set it.
  3. Content type `text/plain;charset=UTF-8`; no preflight `OPTIONS` request before it.

#### TC-SEC-04 Links with control characters or whitespace are refused

- **Covers**: edge case "link not valid", `LinkUrlRules` (no control characters, no whitespace), header splitting
- **Preconditions**: administrator; L1 "QA Link Rules" exists as a draft; devtools Network open.
- **Steps**:
  1. Open L1, press **Save** (no change). Copy as cURL the `PUT .../meresoft-banners/banners/<key>` request. Press the
     editor's **Reload** and take `updateDate` from the `GET` response.
  2. Run the copied command with the new `updateDate` and, in turn, these `linkUrl` values (JSON escapes inside the
     body): `"https://example.org/qa\u0007bell"`, `"https://example.org/qa\r\nSet-Cookie: qa=1"`,
     `"https://example.org/\u0000"`, `"https://example.org/a\tb"`, `"https://example.org/a b"`,
     `"javascript:alert(1)"`, `"//evil.example/x"`, `"https://"`, and a 2,049 character `https://example.org/aaa...`
     address. Add `-s -w '\n%{http_code}\n'`.
  3. Reload the L1 editor.
- **Expected**:
  2. `400` each time with problem details whose `errors` has `linkUrl`: "The link must be an absolute http or https
     address of at most 2048 characters."
  3. **Link** still holds `https://example.org/rules`.

#### TC-SEC-05 An HTML banner has no click address

- **Covers**: contracts/public-endpoints.md ("Html banner: 404"), FR-006
- **Preconditions**: TC-HTML-01 done.
- **Steps**:
  1. `curl -s -D - -o /dev/null -A "$UA" "$BASE$P/c/<H1>?z=<long-html>"`
  2. `curl -s -o /dev/null -w '%{http_code}\n' -A "$UA" "$BASE$P/c/<H1>"`
  3. Wait 10 seconds. Reports, Today, Banner `QA Html Notice`.
- **Expected**:
  1. `404`, no `Location`.
  2. `404`.
  3. Clicks 0 (shown as 0, click rate a dash or 0.0%).

#### TC-SEC-06 Zone delivery returns only public fields of the live version

- **Covers**: FR-010, contracts/public-endpoints.md (zone delivery)
- **Preconditions**: TC-HTML-01 done (`cached-html` holds B5 and H1).
- **Steps**:
  1. Open B5 "QA Ratio Check", change **Alternative text** to `QA draft alt never public`, press **Save** only.
  2. Run `curl -s $BASE$P/zone/cached-html` about 10 times until you have one answer with an image banner and one with
     an HTML banner.
  3. Search all answers for `QA Ratio Check`, `QA Html Notice`, `QA draft alt never public`, `weight`, `limit`,
     `start`, `end`, `impression`, `click"` (as a key), `updateDate`, `key`, `name`, and for a GUID pattern
     (`grep -E '[0-9a-f]{8}-[0-9a-f]{4}-'`).
- **Expected**:
  2. `Content-Type: application/json`. Top level keys only `zone` (`"cached-html"`), `token`, `banners`. Image banner
     keys only `id`, `type` (`"image"`), `clickUrl` (`/_banners/c/<B5>?z=<cached-html>`), `newWindow`, `imageUrl`,
     `width` (300), `height` (250), `alt` (`"Invented ratio check offer"`, the live text), `title` (null). HTML banner
     keys only `id`, `type` (`"html"`), `html` (the stored HTML).
  3. None of them is found.

#### TC-SEC-07 Zone delivery: alias normalisation, invalid aliases and unknown aliases

- **Covers**: contracts/public-endpoints.md (zone delivery), FR-003 (requested zones only from templates)
- **Preconditions**: Phase 4 (`cached-top` exists in client mode).
- **Steps**:
  1. Run `curl -s -w '\n%{http_code}\n' "$BASE$P/zone/<alias>"` with `<alias>`: `CACHED-TOP`, `%20cached-top%20`.
  2. The same with `9cached`, `-top`, `cached.top`, `cached%20top`, `caf%C3%A9`, `cached%00top`, and 101 times `a`.
  3. The same with `qa-junk-alias` and `qa-junk-two`.
  4. Wait 10 seconds. Zones, **Reload**.
- **Expected**:
  1. `200` with `"zone":"cached-top"` and banners.
  2. `404` each time.
  3. `200` with `"zone":"qa-junk-alias"` (and `"qa-junk-two"`), `"banners":[]` and no token (absent or null).
  4. Neither `qa-junk-alias` nor `qa-junk-two` is listed in **Placed but not set up**.

#### TC-SEC-08 Impression endpoint: method and size limits

- **Covers**: contracts/public-endpoints.md (POST seen: 405, 413, 16 KB)
- **Preconditions**: none beyond a running site.
- **Steps**:
  1. `curl -s -D - -o /dev/null $BASE$P/seen` (GET), then with `-X PUT` and `-X DELETE`.
  2. `head -c 17000 /dev/zero | tr '\0' 'a' > $W/big.txt; curl -s -D - -o /dev/null -A "$UA" -X POST -H 'Content-Type: text/plain;charset=UTF-8' --data-binary @$W/big.txt $BASE$P/seen`
  3. Send 17,000 bytes with chunked transfer encoding (no Content-Length):
     `curl -s -o /dev/null -w '%{http_code}\n' -A "$UA" -X POST -H 'Transfer-Encoding: chunked' -H 'Content-Type: text/plain;charset=UTF-8' --data-binary @$W/big.txt $BASE$P/seen`
  4. `head -c 16000 /dev/zero | tr '\0' 'a' > $W/ok16.txt` and post it the same way as step 2.
- **Expected**:
  1. `405` with `Allow: POST`, `Cache-Control: no-store`, `X-Robots-Tag: noindex, nofollow`, for each method.
  2. `413`, with the same two headers.
  3. `413`.
  4. `204` (a body within the limit is accepted, its junk is ignored).

#### TC-SEC-09 CSV export neutralises formula cells

- **Covers**: contracts/management-api.md (CSV injection)
- **Preconditions**: TC-COUNT-04 done (B5 has numbers today).
- **Steps**:
  1. Open B5, change **Name** to `=QA formula 2027`, **Save**.
  2. Reports, Today, **Download CSV**. Open the file in a text editor.
- **Expected**:
  2. The B5 row has the banner cell `'=QA formula 2027` (prefixed with `'`). No cell of the file starts with `=`, `+`,
     `-` or `@` without the prefix.

#### TC-SEC-10 Banner text is encoded in the page and in the back office

- **Covers**: FR-005, output encoding (image banners are not HTML banners)
- **Preconditions**: TC-COUNT-01 done (B3 live on `long-bottom`).
- **Steps**:
  1. Open B3. Set **Alternative text** to `QA "quoted" <b>bold</b> & more`, **Title** to
     `<script>alert('qa')</script>`, **Name** to `QA Below Fold <i>x</i>`. **Publish**.
  2. Open `/long` in the private window, scroll to the banner, and view the source.
  3. Open the Banners list and B3's editor.
- **Expected**:
  2. No alert dialog. The source has `alt="QA &quot;quoted&quot; &lt;b&gt;bold&lt;/b&gt; &amp; more"` (or an equivalent
     encoding) and the title encoded the same way; no raw `<script>` from the title.
  3. The name is shown as plain text `QA Below Fold <i>x</i>` (not in italics); no alert.

#### TC-SEC-11 (optional, restart) PublicPath rules at startup

- **Covers**: contracts/configuration-and-csharp.md (PublicPath rules), README "Configuration", FR-022
- **Preconditions**: Phase 8, last case of the run. You can stop and start the site. The setting is given as an
  environment variable, so no file is edited.
- **Steps**:
  1. Stop the site (Ctrl+C). For each value below, start the site with the start command and the extra variable
     `export Meresoft__Banners__PublicPath='<value>'`, read the console, stop it:
     `/umbraco/ads`, `/UMBRACO`, `/ads/`, `ads`, `/`, `/ads//x`, `/ads?x`, `/ads#x`, `/ads%20x`, `/ads x`, `/qa{x}`.
  2. Start with `Meresoft__Banners__PublicPath=/qa-ads`. Open the home page and view the source.
  3. Run `curl -s -o /dev/null -w '%{http_code} %{redirect_url}\n' "$BASE/qa-ads/c/<B4>?z=<home-top>"` and the same
     for `$BASE/_banners/c/<B4>?z=<home-top>`; `curl -s -o /dev/null -w '%{http_code}\n' -X POST -A "$UA" -H 'Content-Type: text/plain;charset=UTF-8' --data-binary '' $BASE/qa-ads/seen`.
  4. Stop the site, `unset Meresoft__Banners__PublicPath`, start it normally. Open the home page.
- **Expected**:
  1. The site does not start. The message names the key, for example
     `Meresoft:Banners:PublicPath must not start with '/umbraco'.` (`/umbraco/ads`, `/UMBRACO`),
     `Meresoft:Banners:PublicPath must start with '/' and must not end with '/'.` (`/ads/`, `ads`, `/`),
     `Meresoft:Banners:PublicPath must not contain { } ? # % or whitespace, and must not contain '//'.` (the others).
  2. The site starts. If a banner is live in `home-top`, its link is `/qa-ads/c/<id>?z=<id>` and the zone has
     `data-mb-base="/qa-ads"`. (If B4 has reached its limit, publish B1 first to see markup.)
  3. `/qa-ads/c/...` gives `302` to the stored link (or `404` if B4 is not live); `/_banners/c/...` gives `404`;
     `/qa-ads/seen` gives `204`.
  4. The site runs with `/_banners` again.

### A11Y: accessibility

(TC-A11Y-01 is listed in phase 3 above, next to the banner it checks.)

#### TC-A11Y-02 The report chart has a text alternative and a table

- **Covers**: README "Reports" (text alternative for screen readers), FR-015, FR-021
- **Preconditions**: Phase 7; there are numbers for the last 7 days.
- **Steps**:
  1. Reports, **Last 7 days**.
  2. Using only the keyboard (Tab, Shift+Tab, Enter, Space), reach the period choice, the filters, **Download CSV** and
     "Show the daily values as a table".
  3. Activate "Show the daily values as a table".
- **Expected**:
  1. Next to the chart a text such as "Impressions and clicks per day from <from> to <to>: <n> impressions and <m>
     clicks in total.", whose totals equal the **Totals**.
  2. Every control is reachable in a logical order with a visible focus.
  3. A table with **Day**, **Impressions** and **Clicks**, one row per day, equal to the chart.

#### TC-A11Y-03 The banner editor works by keyboard and shows labelled errors

- **Covers**: FR-021 (all texts translatable and shown), editor usability, SC-002
- **Preconditions**: administrator.
- **Steps**:
  1. **Create banner** and use only the keyboard: Tab through all fields, type values, toggle **Open in a new window**
     with Space, tick a zone with Space.
  2. Leave **Alternative text** empty and press **Save** with Enter.
  3. Check that every field has a visible label and that descriptions are in English (no raw keys such as
     `meresoftBanners_...` anywhere in the Banners section: Banners, Zones, Reports, editors, dialogs).
  4. Leave without saving (or delete the draft).
- **Expected**:
  1. Every field is reachable; focus is visible; the image **Choose** button opens the picker and the picker can be
     used and closed with the keyboard.
  2. The error "An image banner needs alternative text." appears at that field; nothing is saved.
  3. All labels and texts are readable English; no localisation key is shown.

### PERF: performance checks that can be run manually

#### TC-PERF-01 A zone adds little time to a page

- **Covers**: SC-003 (manual indicator; the 2 ms gate is the automated `Category=Perf` test, quickstart section 4)
- **Preconditions**: step A in phase 0 (no zone set up), step B in phase 1 after TC-SHOW-08 (one live banner in
  `home-top`). Same machine, nothing else heavy running.
- **Steps**:
  1. (A) Warm up with 20 requests to `$BASE/`, then:
     `for i in $(seq 1 300); do curl -s -o /dev/null -w '%{time_total}\n' $BASE/; done | sort -n | awk '{a[NR]=$1} END{print "p50",a[int(NR*0.5)],"p95",a[int(NR*0.95)]}'`
  2. (B) Repeat step 1 with B1 live.
- **Expected**:
  2. p95 of B minus p95 of A is under 0.005 s. If it is higher, repeat both; report a finding only when the difference
     stays above 0.005 s.

#### TC-PERF-02 Package script size and no third-party requests

- **Covers**: FR-010 (script under 5 KB, no third-party libraries), contracts/public-endpoints.md (package script)
- **Preconditions**: Phase 4.
- **Steps**:
  1. `curl -s $BASE/meresoft-banners/banners.js | wc -c`
  2. `curl -s $BASE/meresoft-banners/banners.js | grep -Eo 'https?://[^"'"'"' )]+' | sort -u`
  3. In a private window with devtools Network open (no filter, "Preserve log" on), open `/`, `/long` (scroll to the
     bottom) and `/cached`, 3 seconds each.
- **Expected**:
  1. Below 5120.
  2. No address of another host (empty output, or only `http://www.w3.org/...` namespace strings if any).
  3. All requests go to `localhost:5100`. `banners.js` is requested once per page. Package requests are only
     `/meresoft-banners/banners.js`, `/_banners/zone/...` (client zones) and `/_banners/seen`.

#### TC-PERF-03 Counting stays exact under a burst

- **Covers**: SC-004, SC-005 at manual scale (quickstart section 4; the full load test is `CountingLoadTests`)
- **Preconditions**: Phase 4; B1 and B2 live in `home-top`; no browser tab on the home page.
- **Steps**:
  1. Reports, Today, Zone `home-top`: note impressions of B1 and B2.
  2. Collect 600 tokens within 10 minutes:
     ```bash
     : > $W/burst.txt
     for i in $(seq 1 600); do H=$(curl -s -A "$UA" $BASE/); T=$(printf '%s' "$H" | grep -o 'data-mbz-token="[^"]*"' | cut -d'"' -f2); ID=$(printf '%s' "$H" | grep -o 'data-mbb="[0-9]*"' | head -1 | cut -d'"' -f2); echo "$T $ID" >> $W/burst.txt; done
     cut -d' ' -f2 $W/burst.txt | sort | uniq -c
     ```
  3. `post_seen $W/burst.txt` (24 posts).
  4. Wait 10 seconds. Reports, Reload.
  5. `post_seen $W/burst.txt` again; wait 10 seconds; Reload.
- **Expected**:
  2. Two lines adding up to 600.
  3. `204` 24 times; each time under 0.1 s.
  4. B1 and B2 impressions in `home-top` grew by exactly the counts of step 2 (no loss, no double count).
  5. No further change (every report is a replay).

---

## 3. Coverage

| Source | Cases |
|---|---|
| QS1 to QS14 | QS1 SHOW-01, QS2 SHOW-02, QS3 SHOW-05, QS4 SHOW-07, QS5 SHOW-08, QS6 ROT-01, QS7 COUNT-01, QS8 COUNT-02, QS9 CLIENT-01, QS10 COUNT-05, QS11 SEC-01, QS12 PERM-02, QS13 LIM-01, QS14 SHOW-10 |
| Quickstart performance checks | PERF-01 (SC-003 indicator), PERF-03 (SC-004, SC-005 at manual scale); the automated gates stay in CI |
| US1 | US1-1 SHOW-07, US1-2 SHOW-04 (fallback not testable on the sample), US1-3 SHOW-01 and SHOW-04, US1-4 SHOW-07 and SHOW-08, US1-5 SHOW-08 |
| US2 | US2-1 ROT-01, US2-2 ROT-03 and ROT-04, US2-3 ROT-02, US2-4 ROT-05 |
| US3 | US3-1 COUNT-04 and COUNT-05, US3-2 SHOW-07 and COUNT-03, US3-3 COUNT-03, US3-4 SEC-01, US3-5 COUNT-05, US3-6 COUNT-01, US3-7 COUNT-02 |
| US4 | US4-1 CLIENT-01, US4-2 CLIENT-03 (fallback not testable on the sample) |
| US5 | US5-1 LIM-01, US5-2 LIM-02, click limit LIM-03 |
| US6 | US6-1 PERM-02, US6-2 PERM-03, HTML-02 and HTML-03, US6-3 PERM-01 |
| Security list | open redirect SEC-01; no cookies SEC-03; no-store SEC-03 and SEC-08; control characters SEC-04; unknown click id SEC-01 and SEC-02; Html banner click 404 SEC-05; public fields SEC-06; invalid aliases SHOW-03 and SEC-07; PublicPath at startup SEC-11 (optional) |
| Edge cases | expired or not yet started ROT-03 and ROT-04; active banner counts ROT-03 to ROT-05; limit reached then raised LIM-01 and LIM-02; scripts off COUNT-02 and CLIENT-03; two zones on one page CLIENT-02 and HTML-01; banner in two zones COUNT-06; deleted banner COUNT-06; no zone SHOW-06 |

## 4. Run order

Run the phases in this order, and the cases inside a phase from top to bottom. Later phases use the banners, zones and
ids created earlier (see the data sheet).

1. **Phase 0, preparation** (clean state, admin signed in): TC-PERM-01, TC-SHOW-01, TC-PERF-01 step A.
2. **Phase 1, first zone and banner** (`home-top`, B1, L1): TC-SHOW-02, TC-SHOW-03, TC-SHOW-04, TC-SHOW-05,
   TC-SHOW-06, TC-SHOW-07, TC-SHOW-08, TC-PERF-01 step B, TC-SEC-01, TC-SEC-02.
3. **Phase 2, rotation** (B2, copies A to C): TC-SHOW-09, TC-ROT-01, TC-ROT-02, TC-ROT-03, TC-ROT-04, TC-ROT-05.
4. **Phase 3, counting in server mode** (`long-bottom`, B3): TC-COUNT-01, TC-A11Y-01, TC-COUNT-02, TC-COUNT-03.
5. **Phase 4, client mode and tokens** (`cached-top`, `cached-html`, B5): TC-CLIENT-01, TC-CLIENT-03, TC-COUNT-04,
   TC-REP-03 step 1 (starts the 11 minute wait), TC-REP-01, TC-REP-02, TC-CLIENT-02, TC-SEC-03, TC-SEC-07, TC-SEC-08,
   TC-PERF-02, TC-PERF-03, then TC-REP-03 steps 2 to 4 once 11 minutes have passed.
6. **Phase 5, HTML banners and permissions** (`long-html`, H1; three browser profiles): TC-HTML-01, TC-SEC-05,
   TC-SEC-06, TC-PERM-02, TC-PERM-04, TC-PERM-03, TC-HTML-02, TC-HIST-03, TC-HTML-03, TC-SEC-04, TC-SEC-10.
7. **Phase 6, limits** (B4; unpublishes B1 and B2): TC-LIM-04, TC-LIM-01, TC-LIM-02, TC-LIM-03.
8. **Phase 7, reports, history, import, wrap-up**: TC-COUNT-05, TC-SEC-09, TC-COUNT-06, TC-IMPORT-01, TC-IMPORT-02,
   TC-HIST-01, TC-HIST-02, TC-A11Y-02, TC-A11Y-03, TC-SHOW-10.
9. **Phase 8, optional, needs restarts** (empties the in-memory token set; run last): TC-COUNT-07, TC-SEC-11.
