/**
 * localStorage-backed persistence for the participant's own secret and the creator's token.
 * Per the spec (§6): "Guarda este dispositivo hasta que termine el sorteo. Tu secreto es
 * necesario para verificar tu elección." — the number/secret never leave the browser except
 * during the reveal submission itself.
 */

export interface ParticipantIdentity {
  drawId: string
  participantId: string
  participantSecret: string
  name: string
  /** Present once the participant has generated their own number/secret, before committing. */
  number?: number
  secret?: string
}

export interface CreatorIdentity {
  drawId: string
  creatorToken: string
}

function participantKey(drawId: string): string {
  return `fairdraw:draw:${drawId}:participant`
}

function creatorKey(drawId: string): string {
  return `fairdraw:draw:${drawId}:creator`
}

export function saveParticipantIdentity(identity: ParticipantIdentity): void {
  localStorage.setItem(participantKey(identity.drawId), JSON.stringify(identity))
}

export function loadParticipantIdentity(drawId: string): ParticipantIdentity | null {
  const raw = localStorage.getItem(participantKey(drawId))
  if (!raw) return null
  try {
    return JSON.parse(raw) as ParticipantIdentity
  } catch {
    return null
  }
}

export function saveCreatorIdentity(identity: CreatorIdentity): void {
  localStorage.setItem(creatorKey(identity.drawId), JSON.stringify(identity))
}

export function loadCreatorIdentity(drawId: string): CreatorIdentity | null {
  const raw = localStorage.getItem(creatorKey(drawId))
  if (!raw) return null
  try {
    return JSON.parse(raw) as CreatorIdentity
  } catch {
    return null
  }
}

/**
 * Parses and validates an imported backup file against the CURRENT page's draw/participant, so a
 * backup for a different draw or a different person can't silently overwrite the wrong identity.
 */
export function parseParticipantBackup(json: string, expectedDrawId: string, expectedParticipantId: string): ParticipantIdentity {
  const data = JSON.parse(json) as Partial<ParticipantIdentity>
  if (
    typeof data.drawId !== 'string' ||
    typeof data.participantId !== 'string' ||
    typeof data.participantSecret !== 'string' ||
    typeof data.name !== 'string'
  ) {
    throw new Error('invalid_backup_format')
  }
  if (data.drawId !== expectedDrawId || data.participantId !== expectedParticipantId) {
    throw new Error('backup_does_not_match_this_draw_participant')
  }
  return data as ParticipantIdentity
}
