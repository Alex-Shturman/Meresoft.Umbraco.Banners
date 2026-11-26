import { customElement, html, nothing, property } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { getZoneHistory } from "../api/sdk.gen.js";
import type { HistoryPageModel } from "../api/types.gen.js";
import { send } from "../shared/api.js";
import "../shared/history-table.element.js";

/** The history view of a zone: who created, saved or deleted it, and when (FR-018). */
@customElement("meresoft-zone-history")
export class MeresoftZoneHistoryElement extends UmbLitElement {
  /** Key of the zone. */
  @property({ attribute: false })
  zoneKey?: string;

  override render() {
    const key = this.zoneKey;
    if (!key) {
      return nothing;
    }
    return html`<meresoft-history-table
      .load=${(skip: number, take: number) =>
        send<HistoryPageModel>(getZoneHistory({ path: { key }, query: { skip, take } }))}></meresoft-history-table>`;
  }
}

export default MeresoftZoneHistoryElement;

declare global {
  interface HTMLElementTagNameMap {
    "meresoft-zone-history": MeresoftZoneHistoryElement;
  }
}
