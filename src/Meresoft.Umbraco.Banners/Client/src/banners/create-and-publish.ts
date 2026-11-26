import type { BannerResponseModel } from "../api/types.gen.js";
import { keyFromLocation, type ApiResult } from "../shared/api.js";

/** The three calls Publish makes on a banner that does not exist yet; the editor passes the real requests. */
export interface CreateAndPublishSteps {
  create: () => Promise<ApiResult<unknown>>;
  load: (key: string) => Promise<ApiResult<BannerResponseModel>>;
  publish: (key: string, updateDate: string) => Promise<ApiResult<BannerResponseModel>>;
}

/**
 * How Publish on a new banner ended. After "publish-failed" the banner exists as a Draft under `key`, so the editor
 * must go to its edit address and show the problem there.
 */
export type CreateAndPublishOutcome =
  | { kind: "create-failed"; result: ApiResult<unknown> }
  /** The banner was created (201) but the response did not say where, so it cannot be opened or published here. */
  | { kind: "created-without-key" }
  | { kind: "published"; key: string; banner: BannerResponseModel }
  | { kind: "publish-failed"; key: string; result: ApiResult<unknown> };

/** Creates the banner, then publishes it with the key and `updateDate` of what was created. It has no DOM. */
export async function createAndPublish(steps: CreateAndPublishSteps): Promise<CreateAndPublishOutcome> {
  const created = await steps.create();
  if (created.status !== 201) {
    return { kind: "create-failed", result: created };
  }
  const key = keyFromLocation(created.location);
  if (!key) {
    return { kind: "created-without-key" };
  }

  const loaded = await steps.load(key);
  if (!loaded.data) {
    return { kind: "publish-failed", key, result: loaded };
  }

  const published = await steps.publish(key, loaded.data.updateDate);
  return published.data
    ? { kind: "published", key, banner: published.data }
    : { kind: "publish-failed", key, result: published };
}
