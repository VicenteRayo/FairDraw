import { describe, expect, it } from 'vitest'
import {
  buildCommitmentCanonical,
  buildConfigurationCanonical,
  buildRevealedDataCanonical,
  isSafeForCanonical,
  sha256Hex,
} from './canonical'
import { goldenVectors } from '../testing/goldenVectors'

describe('canonical formats (golden vectors)', () => {
  it('builds the configuration canonical string exactly like the backend', () => {
    const canonical = buildConfigurationCanonical(goldenVectors.drawId, goldenVectors.options, goldenVectors.participantsNames)
    expect(canonical).toBe(goldenVectors.configurationCanonical)
  })

  it('hashes the configuration canonical string to the expected configurationHash', async () => {
    const canonical = buildConfigurationCanonical(goldenVectors.drawId, goldenVectors.options, goldenVectors.participantsNames)
    await expect(sha256Hex(canonical)).resolves.toBe(goldenVectors.configurationHash)
  })

  it.each(goldenVectors.participants)('builds the commitment canonical string for $name', (p) => {
    const canonical = buildCommitmentCanonical(goldenVectors.drawId, p.participantId, p.number, p.secretHex)
    expect(canonical).toBe(p.commitmentCanonical)
  })

  it.each(goldenVectors.participants)('hashes the commitment for $name to the expected commitmentHash', async (p) => {
    const canonical = buildCommitmentCanonical(goldenVectors.drawId, p.participantId, p.number, p.secretHex)
    await expect(sha256Hex(canonical)).resolves.toBe(p.commitmentHash)
  })

  it('builds the revealed-data canonical string exactly like the backend', () => {
    const reveals = goldenVectors.participants.map((p) => ({ participantId: p.participantId, number: p.number }))
    const canonical = buildRevealedDataCanonical(goldenVectors.drawId, reveals)
    expect(canonical).toBe(goldenVectors.resultCanonical)
  })

  it('hashes the revealed-data canonical string to the expected resultHash', async () => {
    const reveals = goldenVectors.participants.map((p) => ({ participantId: p.participantId, number: p.number }))
    const canonical = buildRevealedDataCanonical(goldenVectors.drawId, reveals)
    await expect(sha256Hex(canonical)).resolves.toBe(goldenVectors.resultHash)
  })

  it('is insensitive to input order (sorts by participantId before joining)', () => {
    const reveals = goldenVectors.participants.map((p) => ({ participantId: p.participantId, number: p.number }))
    const shuffled = [...reveals].reverse()
    expect(buildRevealedDataCanonical(goldenVectors.drawId, shuffled)).toBe(buildRevealedDataCanonical(goldenVectors.drawId, reveals))
  })

  it.each([
    ['Roma', true],
    ['Vicente|Juan', false],
    ['', true],
  ])('isSafeForCanonical(%s) === %s', (value, expected) => {
    expect(isSafeForCanonical(value)).toBe(expected)
  })
})
