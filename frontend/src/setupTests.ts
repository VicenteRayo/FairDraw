import '@testing-library/jest-dom'
import { webcrypto } from 'node:crypto'

// jsdom's own `crypto` implementation does not reliably provide SubtleCrypto across versions;
// Node's built-in WebCrypto implementation does, and is API-compatible with the browser's
// window.crypto.subtle / crypto.getRandomValues used throughout src/crypto.
if (!globalThis.crypto?.subtle) {
  Object.defineProperty(globalThis, 'crypto', { value: webcrypto, configurable: true })
}
