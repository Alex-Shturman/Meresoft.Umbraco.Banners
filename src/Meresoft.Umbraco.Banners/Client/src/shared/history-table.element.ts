import { css, customElement, html, nothing, property, repeat, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type { HistoryEntryModel, HistoryPageModel } from "../api/types.gen.js";
import type { ApiResult } from "./api.js";

const PAGE_SIZE = 20;

/** Colour of the tag of each audit action; an action this view does not know shows as a plain tag. */
const ACTION_COLOR: Record<string, "default" | "positive" | "warning" | "danger"> = {
  new: "default",
  save: "default",
  copy: "default",
  publish: "positive",
  unpublish: "warning",
  delete: "danger",
};

/** A page loader: the banner and the zone history views each pass their own endpoint. */
export type HistoryLoader = (skip: number, take: number) => Promise<ApiResult<HistoryPageModel>>;

/**
 * The changes of one banner or zone, newest first: when, who and what. Shared by the banner and the zone history
 * views, which only differ in the endpoint they call.
 */
@customElement("meresoft-history-table")
export class MeresoftHistoryTableElement extends UmbLitElement {
  /** Loads one page of the history. Setting a new loader reloads the list. */
  @property({ attribute: false })
  set load(value: HistoryLoader | undefined) {
    this.#load = value;
    void this.#reload();
  }
  get load(): HistoryLoader | undefined {
    return this.#load;
  }

  @state() private _entries: HistoryEntryModel[] = [];
  @state() private _total = 0;
  @state() private _loading = true;
  @state() private _failed = false;

  #load?: HistoryLoader;
  #request = 0;

  async #reload() {
    this._entries = [];
    this._total = 0;
    await this.#more();
  }

  async #more() {
    if (!this.#load) {
      return;
    }
    this._loading = true;
    this._failed = false;
    const request = ++this.#request;
    const result = await this.#load(this._entries.length, PAGE_SIZE);
    if (request !== this.#request) {
      return; // A newer load started meanwhile; its result replaces this one.
    }
    if (result.data) {
      this._entries = [...this._entries, ...result.data.items];
      this._total = result.data.total;
    } else {
      this._failed = true;
    }
    this._loading = false;
  }

  #actionLabel(action: string): string {
    const key = `meresoftBanners_history_${action}`;
    const text = this.localize.term(key);
    return text === key || text.startsWith("[") ? action : text;
  }

  override render() {
    if (this._failed) {
      return html`<uui-alert color="danger">
        ${this.localize.term("meresoftBanners_loadFailed")}
        <uui-button look="secondary" label=${this.localize.term("meresoftBanners_reload")} @click=${() => this.#reload()}></uui-button>
      </uui-alert>`;
    }
    if (this._loading && this._entries.length === 0) {
      return html`<uui-loader></uui-loader>`;
    }
    if (this._entries.length === 0) {
      return html`<p class="empty">${this.localize.term("meresoftBanners_historyEmpty")}</p>`;
    }

    return html`
      <uui-table aria-label=${this.localize.term("meresoftBanners_history")}>
        <uui-table-head>
          <uui-table-head-cell>${this.localize.term("meresoftBanners_historyWhen")}</uui-table-head-cell>
          <uui-table-head-cell>${this.localize.term("meresoftBanners_historyWho")}</uui-table-head-cell>
          <uui-table-head-cell>${this.localize.term("meresoftBanners_historyWhat")}</uui-table-head-cell>
        </uui-table-head>
        ${repeat(
          this._entries,
          (_, index) => index,
          (entry) => html`
            <uui-table-row>
              <uui-table-cell class="when">${new Date(entry.date).toLocaleString()}</uui-table-cell>
              <uui-table-cell>${entry.userName ?? this.localize.term("meresoftBanners_historyUnknownUser")}</uui-table-cell>
              <uui-table-cell>
                <uui-tag look="secondary" color=${ACTION_COLOR[entry.action] ?? "default"}>${this.#actionLabel(entry.action)}</uui-tag>
              </uui-table-cell>
            </uui-table-row>
          `,
        )}
      </uui-table>
      ${this._entries.length < this._total
        ? html`<uui-button
            class="more"
            look="secondary"
            label=${this.localize.term("meresoftBanners_historyMore")}
            ?disabled=${this._loading}
            @click=${() => this.#more()}></uui-button>`
        : nothing}
    `;
  }

  static override styles = [
    UmbTextStyles,
    css`
      :host {
        display: block;
      }
      .when {
        white-space: nowrap;
        font-variant-numeric: tabular-nums;
      }
      .empty {
        color: var(--uui-color-text-alt);
      }
      .more {
        margin-top: var(--uui-size-layout-1);
      }
    `,
  ];
}

export default MeresoftHistoryTableElement;

declare global {
  interface HTMLElementTagNameMap {
    "meresoft-history-table": MeresoftHistoryTableElement;
  }
}
