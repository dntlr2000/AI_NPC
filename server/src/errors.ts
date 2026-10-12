export interface NpcInferenceDiagnostics {
  readonly modelCalls: number;
  readonly failureStage: string;
  readonly totalSeconds: number;
  readonly stages: readonly {
    stage: string;
    seconds: number;
    inputTokens: number;
    outputTokens: number;
    failureCode?: string;
  }[];
}

/** Represents one safe, contract-ready failure without leaking upstream details. */
export class NpcServiceError extends Error {
  public readonly code: string;
  public readonly statusCode: number;
  public readonly retryable: boolean;
  public readonly logCategory: string;
  public readonly inferenceDiagnostics?: NpcInferenceDiagnostics;

  /** Captures the public error mapping and an optional private cause. */
  public constructor(
    code: string,
    message: string,
    statusCode: number,
    retryable: boolean,
    logCategory: string,
    options?: ErrorOptions,
    inferenceDiagnostics?: NpcInferenceDiagnostics,
  ) {
    super(message, options);
    this.name = "NpcServiceError";
    this.code = code;
    this.statusCode = statusCode;
    this.retryable = retryable;
    this.logCategory = logCategory;
    if (inferenceDiagnostics) this.inferenceDiagnostics = inferenceDiagnostics;
  }
}
