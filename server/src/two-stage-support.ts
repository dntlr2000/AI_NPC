/** Lists only model aliases that passed this source's Phase 17 adoption gate; an empty list keeps the route unavailable. */
export const adoptedTwoStageModels: readonly string[] = Object.freeze(["gpt-5.6-luna"]);

/** Rejects unvalidated model configurations without changing their existing single-pass route. */
export function supportsTwoStageModel(model: string): boolean {
  return adoptedTwoStageModels.includes(model);
}
