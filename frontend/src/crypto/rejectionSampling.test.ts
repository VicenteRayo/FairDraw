import { describe, expect, it } from 'vitest'
import { selectWinnerIndex } from './rejectionSampling'
import { sha256Hex } from './canonical'
import { goldenVectors } from '../testing/goldenVectors'

async function sha256Bytes(input: string): Promise<Uint8Array> {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(input))
  return new Uint8Array(digest)
}

describe('selectWinnerIndex (golden vector)', () => {
  it('matches the golden vector winnerIndex/attemptsUsed with no rejection needed', async () => {
    const result = await selectWinnerIndex(goldenVectors.resultHash, goldenVectors.optionCount)
    expect(result.winnerIndex).toBe(goldenVectors.winnerIndex)
    expect(result.attemptsUsed).toBe(goldenVectors.attemptsUsed)
  })

  it('is deterministic: same inputs always produce the same output', async () => {
    const a = await selectWinnerIndex(goldenVectors.resultHash, goldenVectors.optionCount)
    const b = await selectWinnerIndex(goldenVectors.resultHash, goldenVectors.optionCount)
    expect(a).toEqual(b)
  })

  it('for N=3, the only biased 8-byte prefix (0xFF...FF) is rejected and retried', async () => {
    // limit = floor(2^64/3)*3 = 2^64 - 1 = the maximum representable uint64, so the *only*
    // rejected value in the entire 2^64 space is 0xFFFFFFFFFFFFFFFF.
    let calls = 0
    const hashFn = async (seed: string) => {
      calls += 1
      if (seed.endsWith('|0')) {
        return new Uint8Array(32).fill(0xff)
      }
      return sha256Bytes(seed)
    }

    const result = await selectWinnerIndex('forced-rejection-vector', 3, hashFn)

    expect(result.attemptsUsed).toBe(1)
    expect(result.winnerIndex).toBeGreaterThanOrEqual(0)
    expect(result.winnerIndex).toBeLessThan(3)
    expect(calls).toBe(2)
  })

  it.each([2, 4, 8, 16, 65536])('never rejects for power-of-two option count N=%i', async (n) => {
    for (let i = 0; i < 25; i++) {
      const hash = await sha256Hex(`probe-${i}`)
      const result = await selectWinnerIndex(hash, n)
      expect(result.attemptsUsed).toBe(0)
    }
  })

  it.each([1, 2, 3, 5, 7, 10, 19])('always returns an index within bounds for N=%i', async (n) => {
    for (let i = 0; i < 50; i++) {
      const hash = await sha256Hex(`bounds-probe-${n}-${i}`)
      const result = await selectWinnerIndex(hash, n)
      expect(result.winnerIndex).toBeGreaterThanOrEqual(0)
      expect(result.winnerIndex).toBeLessThan(n)
    }
  })

  it('rejects a non-positive option count', async () => {
    await expect(selectWinnerIndex(goldenVectors.resultHash, 0)).rejects.toThrow(RangeError)
  })
})
