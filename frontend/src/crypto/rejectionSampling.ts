/**
 * Deterministic, reproducible, unbiased selection of a winning index from a fixed result hash.
 * Must match backend/src/FairDraw.Crypto/RejectionSampling.cs exactly — see this file's C#
 * counterpart for the full rationale (avoiding modulo bias without needing a resamplable random
 * stream, since the result hash is fixed once all reveals are in).
 *
 * seed = "{resultHashHex}|{attempt}"
 * candidateHash = SHA256(UTF8(seed))
 * value = big-endian uint64 of the first 8 bytes of candidateHash
 * limit = floor(2^64 / N) * N
 * while value >= limit: attempt++; recompute
 * winnerIndex = value % N
 *
 * CRITICAL: `value` can exceed Number.MAX_SAFE_INTEGER, so this uses BigInt throughout — never
 * `Number` — to avoid silently losing precision and producing a different winner than the backend.
 */

export type HashFunction = (input: string) => Promise<Uint8Array>

export interface SelectWinnerResult {
  winnerIndex: number
  attemptsUsed: number
}

const TOTAL_SPACE = 1n << 64n // 2^64, exceeds a 64-bit unsigned max (2^64 - 1) by exactly one.

export async function selectWinnerIndex(
  resultHashHex: string,
  optionCount: number,
  hashFn: HashFunction = defaultSha256,
): Promise<SelectWinnerResult> {
  if (!Number.isInteger(optionCount) || optionCount <= 0) {
    throw new RangeError('optionCount must be a positive integer.')
  }

  const n = BigInt(optionCount)
  const limit = (TOTAL_SPACE / n) * n

  for (let attempt = 0; ; attempt++) {
    const seed = `${resultHashHex}|${attempt}`
    const candidate = await hashFn(seed)
    const value = readUint64BigEndian(candidate)

    if (value < limit) {
      const winnerIndex = Number(value % n)
      return { winnerIndex, attemptsUsed: attempt }
    }
  }
}

function readUint64BigEndian(bytes: Uint8Array): bigint {
  const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength)
  return view.getBigUint64(0, false) // false = big-endian
}

async function defaultSha256(input: string): Promise<Uint8Array> {
  const bytes = new TextEncoder().encode(input)
  const digest = await crypto.subtle.digest('SHA-256', bytes)
  return new Uint8Array(digest)
}
