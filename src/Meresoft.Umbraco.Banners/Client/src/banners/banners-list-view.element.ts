import { css, customElement, html, nothing, repeat, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { listBanners, listZones } from "../api/sdk.gen.js";
import type { BannerPageModel, BannerState, BannerType, ZoneResponseModel } from "../api/types.gen.js";
import { configureBannersClient } from "../api/client-setup.js";
import { client, navigate, send } from "../shared/api.js";
import { STATE_DISPLAY, activeFlags } from "./banner-state.js";

const PAGE_SIZE = 20;
const STATES: BannerState[] = ["Draft", "Published", "PublishedWithChanges", "Unpublished"];
const TYPES: BannerType[] = ["Image", "Html"];

/** The banner list with state badges, flags, filters and paging. */
@customElement("meresoft-banners-list-view")
export class MeresoftBannersListViewElement extends UmbLitElement {
  @state() private _page: BannerPageModel = { total: 0, items: [] };
  @state() private _zones: ZoneResponseModel[] = [];
  @state() private _zoneFilter = "";
  @state() private _stateFilter = "";
  @state() private _typeFilter = "";
  @state() private _pageIndex = 0;
  @state() private _loading = true;
  @state() private _failed = false;

  constructor() {
    super();
    configureBannersClient(this, client, () => void this.#loadAll());
  }

  async #loadAll() {
    const zones = await send<ZoneResponseModel[]>(listZones());
    this._zones = zones.data ?? [];
    await this.#load();
  }

  async #load() {
    this._loading = true;
    const result = await send<BannerPageModel>(
      listBanners({
        query: {
          zone: this._zoneFilter || undefined,
          state: (this._stateFilter || undefined) as BannerState | undefined,
          type: (this._typeFilter || undefined) as BannerType | undefined,
          skip: this._pageIndex * PAGE_SIZE,
          take: PAGE_SIZE,
        },
      }),
    );
    this._failed = result.data === undefined;
    this._page = result.data ?? { total: 0, items: [] };
    this._loading = false;
  }

  #filter(apply: () => void) {
    apply();
    this._pageIndex = 0;
    void this.#load();
  }

  #zoneName(key: string) {
    return this._zones.find((z) => z.key === key)?.name ?? key;
  }

  override render() {
    return html`
      <umb-body-layout headline=${this.localize.term("meresoftBanners_banners")}>
        <div slot="action-menu" class="actions">
          <uui-button
            look="primary"
            color="positive"
            label=${this.localize.term("meresoftBanners_createBanner")}
            @click=${() => navigate("section/meresoft-banners/banners/create")}></uui-button>
        </div>
        <div class="filters">
          ${this.#select("meresoftBanners_filterZone", this._zoneFilter, this._zones.map((z) => [z.key, z.name]), (v) => (this._zoneFilter = v))}
          ${this.#select(
            "meresoftBanners_filterState",
            this._stateFilter,
            STATES.map((s) => [s, this.localize.term(STATE_DISPLAY[s].key)]),
            (v) => (this._stateFilter = v),
          )}
          ${this.#select(
            "meresoftBanners_filterType",
            this._typeFilter,
            TYPES.map((t) => [t, this.localize.term(`meresoftBanners_bannerType${t}`)]),
            (v) => (this._typeFilter = v),
          )}
        </div>
        ${this.#renderBody()}
      </umb-body-layout>
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
        @change=${(e: Event) => this.#filter(() => set((e.target as HTMLInputElement).value))}></uui-select>
    `;
  }

  #renderBody() {
    if (this._loading) {
      return html`<uui-loader></uui-loader>`;
    }
    if (this._failed) {
      return html`<uui-alert color="danger">
        ${this.localize.term("meresoftBanners_loadFailed")}
        <uui-button look="secondary" label=${this.localize.term("meresoftBanners_reload")} @click=${() => this.#load()}></uui-button>
      </uui-alert>`;
    }
    if (this._page.items.length === 0) {
      return html`<uui-box><p>${this.localize.term("meresoftBanners_noBanners")}</p></uui-box>`;
    }

    const pages = Math.ceil(this._page.total / PAGE_SIZE);
    return html`
      <uui-box>
        <uui-table aria-label=${this.localize.term("meresoftBanners_banners")}>
          <uui-table-head>
            <uui-table-head-cell>${this.localize.term("meresoftBanners_name")}</uui-table-head-cell>
            <uui-table-head-cell>${this.localize.term("meresoftBanners_bannerType")}</uui-table-head-cell>
            <uui-table-head-cell>${this.localize.term("meresoftBanners_bannerState")}</uui-table-head-cell>
            <uui-table-head-cell>${this.localize.term("meresoftBanners_zones")}</uui-table-head-cell>
            <uui-table-head-cell>${this.localize.term("meresoftBanners_bannerUpdated")}</uui-table-head-cell>
          </uui-table-head>
          ${repeat(
            this._page.items,
            (b) => b.key,
            (b) => html`
              <uui-table-row>
                <uui-table-cell><a href="section/meresoft-banners/banners/edit/${b.key}">${b.name}</a></uui-table-cell>
                <uui-table-cell>${this.localize.term(`meresoftBanners_bannerType${b.type}`)}</uui-table-cell>
                <uui-table-cell>
                  <uui-tag look="primary" color=${STATE_DISPLAY[b.state].color}>${this.localize.term(STATE_DISPLAY[b.state].key)}</uui-tag>
                  ${activeFlags(b.flags).map((f) => html`<uui-tag look="outline" color=${f.color}>${this.localize.term(f.key)}</uui-tag>`)}
                </uui-table-cell>
                <uui-table-cell>${b.zoneKeys.map((k) => this.#zoneName(k)).join(", ")}</uui-table-cell>
                <uui-table-cell>${this.localize.date(b.updateDate)}</uui-table-cell>
              </uui-table-row>
            `,
          )}
        </uui-table>
        ${pages > 1
          ? html`<uui-pagination
              .total=${pages}
              .current=${this._pageIndex + 1}
              @change=${(e: Event) => {
                this._pageIndex = (e.target as HTMLElement & { current: number }).current - 1;
                void this.#load();
              }}></uui-pagination>`
          : nothing}
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
        gap: var(--uui-size-space-4);
        margin-bottom: var(--uui-size-layout-1);
      }
      .filters uui-select {
        min-width: 14rem;
      }
      uui-tag + uui-tag {
        margin-left: var(--uui-size-space-2);
      }
      uui-pagination {
        margin-top: var(--uui-size-layout-1);
      }
      a {
        color: var(--uui-color-interactive);
        font-weight: bold;
      }
    `,
  ];
}

export default MeresoftBannersListViewElement;

declare global {
  interface HTMLElementTagNameMap {
    "meresoft-banners-list-view": MeresoftBannersListViewElement;
  }
}
