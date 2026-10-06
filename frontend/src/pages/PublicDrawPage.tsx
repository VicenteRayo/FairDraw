import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  ApiError,
  cancelDraw,
  claimParticipant,
  commitParticipant,
  getDraw,
  lockDraw,
  revealParticipant,
} from '../api/client'
import type { DrawDto } from '../api/types'
import { computeCommitment, generateSecretHex } from '../crypto/crypto'
import {
  loadCreatorIdentity,
  loadParticipantIdentity,
  parseParticipantBackup,
  saveParticipantIdentity,
  type ParticipantIdentity,
} from '../crypto/storage'
import DrawReveal from '../components/DrawReveal'

const POLL_INTERVAL_MS = 2000
const TERMINAL_STATES = new Set(['Completed', 'Cancelled'])

/** Full public URL for a draw, respecting the app's base path (e.g. "/proyectos/fairdraw/" in production). */
function buildShareUrl(code: string): string {
  const base = import.meta.env.BASE_URL.replace(/\/$/, '')
  return `${window.location.origin}${base}/draw/${code}`
}

function formatExpiry(isoDate: string): string {
  return new Date(isoDate).toLocaleDateString('es-ES', { day: 'numeric', month: 'long', year: 'numeric' })
}

export default function PublicDrawPage() {
  const { code = '' } = useParams()
  const [draw, setDraw] = useState<DrawDto | null>(null)
  const [notFound, setNotFound] = useState(false)
  const [participant, setParticipant] = useState<ParticipantIdentity | null>(() => loadParticipantIdentity(code))
  const [error, setError] = useState<string | null>(null)
  const revealAttempted = useRef(false)

  const creator = loadCreatorIdentity(code)

  const refresh = useCallback(async () => {
    try {
      const latest = await getDraw(code)
      setDraw(latest)
    } catch (err) {
      if (err instanceof ApiError && err.status === 404) setNotFound(true)
    }
  }, [code])

  useEffect(() => {
    refresh()
    const interval = setInterval(() => {
      setDraw((current) => {
        if (current && TERMINAL_STATES.has(current.state)) return current
        refresh()
        return current
      })
    }, POLL_INTERVAL_MS)
    return () => clearInterval(interval)
  }, [refresh])

  // Only for draws that ended up in Revealing (committed before the reveal was sent along with the
  // commitment): auto-reveal as soon as we hold our own number/secret.
  useEffect(() => {
    if (!draw || draw.state !== 'Revealing' || !participant?.number || !participant.secret) return
    const mine = draw.participants.find((p) => p.id === participant.participantId)
    if (mine?.reveal || revealAttempted.current) return

    revealAttempted.current = true
    revealParticipant(code, participant.participantId, participant.participantSecret, participant.number, participant.secret)
      .then(() => refresh())
      .catch(() => setError('No se ha podido enviar tu revelación automáticamente.'))
  }, [draw, participant, code, refresh])

  if (notFound) {
    return (
      <div className="page page-centered">
        <p>Sorteo no encontrado.</p>
        <Link to="/">Volver al inicio</Link>
      </div>
    )
  }

  if (!draw) {
    return (
      <div className="page page-centered">
        <p>Cargando…</p>
      </div>
    )
  }

  async function handleClaim(participantId: string, name: string) {
    setError(null)
    try {
      const claim = await claimParticipant(code, participantId)
      const identity: ParticipantIdentity = {
        drawId: code,
        participantId,
        participantSecret: claim.participantSecret,
        name,
      }
      saveParticipantIdentity(identity)
      setParticipant(identity)
      await refresh()
    } catch {
      setError('Ese nombre ya ha sido reclamado por otra persona, o el sorteo no admite reclamaciones ahora mismo.')
    }
  }

  async function handleCommit(number: number) {
    if (!participant) return
    setError(null)
    try {
      const secret = generateSecretHex()
      const commitment = await computeCommitment(code, participant.participantId, number, secret)
      await commitParticipant(code, participant.participantId, participant.participantSecret, commitment, number, secret)
      const updated: ParticipantIdentity = { ...participant, number, secret }
      saveParticipantIdentity(updated)
      setParticipant(updated)
      await refresh()
    } catch {
      setError('No se ha podido registrar tu elección.')
    }
  }

  async function handleLock() {
    if (!creator) return
    setError(null)
    try {
      await lockDraw(code, creator.creatorToken)
      await refresh()
    } catch {
      setError('No se ha podido bloquear el sorteo (¿tiene al menos 2 opciones y 2 participantes?).')
    }
  }

  async function handleCancel() {
    if (!creator) return
    if (!confirm('¿Seguro que quieres cancelar este sorteo?')) return
    setError(null)
    try {
      await cancelDraw(code, creator.creatorToken)
      await refresh()
    } catch {
      setError('No se ha podido cancelar el sorteo.')
    }
  }

  function handleImportBackup(file: File) {
    if (!participant) return
    file.text().then((text) => {
      try {
        const imported = parseParticipantBackup(text, code, participant.participantId)
        saveParticipantIdentity(imported)
        setParticipant(imported)
      } catch {
        setError('El archivo de copia de seguridad no corresponde a este sorteo/participante.')
      }
    })
  }

  const shareUrl = buildShareUrl(code)

  return (
    <div className="page">
      <header className="draw-header">
        <h1>{draw.name.toUpperCase()}</h1>
        <span className={`badge badge-${draw.state.toLowerCase()}`}>{stateLabel(draw.state)}</span>
      </header>
      <p className="hint">Disponible hasta el {formatExpiry(draw.expiresAt)} (30 días desde su creación).</p>

      {error && <p className="error">{error}</p>}

      {creator && draw.state === 'Created' && (
        <section className="card">
          <p>Opciones y participantes configurados. Bloquea el sorteo para empezar a compartirlo.</p>
          <button className="btn btn-primary" onClick={handleLock}>
            Bloquear sorteo
          </button>
        </section>
      )}

      {creator && !TERMINAL_STATES.has(draw.state) && draw.state !== 'Created' && (
        <section className="card card-muted">
          <p>Enlace público: <code>{shareUrl}</code></p>
          <button className="btn btn-link" onClick={() => navigator.clipboard.writeText(shareUrl)}>
            📋 Copiar enlace
          </button>
          <button className="btn btn-danger" onClick={handleCancel}>
            Cancelar sorteo
          </button>
        </section>
      )}

      {draw.state === 'Created' && !creator && <p>El creador todavía está preparando el sorteo.</p>}

      {(draw.state === 'Open' || draw.state === 'Committing') && !participant && (
        <ClaimList draw={draw} onClaim={handleClaim} />
      )}

      {(draw.state === 'Open' || draw.state === 'Committing') && participant && (
        <CommitPanel draw={draw} participant={participant} onCommit={handleCommit} />
      )}

      {draw.state === 'Revealing' && (
        <RevealPanel draw={draw} participant={participant} onImportBackup={handleImportBackup} />
      )}

      {draw.state === 'Completed' && draw.result && <ResultBanner draw={draw} code={code} />}

      {draw.state === 'Completed' && (
        <footer className="page-footer">
          <Link className="btn btn-primary" to="/">
            🎲 Crear un nuevo sorteo
          </Link>
        </footer>
      )}

      {draw.state === 'Cancelled' && <p className="notice">❌ Este sorteo ha sido cancelado.</p>}
    </div>
  )
}

function stateLabel(state: DrawDto['state']): string {
  switch (state) {
    case 'Created':
      return 'Preparando'
    case 'Open':
      return 'Abierto'
    case 'Committing':
      return 'Comprometiendo'
    case 'Revealing':
      return 'Revelando'
    case 'Completed':
      return 'Completado'
    case 'Cancelled':
      return 'Cancelado'
  }
}

function ClaimList({ draw, onClaim }: { draw: DrawDto; onClaim: (id: string, name: string) => void }) {
  return (
    <section className="card">
      <h2>¿Quién eres?</h2>
      <p>Pulsa tu nombre en la lista para participar.</p>
      <ul className="participant-list">
        {draw.participants.map((p) => (
          <li key={p.id}>
            <span>{p.name}</span>
            {p.claimed ? (
              <span className="tag">reclamado</span>
            ) : (
              <button className="btn btn-secondary" onClick={() => onClaim(p.id, p.name)}>
                Soy yo
              </button>
            )}
          </li>
        ))}
      </ul>
    </section>
  )
}

function CommitPanel({
  draw,
  participant,
  onCommit,
}: {
  draw: DrawDto
  participant: ParticipantIdentity
  onCommit: (n: number) => void
}) {
  const mine = draw.participants.find((p) => p.id === participant.participantId)
  const hasCommitted = mine?.hasCommitted ?? false

  if (hasCommitted) {
    return (
      <section className="card">
        <p className="big">🔒 ELECCIÓN REGISTRADA</p>
        <p>Tu elección está protegida criptográficamente.</p>
        <p>Esperando al resto…</p>
        <p className="progress">
          {draw.commitCount} / {draw.participantCount} participantes
        </p>
        <p className="hint">No hace falta que vuelvas a entrar: el sorteo se resolverá solo cuando elija el último participante.</p>
      </section>
    )
  }

  return (
    <section className="card">
      <p>
        Participante: <strong>{participant.name}</strong>
      </p>
      <p>Elige un número del 1 al 10</p>
      <div className="number-grid">
        {Array.from({ length: 10 }, (_, i) => i + 1).map((n) => (
          <button key={n} className="btn btn-number" onClick={() => onCommit(n)}>
            {n}
          </button>
        ))}
      </div>
      <p className="hint">Cuando elija el último participante, el sorteo se resolverá automáticamente y cualquiera podrá ver el resultado en este enlace.</p>
    </section>
  )
}

function RevealPanel({
  draw,
  participant,
  onImportBackup,
}: {
  draw: DrawDto
  participant: ParticipantIdentity | null
  onImportBackup: (file: File) => void
}) {
  const revealedCount = draw.participants.filter((p) => p.reveal).length
  const mine = participant ? draw.participants.find((p) => p.id === participant.participantId) : undefined

  return (
    <section className="card">
      <p className="big">🔓 REVELANDO ELECCIONES</p>
      <p className="progress">
        {revealedCount} / {draw.participantCount} han revelado
      </p>
      {participant?.number && participant.secret ? (
        <p>{mine?.reveal ? '✅ Tu elección ha sido verificada.' : 'Enviando tu revelación automáticamente…'}</p>
      ) : (
        <div>
          <p>Este dispositivo no tiene tu secreto guardado. Importa tu copia de seguridad para poder revelar tu elección:</p>
          <input
            type="file"
            accept="application/json"
            onChange={(e) => {
              const file = e.target.files?.[0]
              if (file) onImportBackup(file)
            }}
          />
        </div>
      )}
    </section>
  )
}

function ResultBanner({ draw, code }: { draw: DrawDto; code: string }) {
  const result = draw.result!
  const shareUrl = buildShareUrl(code)
  const shareText = `🎲 Hemos realizado el sorteo "${draw.name}".\n\n🏆 Resultado: ${result.winnerOptionText}\n\n🔐 Sorteo verificable:\n${shareUrl}\n\nCualquiera puede comprobar que el resultado es correcto.`

  function share() {
    if (navigator.share) {
      navigator.share({ text: shareText }).catch(() => {})
    } else {
      window.open(`https://wa.me/?text=${encodeURIComponent(shareText)}`, '_blank')
    }
  }

  const orderedOptions = [...draw.options].sort((a, b) => a.sortOrder - b.sortOrder)

  return (
    <DrawReveal drawId={draw.drawId} options={orderedOptions} winnerOptionId={result.winnerOptionId}>
      <section className="card card-result">
        <p className="big">🎉 SORTEO COMPLETADO</p>
        <p>El resultado elegido es:</p>
        <p className="winner">{result.winnerOptionText}</p>

        <h3>Números utilizados</h3>
        <ul className="reveal-list">
          {draw.participants.map((p) => (
            <li key={p.id}>
              {p.name} → {p.reveal?.number ?? '—'}
            </li>
          ))}
        </ul>

        <p className="hint">
          Hash del resultado:
          <br />
          <code>{result.resultHash}</code>
        </p>

        <div className="stack">
          <Link className="btn btn-secondary" to={`/draw/${code}/verify`}>
            🔍 Verificar sorteo
          </Link>
          <button className="btn btn-secondary" onClick={() => navigator.clipboard.writeText(shareText)}>
            📋 Copiar resultado
          </button>
          <button className="btn btn-primary" onClick={share}>
            📤 Compartir en WhatsApp
          </button>
        </div>
      </section>
    </DrawReveal>
  )
}
