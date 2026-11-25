import { css, customElement, html, nothing, property, repeat, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import "./zone-history.element.js";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { umbConfirmModal } from "@umbraco-cms/backoffice/modal";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import {
  deleteZone,
  listBanners,
  getZone,
  createZone,
  updateZone,
} from "../api/sdk.gen.js";
import type { BannerPageModel, ZoneRenderMode, ZoneRequestModel, ZoneResponseModel } from "../api/types.gen.js";
import { configureBannersClient } from "../api/client-setup.js";
import { client, fieldErrors, keyFromLocation, navigate, send, type ApiProblem } from "../shared/api.js";

const ZONES_PATH = "section/meresoft-banners/zones";

/** Creates, edits and deletes one zone. */
@customElement("meresoft-zone-editor")
export class MeresoftZoneEditorElement extends UmbLitElement {
  /** Key of the zone to edit; unset when creating. */
  @property({ attribute: false })
  set zoneKey(value: string | undefined) {
    const changed = value !== this.#zoneKey;
    this.#zoneKey = value;
    if (changed && this.#ready) {
      void this.#load();
    }
  }
  get zoneKey(): string | undefined {
    return this.#zoneKey;
  }

  /** On create: an alias to start with, from the "placed but not set up" list. */
  @property({ attribute: false })
  initialAlias?: string;

  @state() private _view: "details" | "history" = "details";
  @state() private _loading = true;
  @state() private _missing = false;
  @state() private _alias = "";
  @state() private _name = "";
  @state() private _description = "";
  @state() private _showCount = 1;
  @state() private _imageWidth = "";
  @state() private _imageHeight = "";
  @state() private _renderMode: ZoneRenderMode = "Server";
  @state() private _errors: Record<string, string> = {};
  @state() private _conflict = false;
  @state() private _blockingBanners: Array<{ key: string; name: string }> = [];

  #zoneKey?: string;
  #ready = false;
  #savedAlias = "";
  #updateDate?: string;
  #notifications?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;

  constructor() {
    super();
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (context) => (this.#notifications = context));
    configureBannersClient(this, client, () => {
      this.#ready = true;
      void this.#load();
    });
  }

  async #load() {
    this._loading = true;
    this._missing = false;
    this._conflict = false;
    this._errors = {};
    this._blockingBanners = [];

    if (!this.#zoneKey) {
      this.#apply({ alias: this.initialAlias ?? "", name: "", showCount: 1, renderMode: "Server" });
      this.#updateDate = undefined;
      this._loading = false;
      return;
    }

    const result = await send<ZoneResponseModel>(
      getZone({ path: { key: this.#zoneKey } }),
    );
    if (result.data) {
      this.#apply(result.data);
      this.#updateDate = result.data.updateDate;
    } else {
      this._missing = true;
    }
    this._loading = false;
  }

  #apply(zone: Partial<ZoneResponseModel> & Pick<ZoneResponseModel, "alias" | "name" | "showCount" | "renderMode">) {
    this._alias = zone.alias;
    this.#savedAlias = this.#zoneKey ? zone.alias : "";
    this._name = zone.name;
    this._description = zone.description ?? "";
    this._showCount = zone.showCount;
    this._imageWidth = zone.imageWidth?.toString() ?? "";
    this._imageHeight = zone.imageHeight?.toString() ?? "";
    this._renderMode = zone.renderMode;
  }

  #body(): ZoneRequestModel {
    return {
      alias: this._alias,
      name: this._name,
      description: this._description || null,
      showCount: Number(this._showCount),
      imageWidth: this._imageWidth ? Number(this._imageWidth) : null,
      imageHeight: this._imageHeight ? Number(this._imageHeight) : null,
      renderMode: this._renderMode,
      updateDate: this.#updateDate ?? null,
    };
  }

  #toast(key: string, color: "positive" | "danger" = "positive") {
    this.#notifications?.peek(color, { data: { message: this.localize.term(key) } });
  }

  #fail(status: number, problem: ApiProblem | undefined) {
    this._errors = status === 400 ? fieldErrors(problem) : {};
    this._conflict = problem?.type === "changed-by-another-user";
    if (status === 404) {
      this._missing = true;
    }
  }

  async #save() {
    this._errors = {};
    this._conflict = false;

    if (!this.#zoneKey) {
      const created = await send(createZone({ body: this.#body() }));
      if (created.status === 201) {
        this.#toast("meresoftBanners_zoneSaved");
        navigate(`${ZONES_PATH}/edit/${keyFromLocation(created.location)}`);
        return;
      }
      this.#fail(created.status, created.problem);
      if (created.problem?.type === "alias-taken") {
        this._errors = { alias: created.problem.detail ?? "" };
      }
      return;
    }

    const updated = await send<ZoneResponseModel>(
      updateZone({ path: { key: this.#zoneKey }, body: this.#body() }),
    );
    if (updated.data) {
      this.#apply(updated.data);
      this.#updateDate = updated.data.updateDate;
      this.#toast("meresoftBanners_zoneSaved");
      return;
    }
    this.#fail(updated.status, updated.problem);
    if (updated.problem?.type === "alias-taken") {
      this._errors = { alias: updated.problem.detail ?? "" };
    }
  }

  async #delete() {
    if (!this.#zoneKey) {
      return;
    }
    try {
      await umbConfirmModal(this, {
        headline: this.localize.term("meresoftBanners_confirmDeleteHeadline", this._name),
        content: this.localize.term("meresoftBanners_confirmDeleteContent"),
        color: "danger",
        confirmLabel: this.localize.term("meresoftBanners_delete"),
      });
    } catch {
      return; // The editor cancelled the dialog.
    }

    const result = await send(deleteZone({ path: { key: this.#zoneKey } }));
    if (result.status === 204) {
      this.#toast("meresoftBanners_zoneDeleted");
      navigate(ZONES_PATH);
      return;
    }
    if (result.problem?.type === "zone-in-use") {
      await this.#showBlockingBanners();
    } else {
      this.#fail(result.status, result.problem);
    }
  }

  /** Names of the banners that keep the zone from being deleted. */
  async #showBlockingBanners() {
    const page = await send<BannerPageModel>(
      listBanners({ query: { zone: this.#zoneKey, take: 100 } }),
    );
    this._blockingBanners = (page.data?.items ?? []).map((b) => ({ key: b.key, name: b.name }));
  }

  override render() {
    if (this._loading) {
      return html`<uui-loader></uui-loader>`;
    }
    if (this._missing) {
      return html`<umb-body-layout headline=${this.localize.term("meresoftBanners_zones")}>
        <uui-alert color="danger">${this.localize.term("meresoftBanners_zoneNotFound")}</uui-alert>
        <uui-button look="secondary" href=${ZONES_PATH} label=${this.localize.term("meresoftBanners_zones")}></uui-button>
      </umb-body-layout>`;
    }

    const aliasChanged = !!this.#zoneKey && this._alias.trim().toLowerCase() !== this.#savedAlias;
    return html`
      <umb-body-layout headline=${this._name || this.localize.term("meresoftBanners_createZone")}>
        <uui-button slot="header" compact href=${ZONES_PATH} label=${this.localize.term("meresoftBanners_zones")}></uui-button>
        ${this.#zoneKey
          ? html`<uui-tab-group slot="navigation">
              <uui-tab label=${this.localize.term("meresoftBanners_details")} ?active=${this._view === "details"} @click=${() => (this._view = "details")}></uui-tab>
              <uui-tab label=${this.localize.term("meresoftBanners_history")} ?active=${this._view === "history"} @click=${() => (this._view = "history")}></uui-tab>
            </uui-tab-group>`
          : nothing}
        ${this.#zoneKey && this._view === "history"
          ? html`<meresoft-zone-history .zoneKey=${this.#zoneKey}></meresoft-zone-history>`
          : nothing}
        <div ?hidden=${this._view === "history"}>
        ${this._conflict
          ? html`<uui-alert color="warning">
              ${this.localize.term("meresoftBanners_changedByAnotherUser")}
              <uui-button look="secondary" label=${this.localize.term("meresoftBanners_reload")} @click=${() => this.#load()}></uui-button>
            </uui-alert>`
          : nothing}
        ${this.#renderBlockingBanners()}
        <uui-box>
          ${this.#textField("alias", "meresoftBanners_zoneAlias", this._alias, (v) => (this._alias = v), "meresoftBanners_zoneAliasDescription")}
          ${aliasChanged
            ? html`<uui-alert color="warning">${this.localize.term("meresoftBanners_zoneAliasChangeWarning")}</uui-alert>`
            : nothing}
          ${this.#textField("name", "meresoftBanners_name", this._name, (v) => (this._name = v))}
          ${this.#textField("description", "meresoftBanners_zoneDescription", this._description, (v) => (this._description = v))}
          ${this.#textField("showCount", "meresoftBanners_zoneShowCount", String(this._showCount), (v) => (this._showCount = Number(v)), undefined, "number")}
          ${this.#textField("imageWidth", "meresoftBanners_zoneImageWidth", this._imageWidth, (v) => (this._imageWidth = v), undefined, "number")}
          ${this.#textField("imageHeight", "meresoftBanners_zoneImageHeight", this._imageHeight, (v) => (this._imageHeight = v), undefined, "number")}
          ${this.#renderModeChoice()}
        </uui-box>
        </div>
        <umb-footer-layout slot="footer">
          ${this.#zoneKey
            ? html`<uui-button
                slot="actions"
                look="secondary"
                color="danger"
                label=${this.localize.term("meresoftBanners_delete")}
                @click=${() => this.#delete()}></uui-button>`
            : nothing}
          <uui-button
            slot="actions"
            look="primary"
            color="positive"
            label=${this.localize.term("meresoftBanners_save")}
            @click=${() => this.#save()}></uui-button>
        </umb-footer-layout>
      </umb-body-layout>
    `;
  }

  #renderBlockingBanners() {
    if (this._blockingBanners.length === 0) {
      return nothing;
    }
    return html`
      <uui-alert color="danger">
        <strong>${this.localize.term("meresoftBanners_zoneInUseHeadline")}</strong>
        <p>${this.localize.term("meresoftBanners_zoneInUseContent")}</p>
        <ul>
          ${repeat(
            this._blockingBanners,
            (b) => b.key,
            (b) => html`<li><a href="section/meresoft-banners/banners/edit/${b.key}">${b.name}</a></li>`,
          )}
        </ul>
      </uui-alert>
    `;
  }

  /** Server or client rendering, with help about cached pages. */
  #renderModeChoice() {
    return html`<uui-form-layout-item>
      <uui-label slot="label">${this.localize.term("meresoftBanners_zoneRenderMode")}</uui-label>
      <span slot="description">${this.localize.term("meresoftBanners_zoneRenderModeHelp")}</span>
      <uui-radio-group
        .value=${this._renderMode}
        @change=${(e: Event) => (this._renderMode = (e.target as HTMLInputElement).value === "Client" ? "Client" : "Server")}>
        <uui-radio value="Server" label=${this.localize.term("meresoftBanners_zoneRenderModeServer")}></uui-radio>
        <uui-radio value="Client" label=${this.localize.term("meresoftBanners_zoneRenderModeClient")}></uui-radio>
      </uui-radio-group>
    </uui-form-layout-item>`;
  }

  #textField(
    field: string,
    labelKey: string,
    value: string,
    set: (value: string) => void,
    descriptionKey?: string,
    type: "text" | "number" = "text",
  ) {
    const label = this.localize.term(labelKey);
    return html`
      <uui-form-layout-item>
        <uui-label slot="label" for=${field}>${label}</uui-label>
        ${descriptionKey ? html`<span slot="description">${this.localize.term(descriptionKey)}</span>` : nothing}
        <uui-input
          id=${field}
          label=${label}
          type=${type}
          .value=${value}
          @input=${(e: Event) => set((e.target as HTMLInputElement).value)}></uui-input>
        ${this._errors[field] ? html`<div class="field-error" role="alert">${this._errors[field]}</div>` : nothing}
      </uui-form-layout-item>
    `;
  }

  static override styles = [
    UmbTextStyles,
    css`
      :host {
        display: block;
        height: 100%;
      }
      uui-alert,
      uui-box {
        margin-bottom: var(--uui-size-layout-1);
      }
      uui-input,
      uui-select {
        width: 100%;
      }
      [hidden] {
        display: none;
      }
      .field-error {
        color: var(--uui-color-danger-standalone);
        margin-top: var(--uui-size-space-2);
      }
      ul {
        margin: 0;
        padding-left: var(--uui-size-layout-1);
      }
    `,
  ];
}

export default MeresoftZoneEditorElement;

declare global {
  interface HTMLElementTagNameMap {
    "meresoft-zone-editor": MeresoftZoneEditorElement;
  }
}
