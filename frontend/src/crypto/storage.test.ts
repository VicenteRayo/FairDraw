import { beforeEach, describe, expect, it } from 'vitest'
import { loadCreatorIdentity, loadParticipantIdentity, parseParticipantBackup, saveCreatorIdentity, saveParticipantIdentity } from './storage'

beforeEach(() => {
  localStorage.clear()
})

describe('participant identity roundtrip', () => {
  it('saves and loads the exact identity back', () => {
    saveParticipantIdentity({ drawId: '8F4K92LM', participantId: 'p1', participantSecret: 'secret', name: 'Ana' })

    expect(loadParticipantIdentity('8F4K92LM')).toEqual({
      drawId: '8F4K92LM',
      participantId: 'p1',
      participantSecret: 'secret',
      name: 'Ana',
    })
  })

  it('returns null for a draw with no stored identity', () => {
    expect(loadParticipantIdentity('UNKNOWN')).toBeNull()
  })

  it('keeps identities for different draws independent', () => {
    saveParticipantIdentity({ drawId: 'DRAW1', participantId: 'p1', participantSecret: 's1', name: 'Ana' })
    saveParticipantIdentity({ drawId: 'DRAW2', participantId: 'p2', participantSecret: 's2', name: 'Bruno' })

    expect(loadParticipantIdentity('DRAW1')?.name).toBe('Ana')
    expect(loadParticipantIdentity('DRAW2')?.name).toBe('Bruno')
  })
})

describe('creator identity roundtrip', () => {
  it('saves and loads the exact identity back', () => {
    saveCreatorIdentity({ drawId: '8F4K92LM', creatorToken: 'tok' })
    expect(loadCreatorIdentity('8F4K92LM')).toEqual({ drawId: '8F4K92LM', creatorToken: 'tok' })
  })
})

describe('parseParticipantBackup', () => {
  const valid = { drawId: '8F4K92LM', participantId: 'p1', participantSecret: 'secret', name: 'Ana' }

  it('accepts a backup matching the current draw/participant', () => {
    const result = parseParticipantBackup(JSON.stringify(valid), '8F4K92LM', 'p1')
    expect(result).toEqual(valid)
  })

  it('rejects a backup for a different draw', () => {
    expect(() => parseParticipantBackup(JSON.stringify(valid), 'OTHERDRAW', 'p1')).toThrow()
  })

  it('rejects a backup for a different participant', () => {
    expect(() => parseParticipantBackup(JSON.stringify(valid), '8F4K92LM', 'p2')).toThrow()
  })

  it('rejects malformed JSON content', () => {
    expect(() => parseParticipantBackup(JSON.stringify({ foo: 'bar' }), '8F4K92LM', 'p1')).toThrow()
  })
})
