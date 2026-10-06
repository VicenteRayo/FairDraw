// Loads test-vectors/canonical-vectors.json, the single source of truth shared with the
// backend's xUnit suite, so both implementations are checked against the exact same data.
//
// Uses Node's fs/path (via import.meta.url) instead of a static `import` of the JSON file,
// because that file lives outside the Vite project root (frontend/) and a static import would be
// subject to Vite's dev-server file-serving allow-list. This module is test-only — nothing in
// src/ imports it — so it never reaches the browser bundle.
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import path from 'node:path'

export interface ParticipantVector {
  name: string
  participantId: string
  number: number
  secretHex: string
  commitmentCanonical: string
  commitmentHash: string
}

export interface VectorsDocument {
  protocolVersion: number
  drawId: string
  options: string[]
  participantsNames: string[]
  configurationCanonical: string
  configurationHash: string
  participants: ParticipantVector[]
  resultCanonical: string
  resultHash: string
  optionCount: number
  winnerIndex: number
  attemptsUsed: number
  winnerOption: string
}

function loadGoldenVectors(): VectorsDocument {
  const here = path.dirname(fileURLToPath(import.meta.url))
  const filePath = path.resolve(here, '../../../test-vectors/canonical-vectors.json')
  const json = readFileSync(filePath, 'utf-8')
  return JSON.parse(json) as VectorsDocument
}

export const goldenVectors: VectorsDocument = loadGoldenVectors()
