import { css, customElement, html, nothing, property, repeat, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { umbConfirmModal } from "@umbraco-cms/backoffice/modal";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { UMB_CURRENT_USER_CONTEXT } from "@umbraco-cms/backoffice/current-user";
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
import { resolveUtc, userTimeZone, utcToLocalInput } from "../shared/date-time.js";
import { STATE_DISPLAY, activeFlags, limitToText, parseLimit, showsClickLimit } from "./banner-state.js";
import { createAndPublish, createdBanner } from "./create-and-publish.js";
import { publishFailure } from "./publish-failure.js";
import { PendingProblemStore } from "./pending-problem.js";
import { canEditHtml, htmlFieldAccess, offeredBannerTypes } from "./html-permission.js";
import "./banner-history.element.js";

/** The number of a limit field that #limitErrors accepted: null for an empty field. */
function limitValue(text: string): number | null {
  const parsed = parseLimit(text);
  return parsed.ok ? parsed.value : null;
}

// The picker is a back office component; this import makes sure it is defined.
void UmbInputMediaElement;

const BANNERS_PATH = "section/meresoft-banners/banners";

/** Carries a publish failure of a new banner to its edit page, which the router may build as a new editor. */
const pendingProblems = new PendingProblemStore();

/** What differs between the editor's text fields. The range is stated by each field; nothing is assumed. */
interface TextFieldOptions {
  descriptionKey?: string;
  type?: "text" | "number" | "datetime-local";
  min?: number;
  max?: number;
  step?: number;
}

/** Creates and edits one banner (image or HTML): save the draft, publish, unpublish, copy and delete, and see its history. */
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
  @state() private _type: "Image" | "Html" = "Image";
  @state() private _html = "";
  @state() private _canEditHtml = false;
  @state() private _view: "details" | "history" = "details";
  @state() private _mediaKey = "";
  @state() private _linkUrl = "";
  @state() private _openInNewWindow = false;
  @state() private _altText = "";
  @state() private _title = "";
  @state() private _zoneKeys: string[] = [];
  @state() private _weight = "1";
  @state() private _start = "";
  @state() private _end = "";
  @state() private _impressionLimit = "";
  @state() private _clickLimit = "";
  @state() private _errors: Record<string, string> = {};
  @state() private _problem = "";
  @state() private _conflict = false;
  /** A save or publish is running; both buttons are off so a double click cannot create two banners. */
  @state() private _busy = false;

  #bannerKey?: string;
  #ready = false;
  #notifications?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;

  constructor() {
    super();
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (context) => (this.#notifications = context));
    // The server enforces the HTML permission on every write; this only decides what the editor can type. It is the
    // same check as the Umb.Condition.UserPermission.Fallback condition: any group of the user has the verb.
    this.consumeContext(UMB_CURRENT_USER_CONTEXT, (context) => {
      this.observe(context?.fallbackPermissions, (permissions) => {
        this._canEditHtml = canEditHtml(permissions);
      });
    });
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
      this._type = "Image";
      this._view = "details";
      this.#apply("", undefined);
      this._loading = false;
      return;
    }

    const result = await send<BannerResponseModel>(
      getBanner({ path: { key: this.#bannerKey } }),
    );
    // Taken after every load, so a problem nobody showed is gone and cannot appear later.
    const pending = pendingProblems.take(this.#bannerKey);
    if (result.data) {
      this.#show(result.data);
      if (pending) {
        this._errors = pending.errors;
        this._problem = pending.problem;
        this._conflict = pending.conflict;
      }
    } else {
      this._missing = true;
    }
    this._loading = false;
  }

  #show(banner: BannerResponseModel) {
    this._banner = banner;
    this._type = banner.type === "Html" ? "Html" : "Image";
    this.#apply(banner.name, banner.draft ?? undefined);
  }

  #apply(name: string, draft: BannerVersionModel | undefined) {
    this._name = name;
    this._mediaKey = draft?.mediaKey ?? "";
    this._html = draft?.html ?? "";
    this._linkUrl = draft?.linkUrl ?? "";
    this._openInNewWindow = draft?.openInNewWindow ?? false;
    this._altText = draft?.altText ?? "";
    this._title = draft?.title ?? "";
    this._zoneKeys = [...(draft?.zoneKeys ?? [])];
    this._weight = String(draft?.weight ?? 1);
    this._start = utcToLocalInput(draft?.startUtc);
    this._end = utcToLocalInput(draft?.endUtc);
    this._impressionLimit = limitToText(draft?.impressionLimit);
    this._clickLimit = limitToText(draft?.clickLimit);
  }

  /** The edited fields on top of what the draft already holds. */
  #body(): BannerRequestModel {
    const draft = this._banner?.draft;
    return {
      name: this._name,
      type: this._banner?.type ?? this._type,
      mediaKey: this._type === "Image" ? this._mediaKey || null : null,
      // Without the permission the stored HTML is sent back unchanged (the field is read-only), so saving other
      // fields works and the server sees no change.
      html: this._type === "Html" ? this._html : (draft?.html ?? null),
      linkUrl: this._linkUrl || null,
      openInNewWindow: this._openInNewWindow,
      altText: this._altText,
      // The title is the link title of an image banner; an HTML banner has no link of its own to put it on.
      title: this._type === "Image" ? this._title || null : null,
      // The real number goes to the server, or null when the field is empty or not a number, so the server's
      // error for the weight field shows instead of a silent 1.
      weight: this._weight.trim() === "" || Number.isNaN(Number(this._weight)) ? null : Number(this._weight),
      startUtc: resolveUtc(this._start, draft?.startUtc),
      endUtc: resolveUtc(this._end, draft?.endUtc),
      // The fields were checked by #limitErrors before this runs, so a refused text cannot get here.
      impressionLimit: limitValue(this._impressionLimit),
      // An Html banner has no click limit (FR-006), whatever the field held.
      clickLimit: showsClickLimit(this._type) ? limitValue(this._clickLimit) : null,
      zoneKeys: this._zoneKeys,
      updateDate: this._banner?.updateDate ?? null,
    };
  }

  /** Field errors for limit fields that hold something other than a whole number of 1 or more. */
  #limitErrors(): Record<string, string> {
    const errors: Record<string, string> = {};
    const message = this.localize.term("meresoftBanners_bannerLimitInvalid");
    if (!parseLimit(this._impressionLimit).ok) {
      errors.impressionLimit = message;
    }
    if (showsClickLimit(this._type) && !parseLimit(this._clickLimit).ok) {
      errors.clickLimit = message;
    }
    return errors;
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

    const limitErrors = this.#limitErrors();
    if (Object.keys(limitErrors).length > 0) {
      this._errors = limitErrors;
      return false;
    }

    if (!this.#bannerKey) {
      const created = createdBanner(await send(createBanner({ body: this.#body() })));
      if (created.kind === "created") {
        if (showToast) {
          this.#toast("meresoftBanners_bannerSaved");
        }
        navigate(`${BANNERS_PATH}/edit/${created.key}`);
        return true;
      }
      if (created.kind === "created-without-key") {
        // Saved, but the editor cannot tell where: the list shows it as a Draft.
        this.#toast("meresoftBanners_bannerSavedNotOpened");
        navigate(BANNERS_PATH);
        return true;
      }
      this.#fail(created.result.status, created.result.problem);
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

  /** Shows why publishing failed; a banner without a zone gets the error on the zone field. */
  #failPublish(status: number, problem: ApiProblem | undefined, afterCreate = false) {
    const view = publishFailure(status, problem, this.localize.term("meresoftBanners_bannerPublishFailed"), afterCreate);
    this._errors = view.errors;
    this._problem = view.problem;
    this._conflict = view.conflict;
    if (view.missing) {
      this._missing = true;
    }
  }

  /** Runs a save or publish once at a time. */
  async #exclusive(action: () => Promise<void>) {
    if (this._busy) {
      return;
    }
    this._busy = true;
    try {
      await action();
    } finally {
      this._busy = false;
    }
  }

  /** Publishing saves the edits first, so the published version is what the editor sees. */
  async #publish() {
    if (!this.#bannerKey) {
      await this.#createAndPublish();
      return;
    }
    if (!(await this.#save(false)) || !this._banner) {
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
    this.#failPublish(published.status, published.problem);
  }

  /**
   * Publish on a new banner: create it, publish it, and end on its edit address. When publishing fails the banner
   * stays a Draft and the edit page shows the problem.
   */
  async #createAndPublish() {
    this._errors = {};
    this._problem = "";
    this._conflict = false;

    const limitErrors = this.#limitErrors();
    if (Object.keys(limitErrors).length > 0) {
      this._errors = limitErrors;
      return;
    }

    const outcome = await createAndPublish({
      create: () => send(createBanner({ body: this.#body() })),
      load: (key) => send<BannerResponseModel>(getBanner({ path: { key } })),
      publish: (key, updateDate) =>
        send<BannerResponseModel>(publishBanner({ path: { key }, body: { updateDate } })),
    });

    if (outcome.kind === "create-failed") {
      this.#fail(outcome.result.status, outcome.result.problem);
      return;
    }
    if (outcome.kind === "created-without-key") {
      // Saved, but the editor cannot tell where: the list shows it as a Draft.
      this.#toast("meresoftBanners_bannerSavedNotOpened");
      navigate(BANNERS_PATH);
      return;
    }
    if (outcome.kind === "published") {
      this.#toast("meresoftBanners_bannerPublished");
    } else {
      // A 404 right after the create is not a missing page: the edit page loads the banner again and shows the message.
      this.#failPublish(outcome.result.status, outcome.result.problem, true);
      pendingProblems.set({
        key: outcome.key,
        errors: this._errors,
        problem: this._problem,
        conflict: this._conflict,
      });
    }
    navigate(`${BANNERS_PATH}/edit/${outcome.key}`);
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
        ${banner
          ? html`<uui-tab-group slot="navigation">
              <uui-tab label=${this.localize.term("meresoftBanners_details")} ?active=${this._view === "details"} @click=${() => (this._view = "details")}></uui-tab>
              <uui-tab label=${this.localize.term("meresoftBanners_history")} ?active=${this._view === "history"} @click=${() => (this._view = "history")}></uui-tab>
            </uui-tab-group>`
          : nothing}
        ${banner && this._view === "history"
          ? html`<meresoft-banner-history .bannerKey=${this.#bannerKey}></meresoft-banner-history>`
          : nothing}
        <div ?hidden=${this._view === "history"}>
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
          ${!banner ? this.#renderTypeChoice() : nothing}
          ${this._type === "Html" ? this.#renderHtmlField() : this.#renderImageFields()}
          ${this.#textField("weight", "meresoftBanners_bannerWeight", this._weight, (v) => (this._weight = v), { descriptionKey: "meresoftBanners_bannerWeightDescription", type: "number", min: 1, max: 100 })}
          ${this.#textField("startUtc", "meresoftBanners_bannerStart", this._start, (v) => (this._start = v), { descriptionKey: "meresoftBanners_bannerScheduleDescription", type: "datetime-local" })}
          ${this.#textField("endUtc", "meresoftBanners_bannerEnd", this._end, (v) => (this._end = v), { type: "datetime-local" })}
          ${this.#textField("impressionLimit", "meresoftBanners_bannerImpressionLimit", this._impressionLimit, (v) => (this._impressionLimit = v), { descriptionKey: "meresoftBanners_bannerImpressionLimitDescription", type: "number", min: 1, step: 1 })}
          ${showsClickLimit(this._type)
            ? this.#textField("clickLimit", "meresoftBanners_bannerClickLimit", this._clickLimit, (v) => (this._clickLimit = v), { descriptionKey: "meresoftBanners_bannerClickLimitDescription", type: "number", min: 1, step: 1 })
            : nothing}
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
        </div>
        <umb-footer-layout slot="footer">
          ${banner
            ? html`<uui-button slot="actions" look="secondary" color="danger" label=${this.localize.term("meresoftBanners_delete")} @click=${() => this.#delete()}></uui-button>
                <uui-button slot="actions" look="secondary" label=${this.localize.term("meresoftBanners_copy")} @click=${() => this.#copy()}></uui-button>`
            : nothing}
          ${isPublished
            ? html`<uui-button slot="actions" look="secondary" label=${this.localize.term("meresoftBanners_unpublish")} @click=${() => this.#unpublish()}></uui-button>`
            : nothing}
          <uui-button slot="actions" look="secondary" label=${this.localize.term("meresoftBanners_save")} ?disabled=${this._busy} @click=${() => this.#exclusive(async () => void (await this.#save()))}></uui-button>
          <uui-button slot="actions" look="primary" color="positive" label=${this.localize.term("meresoftBanners_publish")} ?disabled=${this._busy} @click=${() => this.#exclusive(() => this.#publish())}></uui-button>
        </umb-footer-layout>
      </umb-body-layout>
    `;
  }

  /** Image or HTML, chosen when a banner is created; HTML is offered only to users with the permission. */
  #renderTypeChoice() {
    return html`<uui-form-layout-item>
      <uui-label slot="label">${this.localize.term("meresoftBanners_bannerType")}</uui-label>
      <uui-radio-group
        .value=${this._type}
        @change=${(e: Event) => (this._type = (e.target as HTMLInputElement).value === "Html" ? "Html" : "Image")}>
        <uui-radio value="Image" label=${this.localize.term("meresoftBanners_bannerTypeImage")}></uui-radio>
        ${offeredBannerTypes(this._canEditHtml).includes("Html")
          ? html`<uui-radio value="Html" label=${this.localize.term("meresoftBanners_bannerTypeHtml")}></uui-radio>`
          : nothing}
      </uui-radio-group>
    </uui-form-layout-item>`;
  }

  /** The code of an HTML banner: monospaced, and read-only without the HTML permission. */
  #renderHtmlField() {
    const label = this.localize.term("meresoftBanners_bannerHtml");
    const access = htmlFieldAccess(this._canEditHtml);
    return html`<uui-form-layout-item>
      <uui-label slot="label" for="html">${label}</uui-label>
      <span slot="description">${this.localize.term(access.noteKey)}</span>
      <uui-textarea
        id="html"
        class="code"
        label=${label}
        rows="12"
        spellcheck="false"
        ?readonly=${access.readonly}
        .value=${this._html}
        @input=${(e: Event) => (this._html = (e.target as HTMLTextAreaElement).value)}></uui-textarea>
      ${this.#error("html")}
    </uui-form-layout-item>`;
  }

  #renderImageFields() {
    return html`
          <uui-form-layout-item>
            <uui-label slot="label">${this.localize.term("meresoftBanners_bannerImage")}</uui-label>
            <span slot="description">${this.localize.term("meresoftBanners_bannerImageDescription")}</span>
            <umb-input-media
              max="1"
              .selection=${this._mediaKey ? [this._mediaKey] : []}
              @change=${(e: Event) => (this._mediaKey = (e.target as UmbInputMediaElement).selection[0] ?? "")}></umb-input-media>
            ${this.#error("mediaKey")}
          </uui-form-layout-item>
          ${this.#textField("linkUrl", "meresoftBanners_bannerLink", this._linkUrl, (v) => (this._linkUrl = v), { descriptionKey: "meresoftBanners_bannerLinkDescription" })}
          <uui-form-layout-item>
            <uui-toggle
              label=${this.localize.term("meresoftBanners_bannerOpenInNewWindow")}
              ?checked=${this._openInNewWindow}
              @change=${(e: Event) => (this._openInNewWindow = (e.target as HTMLInputElement).checked)}></uui-toggle>
          </uui-form-layout-item>
          ${this.#textField("altText", "meresoftBanners_bannerAltText", this._altText, (v) => (this._altText = v), { descriptionKey: "meresoftBanners_bannerAltTextDescription" })}
          ${this.#textField("title", "meresoftBanners_bannerTitle", this._title, (v) => (this._title = v))}
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

  #textField(
    field: string,
    labelKey: string,
    value: string,
    set: (value: string) => void,
    options: TextFieldOptions = {},
  ) {
    const { descriptionKey, type = "text", min, max, step } = options;
    const label = this.localize.term(labelKey);
    return html`
      <uui-form-layout-item>
        <uui-label slot="label" for=${field}>${label}</uui-label>
        ${descriptionKey ? html`<span slot="description">${this.localize.term(descriptionKey, userTimeZone())}</span>` : nothing}
        <uui-input id=${field} type=${type} min=${min ?? nothing} max=${max ?? nothing} step=${step ?? nothing} label=${label} .value=${value} @input=${(e: Event) => set((e.target as HTMLInputElement).value)}></uui-input>
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

      /* The default help text colour of the form layout item is below the AA contrast ratio on the editor background. */
      span[slot="description"] {
        color: var(--uui-color-text);
      }
      uui-alert,
      uui-box,
      .state {
        margin-bottom: var(--uui-size-layout-1);
      }
      .state uui-tag + uui-tag {
        margin-left: var(--uui-size-space-2);
      }
      uui-input,
      uui-textarea {
        width: 100%;
      }
      uui-textarea.code {
        font-family: var(--uui-font-monospace, ui-monospace, monospace);
        font-size: var(--uui-type-small-size);
      }
      [hidden] {
        display: none;
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
