import { css, customElement, html, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import type { UmbRoute } from "@umbraco-cms/backoffice/router";
import type { MeresoftZoneEditorElement } from "./zone-editor.element.js";

/** Hosts the zone views: the list, the create form and the edit form. */
@customElement("meresoft-zones-section")
export class MeresoftZonesSectionElement extends UmbLitElement {
  @state()
  private _routes: UmbRoute[] = [
    {
      path: "create",
      component: () => import("./zone-editor.element.js"),
      setup: (component) => {
        const editor = component as MeresoftZoneEditorElement;
        editor.zoneKey = undefined;
        editor.initialAlias = new URLSearchParams(window.location.search).get("alias") ?? undefined;
      },
    },
    {
      path: "edit/:key",
      component: () => import("./zone-editor.element.js"),
      setup: (component, info) => {
        const editor = component as MeresoftZoneEditorElement;
        editor.initialAlias = undefined;
        editor.zoneKey = info.match.params.key;
      },
    },
    { path: "", pathMatch: "full", component: () => import("./zones-list-view.element.js") },
  ];

  override render() {
    return html`<umb-router-slot .routes=${this._routes}></umb-router-slot>`;
  }

  static override styles = [
    css`
      :host {
        display: block;
        height: 100%;
      }
    `,
  ];
}

export default MeresoftZonesSectionElement;

declare global {
  interface HTMLElementTagNameMap {
    "meresoft-zones-section": MeresoftZonesSectionElement;
  }
}
