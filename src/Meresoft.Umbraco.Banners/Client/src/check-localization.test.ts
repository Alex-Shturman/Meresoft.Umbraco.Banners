import { describe, expect, it } from "vitest";
import { checkSource } from "../scripts/check-localization.mjs";

describe("check-localization", () => {
  it("passes localized templates", () => {
    const source =
      'const t = html`<uui-button label=${this.localize.term("k")}>${this.localize.term("x")}</uui-button><umb-localize key="k"></umb-localize><p>${a} / ${b}</p>`;\nconst m = { label: "#key" };';
    expect(checkSource(source, "a.ts")).toEqual([]);
  });

  it("reports file and line for literal text between tags", () => {
    const problems = checkSource("const x = 1;\nconst t = html`\n<p>Hello world</p>`;", "a.ts");
    expect(problems).toEqual([{ file: "a.ts", line: 3, text: "Hello world" }]);
  });

  it("reports literal label, placeholder and manifest label", () => {
    const problems = checkSource(
      'const t = html`<uui-input placeholder="Type here" label="Name"></uui-input>`;\nconst m = { label: "Zones" };',
      "b.ts",
    );
    expect(problems.map((p) => p.text)).toEqual(['placeholder="Type here"', 'label="Name"', 'label: "Zones"']);
    expect(problems.map((p) => p.line)).toEqual([1, 1, 2]);
  });

  it("ignores text inside expressions and nested templates", () => {
    const source = 'const t = html`<ul>${items.map((i) => html`<li>${this.localize.term("k")}</li>`)}</ul>`;';
    expect(checkSource(source, "c.ts")).toEqual([]);
  });
});
