/** Represents one safe, contract-ready failure without leaking upstream details. */
export class NpcServiceError extends Error {
  public readonly code: string;
  public readonly statusCode: number;
  public readonly retryable: boolean;
  public readonly logCategory: string;

  /** Captures the public error mapping and an optional private cause. */
  public constructor(
    code: string,
    message: string,
    statusCode: number,
    retryable: boolean,
    logCategory: string,
    options?: ErrorOptions,
  ) {
    super(message, options);
    this.name = "NpcServiceError";
    this.code = code;
    this.statusCode = statusCode;
    this.retryable = retryable;
    this.logCategory = logCategory;
  }
}
