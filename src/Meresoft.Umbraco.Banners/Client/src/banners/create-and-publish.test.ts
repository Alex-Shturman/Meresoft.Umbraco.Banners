import { describe, expect, it } from "vitest";
import type { BannerResponseModel } from "../api/types.gen.js";
import type { ApiResult } from "../shared/api.js";
import { createAndPublish, createdBanner } from "./create-and-publish.js";

const KEY = "11111111-2222-3333-4444-555555555555";
const loaded = { key: KEY, updateDate: "2026-01-02T03:04:05Z" } as unknown as BannerResponseModel;
const published = { key: KEY, state: "Published" } as unknown as BannerResponseModel;

/** Fake steps that record the order of the calls and what the publish step was given. */
function steps(
  overrides: {
    create?: ApiResult<unknown>;
    load?: ApiResult<BannerResponseModel>;
    publish?: ApiResult<BannerResponseModel>;
  } = {},
) {
  const calls: string[] = [];
  let publishedWith: { key: string; updateDate: string } | undefined;
  return {
    calls,
    publishedWith: () => publishedWith,
    steps: {
      create: async (): Promise<ApiResult<unknown>> => {
        calls.push("create");
        return overrides.create ?? { status: 201, location: `/umbraco/api/banners/${KEY}` };
      },
      load: async (key: string): Promise<ApiResult<BannerResponseModel>> => {
        calls.push(`load ${key}`);
        return overrides.load ?? { status: 200, data: loaded };
      },
      publish: async (key: string, updateDate: string): Promise<ApiResult<BannerResponseModel>> => {
        calls.push("publish");
        publishedWith = { key, updateDate };
        return overrides.publish ?? { status: 200, data: published };
      },
    },
  };
}

describe("createdBanner", () => {
  it("gives the key at the end of the Location of a 201", () => {
    expect(createdBanner({ status: 201, location: `/umbraco/api/banners/${KEY}` })).toEqual({ kind: "created", key: KEY });
  });

  it.each([undefined, ""])("reports a 201 whose Location is %j as created without a key", (location) => {
    expect(createdBanner({ status: 201, location })).toEqual({ kind: "created-without-key" });
  });

  it("reports any other status as failed", () => {
    const result = { status: 400, problem: { detail: "bad" } };
    expect(createdBanner(result)).toEqual({ kind: "create-failed", result });
  });
});

describe("createAndPublish", () => {
  it("creates, loads the banner by the created key, then publishes with its updateDate", async () => {
    const fake = steps();
    const outcome = await createAndPublish(fake.steps);
    expect(fake.calls).toEqual(["create", `load ${KEY}`, "publish"]);
    expect(fake.publishedWith()).toEqual({ key: KEY, updateDate: loaded.updateDate });
    expect(outcome).toEqual({ kind: "published", key: KEY, banner: published });
  });

  it.each([undefined, ""])("reports a created banner whose Location is %j as created without a key", async (location) => {
    const fake = steps({ create: { status: 201, location } });
    const outcome = await createAndPublish(fake.steps);
    expect(fake.calls).toEqual(["create"]);
    expect(outcome).toEqual({ kind: "created-without-key" });
  });

  it("does not load or publish when the create fails", async () => {
    const create = { status: 400, problem: { detail: "bad" } };
    const fake = steps({ create });
    const outcome = await createAndPublish(fake.steps);
    expect(fake.calls).toEqual(["create"]);
    expect(outcome).toEqual({ kind: "create-failed", result: create });
  });

  it("keeps the created banner as a draft when the publish fails", async () => {
    const publish = { status: 400, problem: { type: "no-zone-assigned", detail: "Pick a zone." } };
    const fake = steps({ publish });
    const outcome = await createAndPublish(fake.steps);
    expect(outcome).toEqual({ kind: "publish-failed", key: KEY, result: publish });
  });

  it("reports the created key when the banner cannot be loaded for publishing", async () => {
    const load = { status: 500 };
    const fake = steps({ load });
    const outcome = await createAndPublish(fake.steps);
    expect(fake.calls).toEqual(["create", `load ${KEY}`]);
    expect(outcome).toEqual({ kind: "publish-failed", key: KEY, result: load });
  });
});
