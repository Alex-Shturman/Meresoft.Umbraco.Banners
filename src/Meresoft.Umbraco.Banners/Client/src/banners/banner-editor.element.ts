import { css, customElement, html, nothing, property, repeat, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { umbConfirmModal } from "@umbraco-cms/backoffice/modal";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { UmbInputMediaElement } from "@umbraco-cms/backoffice/media";
import {
  deleteBanner,
  getBanner,
  listZones,
  createBanner,
  copyBanner,
  publishBanner,
  unpublishBanner,
  updateBanner,
} from "../api/sdk.gen.js";
import type {
  BannerRequestModel,
  BannerResponseModel,
  BannerVersionModel,
  ZoneResponseModel,
} from "../api/types.gen.js";
import { configureBannersClient } from "../api/client-setup.js";
import { client, fieldErrors, keyFromLocation, navigate, send, type ApiProblem } from "../shared/api.js";
import { STATE_DISPLAY, activeFlags } from "./banner-state.js";

// The picker is a back office component; this import makes sure it is defined.
void UmbInputMediaElement;

const BANNERS_PATH = "section/meresoft-banners/banners";

/** Creates and edits one image banner: save the draft, publish, unpublish, copy and delete. */
@customElement("meresoft-banner-editor")
export class MeresoftBannerEditorElement extends UmbLitElement {
  /** Key of the banner to edit; unset when creating. */
  @property({ attribute: false })
  set bannerKey(value: string | undefined) {
    const changed = value !== this.#bannerKey;
    this.#bannerKey = value;
    if (changed && this.#ready) {
      void this.#load();
    }
  }
  get bannerKey(): string | undefined {
    return this.#bannerKey;
  }

  @state() private _loading = true;
  @state() private _missing = false;
  @state() private _banner?: BannerResponseModel;
  @state() private _zones: ZoneResponseModel[] = [];
  @state() private _name = "";
  @state() private _mediaKey = "";
  @state() private _linkUrl = "";
  @state() private _openInNewWindow = false;
  @state() private _altText = "";
  @state() private _title = "";
  @state() private _zoneKeys: string[] = [];
  @state() private _errors: Record<string, string> = {};
  @state() private _problem = "";
  @state() private _conflict = false;

  #bannerKey?: string;
  #ready = false;
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
    this._problem = "";

    const zones = await send<ZoneResponseModel[]>(listZones());
    this._zones = zones.data ?? [];

    if (!this.#bannerKey) {
      this._banner = undefined;
      this.#apply("", undefined);
      this._loading = false;
      return;
    }

    const result = await send<BannerResponseModel>(
      getBanner({ path: { key: this.#bannerKey } }),
    );
    if (result.data) {
      this.#show(result.data);
    } else {
      this._missing = true;
    }
    this._loading = false;
  }

  #show(banner: BannerResponseModel) {
    this._banner = banner;
    this.#apply(banner.name, banner.draft ?? undefined);
  }

  #apply(name: string, draft: BannerVersionModel | undefined) {
    this._name = name;
    this._mediaKey = draft?.mediaKey ?? "";
    this._linkUrl = draft?.linkUrl ?? "";
    this._openInNewWindow = draft?.openInNewWindow ?? false;
    this._altText = draft?.altText ?? "";
    this._title = draft?.title ?? "";
    this._zoneKeys = [...(draft?.zoneKeys ?? [])];
  }

  /** The edited fields on top of what the draft already holds (weight, schedule and limits are edited elsewhere). */
  #body(): BannerRequestModel {
    const draft = this._banner?.draft;
    return {
      name: this._name,
      type: this._banner?.type ?? "Image",
      mediaKey: this._mediaKey || null,
      html: draft?.html ?? null,
      linkUrl: this._linkUrl || null,
      openInNewWindow: this._openInNewWindow,
      altText: this._altText,
      title: this._title || null,
      weight: draft?.weight ?? 1,
      startUtc: draft?.startUtc ?? null,
      endUtc: draft?.endUtc ?? null,
      impressionLimit: draft?.impressionLimit ?? null,
      clickLimit: draft?.clickLimit ?? null,
      zoneKeys: this._zoneKeys,
      updateDate: this._banner?.updateDate ?? null,
    };
  }

  #toast(key: string) {
    this.#notifications?.peek("positive", { data: { message: this.localize.term(key) } });
  }

  #fail(status: number, problem: ApiProblem | undefined) {
    this._errors = status === 400 ? fieldErrors(problem) : {};
    this._problem = status === 400 && Object.keys(this._errors).length === 0 ? (problem?.detail ?? "") : "";
    this._conflict = problem?.type === "changed-by-another-user";
    if (status === 404) {
      this._missing = true;
    }
  }

  /** Saves the draft; returns false when it failed. A new banner moves to its edit address. */
  async #save(showToast = true): Promise<boolean> {
    this._errors = {};
    this._problem = "";
    this._conflict = false;

    if (!this.#bannerKey) {
      const created = await send(createBanner({ body: this.#body() }));
      if (created.status === 201) {
        if (showToast) {
          this.#toast("meresoftBanners_bannerSaved");
        }
        navigate(`${BANNERS_PATH}/edit/${keyFromLocation(created.location)}`);
        return true;
      }
      this.#fail(created.status, created.problem);
      return false;
    }

    const saved = await send<BannerResponseModel>(
      updateBanner({ path: { key: this.#bannerKey }, body: this.#body() }),
    );
    if (saved.data) {
      this.#show(saved.data);
      if (showToast) {
        this.#toast("meresoftBanners_bannerSaved");
      }
      return true;
    }
    this.#fail(saved.status, saved.problem);
    return false;
  }

  /** Publishing saves the edits first, so the published version is what the editor sees. */
  async #publish() {
    if (!(await this.#save(false)) || !this.#bannerKey || !this._banner) {
      return;
    }
    const published = await send<BannerResponseModel>(
      publishBanner({
        path: { key: this.#bannerKey },
        body: { updateDate: this._banner.updateDate },
      }),
    );
    if (published.data) {
      this.#show(published.data);
      this.#toast("meresoftBanners_bannerPublished");
      return;
    }
    this.#fail(published.status, published.problem);
    if (published.problem?.type === "no-zone-assigned") {
      this._errors = { zoneKeys: published.problem.detail ?? "" };
    }
  }

  async #unpublish() {
    if (!this.#bannerKey) {
      return;
    }
    const result = await send<BannerResponseModel>(
      unpublishBanner({ path: { key: this.#bannerKey } }),
    );
    if (result.data) {
      this.#show(result.data);
      this.#toast("meresoftBanners_bannerUnpublished");
    } else {
      this.#fail(result.status, result.problem);
    }
  }

  async #copy() {
    if (!this.#bannerKey) {
      return;
    }
    const result = await send(
      copyBanner({ path: { key: this.#bannerKey }, body: {} }),
    );
    if (result.status === 201) {
      this.#toast("meresoftBanners_bannerCopied");
      navigate(`${BANNERS_PATH}/edit/${keyFromLocation(result.location)}`);
    } else {
      this.#fail(result.status, result.problem);
    }
  }

  async #delete() {
    if (!this.#bannerKey) {
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
    const result = await send(deleteBanner({ path: { key: this.#bannerKey } }));
    if (result.status === 204) {
      this.#toast("meresoftBanners_bannerDeleted");
      navigate(BANNERS_PATH);
    } else {
      this.#fail(result.status, result.problem);
    }
  }

  #toggleZone(key: string, selected: boolean) {
    this._zoneKeys = selected ? [...new Set([...this._zoneKeys, key])] : this._zoneKeys.filter((k) => k !== key);
  }

  override render() {
    if (this._loading) {
      return html`<uui-loader></uui-loader>`;
    }
    if (this._missing) {
      return html`<umb-body-layout headline=${this.localize.term("meresoftBanners_banners")}>
        <uui-alert color="danger">${this.localize.term("meresoftBanners_bannerNotFound")}</uui-alert>
        <uui-button look="secondary" href=${BANNERS_PATH} label=${this.localize.term("meresoftBanners_banners")}></uui-button>
      </umb-body-layout>`;
    }

    const banner = this._banner;
    const isPublished = banner?.state === "Published" || banner?.state === "PublishedWithChanges";
    return html`
      <umb-body-layout headline=${banner?.name || this.localize.term("meresoftBanners_createBanner")}>
        <uui-button slot="header" compact href=${BANNERS_PATH} label=${this.localize.term("meresoftBanners_banners")}></uui-button>
        ${banner ? this.#renderState(banner) : nothing}
        ${this._conflict
          ? html`<uui-alert color="warning">
              ${this.localize.term("meresoftBanners_changedByAnotherUser")}
              <uui-button look="secondary" label=${this.localize.term("meresoftBanners_reload")} @click=${() => this.#load()}></uui-button>
            </uui-alert>`
          : nothing}
        ${this._problem ? html`<uui-alert color="danger">${this._problem}</uui-alert>` : nothing}
        <uui-box>
          ${this.#textField("name", "meresoftBanners_name", this._name, (v) => (this._name = v))}
          <uui-form-layout-item>
            <uui-label slot="label">${this.localize.term("meresoftBanners_bannerImage")}</uui-label>
            <umb-input-media
              max="1"
              .selection=${this._mediaKey ? [this._mediaKey] : []}
              @change=${(e: Event) => (this._mediaKey = (e.target as UmbInputMediaElement).selection[0] ?? "")}></umb-input-media>
            ${this.#error("mediaKey")}
          </uui-form-layout-item>
          ${this.#textField("linkUrl", "meresoftBanners_bannerLink", this._linkUrl, (v) => (this._linkUrl = v), "meresoftBanners_bannerLinkDescription")}
          <uui-form-layout-item>
            <uui-toggle
              label=${this.localize.term("meresoftBanners_bannerOpenInNewWindow")}
              ?checked=${this._openInNewWindow}
              @change=${(e: Event) => (this._openInNewWindow = (e.target as HTMLInputElement).checked)}></uui-toggle>
          </uui-form-layout-item>
          ${this.#textField("altText", "meresoftBanners_bannerAltText", this._altText, (v) => (this._altText = v), "meresoftBanners_bannerAltTextDescription")}
          ${this.#textField("title", "meresoftBanners_bannerTitle", this._title, (v) => (this._title = v))}
          <uui-form-layout-item>
            <uui-label slot="label">${this.localize.term("meresoftBanners_bannerZones")}</uui-label>
            ${this._zones.length === 0
              ? html`<p>${this.localize.term("meresoftBanners_bannerNoZonesAvailable")}</p>`
              : repeat(
                  this._zones,
                  (z) => z.key,
                  (z) => html`<div>
                    <uui-checkbox
                      label=${z.name}
                      ?checked=${this._zoneKeys.includes(z.key)}
                      @change=${(e: Event) => this.#toggleZone(z.key, (e.target as HTMLInputElement).checked)}></uui-checkbox>
                  </div>`,
                )}
            ${this.#error("zoneKeys")}
          </uui-form-layout-item>
        </uui-box>
        <umb-footer-layout slot="footer">
          ${banner
            ? html`<uui-button slot="actions" look="secondary" color="danger" label=${this.localize.term("meresoftBanners_delete")} @click=${() => this.#delete()}></uui-button>
                <uui-button slot="actions" look="secondary" label=${this.localize.term("meresoftBanners_copy")} @click=${() => this.#copy()}></uui-button>`
            : nothing}
          ${isPublished
            ? html`<uui-button slot="actions" look="secondary" label=${this.localize.term("meresoftBanners_unpublish")} @click=${() => this.#unpublish()}></uui-button>`
            : nothing}
          <uui-button slot="actions" look="secondary" label=${this.localize.term("meresoftBanners_save")} @click=${() => this.#save()}></uui-button>
          <uui-button slot="actions" look="primary" color="positive" label=${this.localize.term("meresoftBanners_publish")} @click=${() => this.#publish()}></uui-button>
        </umb-footer-layout>
      </umb-body-layout>
    `;
  }

  #renderState(banner: BannerResponseModel) {
    const display = STATE_DISPLAY[banner.state];
    return html`<div class="state">
      <uui-tag look="primary" color=${display.color}>${this.localize.term(display.key)}</uui-tag>
      ${activeFlags(banner.flags).map((f) => html`<uui-tag look="outline" color=${f.color}>${this.localize.term(f.key)}</uui-tag>`)}
    </div>`;
  }

  #error(field: string) {
    return this._errors[field] ? html`<div class="field-error" role="alert">${this._errors[field]}</div>` : nothing;
  }

  #textField(field: string, labelKey: string, value: string, set: (value: string) => void, descriptionKey?: string) {
    const label = this.localize.term(labelKey);
    return html`
      <uui-form-layout-item>
        <uui-label slot="label" for=${field}>${label}</uui-label>
        ${descriptionKey ? html`<span slot="description">${this.localize.term(descriptionKey)}</span>` : nothing}
        <uui-input id=${field} label=${label} .value=${value} @input=${(e: Event) => set((e.target as HTMLInputElement).value)}></uui-input>
        ${this.#error(field)}
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
      uui-box,
      .state {
        margin-bottom: var(--uui-size-layout-1);
      }
      .state uui-tag + uui-tag {
        margin-left: var(--uui-size-space-2);
      }
      uui-input {
        width: 100%;
      }
      .field-error {
        color: var(--uui-color-danger-standalone);
        margin-top: var(--uui-size-space-2);
      }
    `,
  ];
}

export default MeresoftBannerEditorElement;

declare global {
  interface HTMLElementTagNameMap {
    "meresoft-banner-editor": MeresoftBannerEditorElement;
  }
}
