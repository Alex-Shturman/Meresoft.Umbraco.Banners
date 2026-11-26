import { customElement, html, nothing, property } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { getBannerHistory } from "../api/sdk.gen.js";
import type { HistoryPageModel } from "../api/types.gen.js";
import { send } from "../shared/api.js";
import "../shared/history-table.element.js";

/** The history view of a banner: who created, saved, published, unpublished or copied it, and when (FR-018). */
@customElement("meresoft-banner-history")
export class MeresoftBannerHistoryElement extends UmbLitElement {
  /** Key of the banner. */
  @property({ attribute: false })
  bannerKey?: string;

  override render() {
    const key = this.bannerKey;
    if (!key) {
      return nothing;
    }
    return html`<meresoft-history-table
      .load=${(skip: number, take: number) =>
        send<HistoryPageModel>(getBannerHistory({ path: { key }, query: { skip, take } }))}></meresoft-history-table>`;
  }
}

export default MeresoftBannerHistoryElement;

declare global {
  interface HTMLElementTagNameMap {
    "meresoft-banner-history": MeresoftBannerHistoryElement;
  }
}
