import { describe, expect, it } from 'vitest'
import { computeCommitment, computeResult, generateSecretHex, randomHex, randomNumberInRange } from './crypto'
import { goldenVectors } from '../testing/goldenVectors'

describe('randomHex', () => {
  it.each([1, 16, 32])('returns lowercase hex of length 2*%i', (byteLength) => {
    const hex = randomHex(byteLength)
    expect(hex).toHaveLength(byteLength * 2)
    expect(hex).toMatch(/^[0-9a-f]+$/)
  })

  it('produces different values across calls', () => {
    const values = new Set(Array.from({ length: 20 }, () => randomHex(32)))
    expect(values.size).toBe(20)
  })
})

describe('generateSecretHex', () => {
  it('returns a 64-character lowercase hex string (32 bytes)', () => {
    expect(generateSecretHex()).toMatch(/^[0-9a-f]{64}$/)
  })
})

describe('randomNumberInRange', () => {
  it('always stays within [1, 10]', () => {
    for (let i = 0; i < 500; i++) {
      const n = randomNumberInRange(1, 10)
      expect(n).toBeGreaterThanOrEqual(1)
      expect(n).toBeLessThanOrEqual(10)
    }
  })

  it('covers the full range over many samples', () => {
    const seen = new Set<number>()
    for (let i = 0; i < 2000; i++) {
      seen.add(randomNumberInRange(1, 10))
    }
    expect(seen).toEqual(new Set([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]))
  })

  it('rejects a range larger than 256', () => {
    expect(() => randomNumberInRange(1, 300)).toThrow(RangeError)
  })
})

describe('computeCommitment (golden vectors)', () => {
  it.each(goldenVectors.participants)('matches the golden commitmentHash for $name', async (p) => {
    const commitment = await computeCommitment(goldenVectors.drawId, p.participantId, p.number, p.secretHex)
    expect(commitment).toBe(p.commitmentHash)
  })
})

describe('computeResult (golden vector, end to end)', () => {
  it('matches resultHash, winnerIndex and attemptsUsed', async () => {
    const reveals = goldenVectors.participants.map((p) => ({ participantId: p.participantId, number: p.number }))
    const result = await computeResult(goldenVectors.drawId, reveals, goldenVectors.optionCount)

    expect(result.resultHash).toBe(goldenVectors.resultHash)
    expect(result.winnerIndex).toBe(goldenVectors.winnerIndex)
    expect(result.attemptsUsed).toBe(goldenVectors.attemptsUsed)
  })
})
