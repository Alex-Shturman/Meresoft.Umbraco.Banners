import { css, customElement, html, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import type { UmbRoute } from "@umbraco-cms/backoffice/router";
import type { MeresoftBannerEditorElement } from "./banner-editor.element.js";

/** Hosts the banner views: the list, the create form and the edit form. */
@customElement("meresoft-banners-section")
export class MeresoftBannersSectionElement extends UmbLitElement {
  @state()
  private _routes: UmbRoute[] = [
    {
      path: "create",
      component: () => import("./banner-editor.element.js"),
      setup: (component) => {
        (component as MeresoftBannerEditorElement).bannerKey = undefined;
      },
    },
    {
      path: "edit/:key",
      component: () => import("./banner-editor.element.js"),
      setup: (component, info) => {
        (component as MeresoftBannerEditorElement).bannerKey = info.match.params.key;
      },
    },
    { path: "", pathMatch: "full", component: () => import("./banners-list-view.element.js") },
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

export default MeresoftBannersSectionElement;

declare global {
  interface HTMLElementTagNameMap {
    "meresoft-banners-section": MeresoftBannersSectionElement;
  }
}
