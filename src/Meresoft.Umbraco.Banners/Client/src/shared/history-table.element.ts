import { css, customElement, html, nothing, property } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { formatDateTime } from "./date-time.js";
import { HistoryPager, type HistoryLoader } from "./history-pager.js";

export type { HistoryLoader } from "./history-pager.js";

/** Colour of the tag of each audit action; an action this view does not know shows as a plain tag. */
const ACTION_COLOR: Record<string, "default" | "positive" | "warning" | "danger"> = {
  new: "default",
  save: "default",
  copy: "default",
  publish: "positive",
  unpublish: "warning",
  delete: "danger",
};

/**
 * The changes of one banner or zone, newest first: when, who and what. Shared by the banner and the zone history
 * views, which only differ in the endpoint they call. The paging state lives in `HistoryPager`.
 */
@customElement("meresoft-history-table")
export class MeresoftHistoryTableElement extends UmbLitElement {
  /** Loads one page of the history. Setting a new loader reloads the list. */
  @property({ attribute: false })
  set load(value: HistoryLoader | undefined) {
    this.#pager = value ? new HistoryPager(value, 20, undefined, () => this.requestUpdate()) : undefined;
    void this.#pager?.reload();
    this.requestUpdate();
  }

  #pager?: HistoryPager;

  #actionLabel(action: string): string {
    const key = `meresoftBanners_history_${action}`;
    const text = this.localize.term(key);
    return text === key || text.startsWith("[") ? action : text;
  }

  override render() {
    const pager = this.#pager;
    if (!pager) {
      return nothing;
    }
    if (pager.error === "first") {
      return html`<uui-alert color="danger">
        ${this.localize.term("meresoftBanners_loadFailed")}
        <uui-button look="secondary" label=${this.localize.term("meresoftBanners_reload")} @click=${() => pager.reload()}></uui-button>
      </uui-alert>`;
    }
    if (pager.loading && pager.entries.length === 0) {
      return html`<uui-loader></uui-loader>`;
    }
    if (pager.entries.length === 0) {
      return html`<p class="empty">${this.localize.term("meresoftBanners_historyEmpty")}</p>`;
    }

    return html`
      <uui-table aria-label=${this.localize.term("meresoftBanners_history")}>
        <uui-table-head>
          <uui-table-head-cell>${this.localize.term("meresoftBanners_historyWhen")}</uui-table-head-cell>
          <uui-table-head-cell>${this.localize.term("meresoftBanners_historyWho")}</uui-table-head-cell>
          <uui-table-head-cell>${this.localize.term("meresoftBanners_historyWhat")}</uui-table-head-cell>
        </uui-table-head>
        ${pager.entries.map(
          (entry) => html`
            <uui-table-row>
              <uui-table-cell class="when">${formatDateTime(entry.date)}</uui-table-cell>
              <uui-table-cell>${entry.userName ?? this.localize.term("meresoftBanners_historyUnknownUser")}</uui-table-cell>
              <uui-table-cell>
                <uui-tag look="secondary" color=${ACTION_COLOR[entry.action] ?? "default"}>${this.#actionLabel(entry.action)}</uui-tag>
              </uui-table-cell>
            </uui-table-row>
          `,
        )}
      </uui-table>
      ${pager.error === "more"
        ? html`<uui-alert class="more-error" color="danger" role="alert">${this.localize.term("meresoftBanners_historyMoreFailed")}</uui-alert>`
        : nothing}
      ${pager.canLoadMore
        ? html`<uui-button
            class="more"
            look="secondary"
            label=${this.localize.term(pager.error === "more" ? "meresoftBanners_historyRetry" : "meresoftBanners_historyMore")}
            ?disabled=${pager.loading}
            @click=${() => pager.more()}></uui-button>`
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
      .more,
      .more-error {
        margin-top: var(--uui-size-layout-1);
      }
      .more-error {
        display: block;
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
