import { describe, expect, it } from "vitest";
import { adoptedTwoStageModels, supportsTwoStageModel } from "../src/two-stage-support.js";

describe("qualified two-stage model selection", () => {
  /** Enables only the measured API alias without silently extending support to a different configured model. */
  it("restricts adoption to the qualified API configuration", () => {
    expect(adoptedTwoStageModels).toEqual(["gpt-5.6-luna"]);
    expect(supportsTwoStageModel("gpt-5.6-luna")).toBe(true);
    for (const model of ["", "gpt-5.6-luna-other", "GEMMA", "gpt-5.6"]) {
      expect(supportsTwoStageModel(model)).toBe(false);
    }
  });
});
