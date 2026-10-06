import { bytesToHex, buildCommitmentCanonical, buildRevealedDataCanonical, sha256Hex, type RevealedEntry } from './canonical'
import { selectWinnerIndex, type SelectWinnerResult } from './rejectionSampling'

export { sha256Hex, isSafeForCanonical, buildConfigurationCanonical, buildCommitmentCanonical, buildRevealedDataCanonical } from './canonical'
export { selectWinnerIndex } from './rejectionSampling'
export type { RevealedEntry } from './canonical'
export type { SelectWinnerResult } from './rejectionSampling'

/** `byteLength` cryptographically secure random bytes (crypto.getRandomValues), as lowercase hex. */
export function randomHex(byteLength: number): string {
  const bytes = new Uint8Array(byteLength)
  crypto.getRandomValues(bytes)
  return bytesToHex(bytes)
}

/**
 * Uniformly random integer in [min, max] (inclusive), via rejection sampling on a single byte to
 * avoid modulo bias. Mirrors FairDraw.Crypto.SecretGenerator.GenerateNumberInRange — a hygiene
 * choice for the participant's own number, not the security-critical part (that's
 * selectWinnerIndex, which maps the *result* hash to the winning option without bias).
 */
export function randomNumberInRange(min: number, max: number): number {
  const range = max - min + 1
  if (range <= 0 || range > 256) {
    throw new RangeError('Range must be within (0, 256].')
  }

  const limit = Math.floor(256 / range) * range
  const byte = new Uint8Array(1)
  for (;;) {
    crypto.getRandomValues(byte)
    if (byte[0] < limit) {
      return min + (byte[0] % range)
    }
  }
}

/** Generates a fresh 256-bit secret for a participant's own commit, as lowercase hex (64 chars). */
export function generateSecretHex(): string {
  return randomHex(32)
}

/** commitment = SHA256(canonical commitment string). See canonical.ts for the exact format. */
export async function computeCommitment(
  drawId: string,
  participantId: string,
  number: number,
  secretHex: string,
): Promise<string> {
  return sha256Hex(buildCommitmentCanonical(drawId, participantId, number, secretHex))
}

/** resultHash = SHA256(canonical revealed-data string). See canonical.ts for the exact format. */
export async function computeResultHash(drawId: string, reveals: RevealedEntry[]): Promise<string> {
  return sha256Hex(buildRevealedDataCanonical(drawId, reveals))
}

/** Convenience: resultHash + rejection-sampled winner in one call. */
export async function computeResult(drawId: string, reveals: RevealedEntry[], optionCount: number): Promise<{ resultHash: string } & SelectWinnerResult> {
  const resultHash = await computeResultHash(drawId, reveals)
  const winner = await selectWinnerIndex(resultHash, optionCount)
  return { resultHash, ...winner }
}
