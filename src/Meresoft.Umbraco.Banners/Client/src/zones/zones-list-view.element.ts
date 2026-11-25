import { css, customElement, html, nothing, repeat, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import {
  listRequestedZones,
  listZones,
  dismissRequestedZone,
} from "../api/sdk.gen.js";
import type { RequestedZoneResponseModel, ZoneResponseModel } from "../api/types.gen.js";
import { configureBannersClient } from "../api/client-setup.js";
import { client, navigate, send } from "../shared/api.js";

/** The zones table, and the zones that templates ask for but that do not exist yet. */
@customElement("meresoft-zones-list-view")
export class MeresoftZonesListViewElement extends UmbLitElement {
  @state() private _zones: ZoneResponseModel[] = [];
  @state() private _requested: RequestedZoneResponseModel[] = [];
  @state() private _loading = true;
  @state() private _failed = false;

  constructor() {
    super();
    configureBannersClient(this, client, () => void this.#load());
  }

  async #load() {
    this._loading = true;
    const [zones, requested] = await Promise.all([
      send<ZoneResponseModel[]>(listZones()),
      send<RequestedZoneResponseModel[]>(listRequestedZones()),
    ]);
    this._failed = zones.data === undefined || requested.data === undefined;
    this._zones = zones.data ?? [];
    this._requested = requested.data ?? [];
    this._loading = false;
  }

  async #dismiss(alias: string) {
    await send(dismissRequestedZone({ path: { alias } }));
    await this.#load();
  }

  #create(alias?: string) {
    navigate(`section/meresoft-banners/zones/create${alias ? `?alias=${encodeURIComponent(alias)}` : ""}`);
  }

  override render() {
    return html`
      <umb-body-layout headline=${this.localize.term("meresoftBanners_zones")}>
        <div slot="action-menu" class="actions">
          <uui-button
            look="primary"
            color="positive"
            label=${this.localize.term("meresoftBanners_createZone")}
            @click=${() => this.#create()}></uui-button>
        </div>
        ${this.#renderBody()}
      </umb-body-layout>
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
    return html`${this.#renderRequested()} ${this.#renderZones()}`;
  }

  #renderRequested() {
    if (this._requested.length === 0) {
      return nothing;
    }
    return html`
      <uui-box class="requested" headline=${this.localize.term("meresoftBanners_placedNotSetUp")}>
        <p>${this.localize.term("meresoftBanners_placedNotSetUpHint")}</p>
        <ul>
          ${repeat(
            this._requested,
            (r) => r.alias,
            (r) => html`
              <li>
                <code>${r.alias}</code>
                <span class="seen">${this.localize.term("meresoftBanners_lastSeen")}: ${this.localize.date(r.lastSeen)}</span>
                <uui-button
                  look="primary"
                  compact
                  label=${this.localize.term("meresoftBanners_createZoneFor", r.alias)}
                  @click=${() => this.#create(r.alias)}></uui-button>
                <uui-button
                  look="secondary"
                  compact
                  label=${this.localize.term("meresoftBanners_dismissFor", r.alias)}
                  @click=${() => this.#dismiss(r.alias)}>
                  ${this.localize.term("meresoftBanners_dismiss")}
                </uui-button>
              </li>
            `,
          )}
        </ul>
      </uui-box>
    `;
  }

  #renderZones() {
    if (this._zones.length === 0) {
      return html`<uui-box><p>${this.localize.term("meresoftBanners_noZones")}</p></uui-box>`;
    }
    return html`
      <uui-box>
        <uui-table aria-label=${this.localize.term("meresoftBanners_zones")}>
          <uui-table-head>
            <uui-table-head-cell>${this.localize.term("meresoftBanners_name")}</uui-table-head-cell>
            <uui-table-head-cell>${this.localize.term("meresoftBanners_zoneAlias")}</uui-table-head-cell>
            <uui-table-head-cell>${this.localize.term("meresoftBanners_zoneShowCount")}</uui-table-head-cell>
            <uui-table-head-cell>${this.localize.term("meresoftBanners_zoneActiveBanners")}</uui-table-head-cell>
          </uui-table-head>
          ${repeat(
            this._zones,
            (z) => z.key,
            (z) => html`
              <uui-table-row>
                <uui-table-cell>
                  <a href="section/meresoft-banners/zones/edit/${z.key}">${z.name}</a>
                </uui-table-cell>
                <uui-table-cell><code>${z.alias}</code></uui-table-cell>
                <uui-table-cell>${z.showCount}</uui-table-cell>
                <uui-table-cell>${z.activeBannerCount}</uui-table-cell>
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
      uui-box + uui-box {
        margin-top: var(--uui-size-layout-1);
      }
      .requested {
        border-left: var(--uui-size-1) solid var(--uui-color-warning);
      }
      .requested ul {
        list-style: none;
        margin: 0;
        padding: 0;
      }
      .requested li {
        display: flex;
        align-items: center;
        flex-wrap: wrap;
        gap: var(--uui-size-space-4);
        padding: var(--uui-size-space-3) 0;
      }
      .seen {
        color: var(--uui-color-text-alt);
        margin-right: auto;
      }
      a {
        color: var(--uui-color-interactive);
        font-weight: bold;
      }
    `,
  ];
}

export default MeresoftZonesListViewElement;

declare global {
  interface HTMLElementTagNameMap {
    "meresoft-zones-list-view": MeresoftZonesListViewElement;
  }
}
