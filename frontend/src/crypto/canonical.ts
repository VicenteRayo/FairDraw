/**
 * Exact, unambiguous canonical string formats used throughout the Commit-Reveal protocol.
 * These MUST match, byte for byte, the C# implementation in
 * backend/src/FairDraw.Crypto/CanonicalFormats.cs — see test-vectors/canonical-vectors.json for
 * the shared golden vectors both sides are tested against.
 *
 * Global rules:
 *  - every canonical string is UTF-8 encoded before hashing with SHA-256;
 *  - every hash is represented as lowercase hex, no "0x" prefix;
 *  - "drawId" always means the short public code (e.g. "8F4K92LM"), never an internal database id;
 *  - option and participant names must never contain '|' so the pipe-joined string can always be
 *    reconstructed unambiguously (validated server-side at creation time).
 */

export const PROTOCOL_VERSION = 1

/** SHA-256 of the UTF-8 bytes of `input`, as lowercase hex. Uses window.crypto.subtle only. */
export async function sha256Hex(input: string): Promise<string> {
  const bytes = new TextEncoder().encode(input)
  const digest = await crypto.subtle.digest('SHA-256', bytes)
  return bytesToHex(new Uint8Array(digest))
}

export function bytesToHex(bytes: Uint8Array): string {
  return Array.from(bytes)
    .map((b) => b.toString(16).padStart(2, '0'))
    .join('')
}

/** True if the value contains no character that would break canonical parsing. */
export function isSafeForCanonical(value: string): boolean {
  return !value.includes('|')
}

/**
 * "{drawId}|{OPTION_1}|...|{OPTION_M}|{PARTICIPANT_1}|...|{PARTICIPANT_N}"
 * Options and participants must already be in ascending SortOrder as fixed at Lock time.
 */
export function buildConfigurationCanonical(
  drawId: string,
  optionsInOrder: string[],
  participantsInOrder: string[],
): string {
  return [drawId, ...optionsInOrder, ...participantsInOrder].join('|')
}

/**
 * "version=1|drawId={drawId}|participantId={participantId}|number={number}|secret={secretHex}"
 * participantId is the participant's GUID exactly as issued by the API (36 chars, lowercase,
 * hyphenated). number is a decimal integer 1-10 with no leading zeros. secretHex is 64 lowercase
 * hex chars (32 bytes).
 */
export function buildCommitmentCanonical(
  drawId: string,
  participantId: string,
  number: number,
  secretHex: string,
): string {
  return `version=${PROTOCOL_VERSION}|drawId=${drawId}|participantId=${participantId}|number=${number}|secret=${secretHex}`
}

export interface RevealedEntry {
  participantId: string
  number: number
}

/**
 * "version=1|drawId={drawId}|reveals={pid1}:{num1},{pid2}:{num2},...,{pidN}:{numN}"
 * Reveals are sorted by participantId using a plain ordinal string comparison — this is exactly
 * what JavaScript's default Array.sort() does for ASCII strings like GUIDs, which is why the C#
 * side explicitly uses StringComparer.Ordinal to match it.
 */
export function buildRevealedDataCanonical(drawId: string, reveals: RevealedEntry[]): string {
  const ordered = [...reveals].sort((a, b) => (a.participantId < b.participantId ? -1 : a.participantId > b.participantId ? 1 : 0))
  const joined = ordered.map((r) => `${r.participantId}:${r.number}`).join(',')
  return `version=${PROTOCOL_VERSION}|drawId=${drawId}|reveals=${joined}`
}
