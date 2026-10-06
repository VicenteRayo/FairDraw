import type {
  ClaimResponse,
  CommitResponse,
  CreateDrawResponse,
  DrawDto,
  RevealResponse,
  VerificationResponse,
} from './types'

const BASE_URL = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? 'http://localhost:5080/api/v1'

export class ApiError extends Error {
  readonly status: number
  readonly code: string

  constructor(status: number, code: string) {
    super(code)
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...init?.headers,
    },
  })

  if (!response.ok) {
    let code = 'unknown_error'
    try {
      const body = (await response.json()) as { error?: string }
      code = body.error ?? code
    } catch {
      // Response had no JSON body; keep the generic code.
    }
    throw new ApiError(response.status, code)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

function authHeader(token: string): HeadersInit {
  return { Authorization: `Bearer ${token}` }
}

export function createDraw(name: string, options: string[], participants: string[]): Promise<CreateDrawResponse> {
  return request('/draws', { method: 'POST', body: JSON.stringify({ name, options, participants }) })
}

export function getDraw(code: string): Promise<DrawDto> {
  return request(`/draws/${code}`)
}

export function patchDraw(
  code: string,
  creatorToken: string,
  name: string,
  options: string[],
  participants: string[],
): Promise<DrawDto> {
  return request(`/draws/${code}`, {
    method: 'PATCH',
    headers: authHeader(creatorToken),
    body: JSON.stringify({ name, options, participants }),
  })
}

export function lockDraw(code: string, creatorToken: string): Promise<DrawDto> {
  return request(`/draws/${code}/lock`, { method: 'POST', headers: authHeader(creatorToken) })
}

export function cancelDraw(code: string, creatorToken: string): Promise<DrawDto> {
  return request(`/draws/${code}/cancel`, { method: 'POST', headers: authHeader(creatorToken) })
}

export function claimParticipant(code: string, participantId: string): Promise<ClaimResponse> {
  return request(`/draws/${code}/participants/${participantId}/claim`, { method: 'POST' })
}

/**
 * Sends the commitment together with the number/secret it was computed from. The server checks
 * they match, keeps them hidden until everyone has committed, and resolves the draw as soon as the
 * last participant commits — so nobody has to come back to reveal.
 */
export function commitParticipant(
  code: string,
  participantId: string,
  participantSecret: string,
  commitment: string,
  number: number,
  secret: string,
): Promise<CommitResponse> {
  return request(`/draws/${code}/participants/${participantId}/commitments`, {
    method: 'POST',
    headers: authHeader(participantSecret),
    body: JSON.stringify({ commitment, number, secret }),
  })
}

/**
 * Note: a mismatched reveal is a normal, expected outcome (wrong number/secret), not a network
 * failure — the backend returns 422 for it, which this function resolves to `{ valid: false }`
 * rather than throwing, so callers don't have to special-case ApiError for this one status code.
 */
export async function revealParticipant(
  code: string,
  participantId: string,
  participantSecret: string,
  number: number,
  secret: string,
): Promise<RevealResponse> {
  try {
    return await request(`/draws/${code}/participants/${participantId}/reveals`, {
      method: 'POST',
      headers: authHeader(participantSecret),
      body: JSON.stringify({ number, secret }),
    })
  } catch (err) {
    if (err instanceof ApiError && err.status === 422) {
      return { participantId, valid: false, revealedAt: new Date().toISOString() }
    }
    throw err
  }
}

export function getVerification(code: string): Promise<VerificationResponse> {
  return request(`/draws/${code}/verification`)
}
