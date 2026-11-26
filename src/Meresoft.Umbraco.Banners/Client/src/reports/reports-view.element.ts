import { css, customElement, html, nothing, repeat, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { getReport, getReportCsv, listBanners, listZones } from "../api/sdk.gen.js";
import type { BannerPageModel, ReportResponseModel, ZoneResponseModel } from "../api/types.gen.js";
import { configureBannersClient } from "../api/client-setup.js";
import { client, send } from "../shared/api.js";
import { loadAllPages } from "../shared/paging.js";
import "./daily-chart.element.js";
import { presetRange, type Preset } from "./presets.js";

const PRESETS: Preset[] = ["today", "last7", "last30", "thisMonth", "lastMonth", "custom"];

/** Impressions, clicks and click through rate for a period, with a daily chart and a CSV download. */
@customElement("meresoft-reports-view")
export class MeresoftReportsViewElement extends UmbLitElement {
  @state() private _preset: Preset = "last7";
  @state() private _from = presetRange("last7").from;
  @state() private _to = presetRange("last7").to;
  @state() private _zone = "";
  @state() private _banner = "";
  @state() private _zones: ZoneResponseModel[] = [];
  @state() private _banners: Array<{ key: string; name: string }> = [];
  @state() private _report?: ReportResponseModel;
  @state() private _loading = true;
  @state() private _failed = false;
  @state() private _periodError?: string;
  @state() private _csvFailed = false;
  #requestCount = 0;

  constructor() {
    super();
    configureBannersClient(this, client, () => void this.#start());
  }

  async #start() {
    const [zones, banners] = await Promise.all([
      send<ZoneResponseModel[]>(listZones()),
      // Every banner, not only the first page, so the filter offers them all.
      loadAllPages(async (skip, take) => (await send<BannerPageModel>(listBanners({ query: { skip, take } }))).data),
    ]);
    this._zones = zones.data ?? [];
    this._banners = (banners ?? []).map((b) => ({ key: b.key, name: b.name }));
    await this.#load();
  }

  #query() {
    return { from: this._from, to: this._to, zone: this._zone || undefined, banner: this._banner || undefined };
  }

  async #load() {
    const request = ++this.#requestCount;
    this._loading = true;
    this._periodError = undefined;
    const result = await send<ReportResponseModel>(getReport({ query: this.#query() }));
    if (request !== this.#requestCount) {
      return; // a newer choice was made meanwhile; its answer is the one to show
    }
    if (result.status === 400) {
      this._failed = false;
      this._periodError = this.localize.term("meresoftBanners_reportPeriodInvalid");
    } else {
      this._failed = result.data === undefined;
      this._report = result.data ?? this._report;
    }
    this._loading = false;
  }

  #pick(preset: Preset) {
    this._preset = preset;
    if (preset !== "custom") {
      ({ from: this._from, to: this._to } = presetRange(preset));
      void this.#load();
    }
  }

  #setDate(which: "from" | "to", value: string) {
    if (!value) {
      return;
    }
    if (which === "from") {
      this._from = value;
    } else {
      this._to = value;
    }
    this._preset = "custom";
    void this.#load();
  }

  async #downloadCsv() {
    this._csvFailed = false;
    const result = await send<Blob>(getReportCsv({ query: this.#query(), parseAs: "blob" }));
    if (!(result.data instanceof Blob)) {
      this._csvFailed = true;
      return;
    }
    const url = URL.createObjectURL(result.data);
    const link = document.createElement("a");
    link.href = url;
    link.download = `banners-${this._from}-${this._to}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

  #ctr(value: number | null | undefined) {
    return value === null || value === undefined ? "–" : `${value.toFixed(1)} %`;
  }

  override render() {
    return html`
      <umb-body-layout headline=${this.localize.term("meresoftBanners_reports")}>
        <div slot="action-menu" class="actions">
          <uui-button
            look="secondary"
            label=${this.localize.term("meresoftBanners_downloadCsv")}
            @click=${() => this.#downloadCsv()}></uui-button>
        </div>
        ${this.#renderFilters()} ${this.#renderBody()}
        <p class="notes">
          ${this.localize.term("meresoftBanners_reportNoteUtc")}<br />
          ${this.localize.term("meresoftBanners_reportNoteDelay")}
        </p>
      </umb-body-layout>
    `;
  }

  #renderFilters() {
    return html`
      <div class="filters">
        <uui-button-group role="group" aria-label=${this.localize.term("meresoftBanners_reportPeriod")}>
          ${PRESETS.map(
            (preset) => html`
              <uui-button
                look=${this._preset === preset ? "primary" : "secondary"}
                aria-pressed=${this._preset === preset ? "true" : "false"}
                label=${this.localize.term(`meresoftBanners_preset_${preset}`)}
                @click=${() => this.#pick(preset)}></uui-button>
            `,
          )}
        </uui-button-group>
        <label>
          ${this.localize.term("meresoftBanners_reportFrom")}
          <uui-input type="date" .value=${this._from} label=${this.localize.term("meresoftBanners_reportFrom")} @change=${(e: Event) => this.#setDate("from", (e.target as HTMLInputElement).value)}></uui-input>
        </label>
        <label>
          ${this.localize.term("meresoftBanners_reportTo")}
          <uui-input type="date" .value=${this._to} label=${this.localize.term("meresoftBanners_reportTo")} @change=${(e: Event) => this.#setDate("to", (e.target as HTMLInputElement).value)}></uui-input>
        </label>
        ${this.#select("meresoftBanners_filterZone", this._zone, this._zones.map((z) => [z.key, z.name]), (v) => (this._zone = v))}
        ${this.#select("meresoftBanners_filterBanner", this._banner, this._banners.map((b) => [b.key, b.name]), (v) => (this._banner = v))}
      </div>
      ${this._periodError ? html`<uui-alert color="warning" role="alert">${this._periodError}</uui-alert>` : nothing}
      ${this._csvFailed ? html`<uui-alert color="danger" role="alert">${this.localize.term("meresoftBanners_csvFailed")}</uui-alert>` : nothing}
    `;
  }

  #select(labelKey: string, value: string, options: string[][], set: (value: string) => void) {
    const label = this.localize.term(labelKey);
    return html`
      <uui-select
        label=${label}
        .options=${[
          { name: `${label}: ${this.localize.term("meresoftBanners_all")}`, value: "", selected: value === "" },
          ...options.map(([v, name]) => ({ name, value: v, selected: v === value })),
        ]}
        @change=${(e: Event) => {
          set((e.target as HTMLInputElement).value);
          void this.#load();
        }}></uui-select>
    `;
  }

  #renderBody() {
    if (this._failed) {
      return html`<uui-alert color="danger">
        ${this.localize.term("meresoftBanners_loadFailed")}
        <uui-button look="secondary" label=${this.localize.term("meresoftBanners_reload")} @click=${() => this.#load()}></uui-button>
      </uui-alert>`;
    }
    const report = this._report;
    if (!report) {
      return html`<uui-loader></uui-loader>`;
    }
    return html`
      <div class="results" aria-busy=${this._loading ? "true" : "false"}>
        <uui-box headline=${this.localize.term("meresoftBanners_totals")}>
          <dl class="totals">
            <div>
              <dt>${this.localize.term("meresoftBanners_impressions")}</dt>
              <dd data-total="impressions">${report.totals.impressions.toLocaleString()}</dd>
            </div>
            <div>
              <dt>${this.localize.term("meresoftBanners_clicks")}</dt>
              <dd data-total="clicks">${report.totals.clicks.toLocaleString()}</dd>
            </div>
            <div>
              <dt>${this.localize.term("meresoftBanners_ctr")}</dt>
              <dd data-total="ctr">${this.#ctr(report.totals.ctr)}</dd>
            </div>
          </dl>
        </uui-box>
        <uui-box headline=${this.localize.term("meresoftBanners_perDay")}>
          <meresoft-daily-chart .days=${report.daily}></meresoft-daily-chart>
          ${this.#renderDailyTable(report)}
        </uui-box>
        ${this.#renderRows(report)}
      </div>
    `;
  }

  /** The text alternative of the chart: every day as a table. */
  #renderDailyTable(report: ReportResponseModel) {
    return html`
      <details class="daily-table">
        <summary>${this.localize.term("meresoftBanners_showDailyValues")}</summary>
        <uui-table aria-label=${this.localize.term("meresoftBanners_perDay")}>
          <uui-table-head>
            <uui-table-head-cell>${this.localize.term("meresoftBanners_day")}</uui-table-head-cell>
            <uui-table-head-cell>${this.localize.term("meresoftBanners_impressions")}</uui-table-head-cell>
            <uui-table-head-cell>${this.localize.term("meresoftBanners_clicks")}</uui-table-head-cell>
            <uui-table-head-cell>${this.localize.term("meresoftBanners_ctr")}</uui-table-head-cell>
          </uui-table-head>
          ${repeat(
            report.daily,
            (d) => d.day,
            (d) => html`
              <uui-table-row>
                <uui-table-cell>
                  ${d.day}${d.imported ? html` <uui-tag look="secondary" compact>${this.localize.term("meresoftBanners_imported")}</uui-tag>` : nothing}
                </uui-table-cell>
                <uui-table-cell>${d.impressions.toLocaleString()}</uui-table-cell>
                <uui-table-cell>${d.clicks.toLocaleString()}</uui-table-cell>
                <uui-table-cell>${this.#ctr(d.impressions === 0 ? null : Math.round((d.clicks * 1000) / d.impressions) / 10)}</uui-table-cell>
              </uui-table-row>
            `,
          )}
        </uui-table>
      </details>
    `;
  }

  #renderRows(report: ReportResponseModel) {
    if (report.rows.length === 0) {
      return html`<uui-box><p>${this.localize.term("meresoftBanners_reportEmpty")}</p></uui-box>`;
    }
    return html`
      <uui-box headline=${this.localize.term("meresoftBanners_byBanner")}>
        <uui-table aria-label=${this.localize.term("meresoftBanners_byBanner")}>
          <uui-table-head>
            <uui-table-head-cell>${this.localize.term("meresoftBanners_banner")}</uui-table-head-cell>
            <uui-table-head-cell>${this.localize.term("meresoftBanners_zone")}</uui-table-head-cell>
            <uui-table-head-cell class="num">${this.localize.term("meresoftBanners_impressions")}</uui-table-head-cell>
            <uui-table-head-cell class="num">${this.localize.term("meresoftBanners_clicks")}</uui-table-head-cell>
            <uui-table-head-cell class="num">${this.localize.term("meresoftBanners_ctr")}</uui-table-head-cell>
          </uui-table-head>
          ${repeat(
            report.rows,
            // Deleted banners have no key and share a name, so the position keeps their keys apart.
            (r, index) => `${r.bannerKey ?? `deleted-${index}`}/${r.zoneAlias}`,
            (r) => html`
              <uui-table-row>
                <uui-table-cell>
                  ${r.bannerKey ? html`<a href="section/meresoft-banners/banners/edit/${r.bannerKey}">${r.bannerName}</a>` : html`<em>${r.bannerName}</em>`}
                </uui-table-cell>
                <uui-table-cell><code>${r.zoneAlias}</code></uui-table-cell>
                <uui-table-cell class="num">${r.impressions.toLocaleString()}</uui-table-cell>
                <uui-table-cell class="num">${r.clicks.toLocaleString()}</uui-table-cell>
                <uui-table-cell class="num">${this.#ctr(r.ctr)}</uui-table-cell>
              </uui-table-row>
            `,
          )}
        </uui-table>
      </uui-box>
    `;
  }

  static override styles = [
    UmbTextStyles,
    css`
      :host {
        display: block;
        height: 100%;
      }
      .actions {
        padding-right: var(--uui-size-layout-1);
      }
      .filters {
        display: flex;
        flex-wrap: wrap;
        align-items: flex-end;
        gap: var(--uui-size-space-4) var(--uui-size-space-5);
        margin-bottom: var(--uui-size-layout-1);
      }
      .filters label {
        display: grid;
        gap: var(--uui-size-space-1);
        color: var(--uui-color-text-alt);
      }
      uui-alert {
        margin-bottom: var(--uui-size-layout-1);
      }
      .results {
        display: grid;
        gap: var(--uui-size-layout-1);
        transition: opacity 120ms;
      }
      .results[aria-busy="true"] {
        opacity: 0.55;
      }
      .totals {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr));
        gap: var(--uui-size-space-5);
        margin: 0;
      }
      .totals dt {
        color: var(--uui-color-text-alt);
      }
      .totals dd {
        margin: 0;
        font-size: var(--uui-size-8, 2rem);
        font-weight: 700;
        font-variant-numeric: tabular-nums;
        line-height: 1.2;
      }
      .daily-table {
        margin-top: var(--uui-size-space-5);
      }
      .daily-table summary {
        cursor: pointer;
        color: var(--uui-color-interactive);
        margin-bottom: var(--uui-size-space-3);
      }
      .num {
        text-align: right;
        font-variant-numeric: tabular-nums;
      }
      a {
        color: var(--uui-color-interactive);
        font-weight: bold;
      }
      .notes {
        margin: var(--uui-size-layout-1) 0 0;
        color: var(--uui-color-text-alt);
        max-width: 60ch;
      }
      @media (prefers-reduced-motion: reduce) {
        .results {
          transition: none;
        }
      }
    `,
  ];
}

export default MeresoftReportsViewElement;

declare global {
  interface HTMLElementTagNameMap {
    "meresoft-reports-view": MeresoftReportsViewElement;
  }
}
