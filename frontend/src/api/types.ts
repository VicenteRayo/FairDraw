// Mirrors backend/src/FairDraw.Api/Dtos/DrawDtos.cs — keep both in sync.

export type DrawState = 'Created' | 'Open' | 'Committing' | 'Revealing' | 'Completed' | 'Cancelled'

export interface OptionDto {
  id: string
  text: string
  sortOrder: number
}

export interface RevealSummaryDto {
  number: number
  secretHex: string
  revealedAt: string
}

export interface ParticipantDto {
  id: string
  name: string
  sortOrder: number
  claimed: boolean
  hasCommitted: boolean
  reveal: RevealSummaryDto | null
}

export interface ResultDto {
  resultHash: string
  attemptsUsed: number
  winnerIndex: number
  winnerOptionId: string
  winnerOptionText: string | null
}

export interface DrawDto {
  drawId: string
  name: string
  state: DrawState
  configurationHash: string | null
  options: OptionDto[]
  participants: ParticipantDto[]
  commitCount: number
  participantCount: number
  result: ResultDto | null
  /** ISO date-time. The draw (and its result, if any) stops being available 30 days after creation. */
  expiresAt: string
}

export interface CreateDrawResponse {
  drawId: string
  state: DrawState
  creatorToken: string
  draw: DrawDto
}

export interface ClaimResponse {
  participantId: string
  participantSecret: string
  claimedAt: string
}

export interface CommitResponse {
  participantId: string
  committedAt: string
}

export interface RevealResponse {
  participantId: string
  valid: boolean
  revealedAt: string
}

export interface VerificationParticipantDto {
  id: string
  name: string
  sortOrder: number
}

export interface VerificationCommitmentDto {
  participantId: string
  commitmentHash: string
  committedAt: string
}

export interface VerificationRevealDto {
  participantId: string
  number: number
  secretHex: string
  revealedAt: string
}

export interface VerificationResponse {
  protocolVersion: number
  drawId: string
  state: DrawState
  configurationHash: string | null
  options: OptionDto[]
  participants: VerificationParticipantDto[]
  commitments: VerificationCommitmentDto[]
  reveals: VerificationRevealDto[]
  result: ResultDto | null
  expiresAt: string
}
