import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ApiError, getVerification } from '../api/client'
import type { VerificationResponse } from '../api/types'
import { buildConfigurationCanonical, computeCommitment, computeResultHash, sha256Hex } from '../crypto/crypto'
import { selectWinnerIndex } from '../crypto/rejectionSampling'

type CheckStatus = 'ok' | 'fail' | 'pending'

interface Check {
  label: string
  status: CheckStatus
  detail?: string
}

export default function VerifyPage() {
  const { code = '' } = useParams()
  const [data, setData] = useState<VerificationResponse | null>(null)
  const [checks, setChecks] = useState<Check[] | null>(null)
  const [notFound, setNotFound] = useState(false)

  useEffect(() => {
    getVerification(code)
      .then(async (verification) => {
        setData(verification)
        setChecks(await runVerification(verification))
      })
      .catch((err) => {
        if (err instanceof ApiError && err.status === 404) setNotFound(true)
      })
  }, [code])

  if (notFound) {
    return (
      <div className="page page-centered">
        <p>Sorteo no encontrado.</p>
        <Link to="/">Volver al inicio</Link>
      </div>
    )
  }

  if (!data || !checks) {
    return (
      <div className="page page-centered">
        <p>Comprobando…</p>
      </div>
    )
  }

  const allOk = checks.every((c) => c.status === 'ok')
  const anyFail = checks.some((c) => c.status === 'fail')
  const verdict = data.state === 'Completed' ? (allOk ? 'verified' : anyFail ? 'invalid' : 'pending') : 'pending'

  return (
    <div className="page">
      <div className="page-head">
        <Link to={`/draw/${code}`} className="back-link" aria-label="Volver al sorteo">
          ←
        </Link>
        <h1>Verificación del sorteo</h1>
      </div>

      <section className="card">
        <h2>{data.drawId}</h2>
        <p className="hint">
          Disponible hasta el {new Date(data.expiresAt).toLocaleDateString('es-ES', { day: 'numeric', month: 'long', year: 'numeric' })} (30 días desde su creación).
        </p>
        <p>
          Opciones:
          <br />
          {data.options.map((o) => (
            <span key={o.id}>
              {o.sortOrder + 1}. {o.text}
              <br />
            </span>
          ))}
        </p>
        <p>
          Participantes:
          <br />
          {data.participants.map((p) => (
            <span key={p.id}>
              {p.name}
              <br />
            </span>
          ))}
        </p>
      </section>

      <section className="card">
        <h2>Comprobaciones</h2>
        <ul className="checklist">
          {checks.map((c, i) => (
            <li key={i} className={`check-${c.status}`}>
              {c.status === 'ok' ? '✅' : c.status === 'fail' ? '❌' : '⏳'} {c.label}
              {c.detail && <div className="hint">{c.detail}</div>}
            </li>
          ))}
        </ul>
      </section>

      {verdict === 'verified' && <p className="notice notice-success">✅ SORTEO VERIFICADO</p>}
      {verdict === 'invalid' && <p className="notice notice-error">❌ DATOS NO VÁLIDOS</p>}
      {verdict === 'pending' && <p className="notice">El sorteo aún no ha terminado; algunas comprobaciones están pendientes.</p>}

      {data.result && (
        <section className="card">
          <p>Hash del resultado: <code>{data.result.resultHash}</code></p>
          <p>Índice ganador: {data.result.winnerIndex}</p>
          <p>Opción ganadora: {data.result.winnerOptionText}</p>
        </section>
      )}
    </div>
  )
}

async function runVerification(v: VerificationResponse): Promise<Check[]> {
  const checks: Check[] = []
  const orderedOptions = [...v.options].sort((a, b) => a.sortOrder - b.sortOrder)
  const orderedParticipants = [...v.participants].sort((a, b) => a.sortOrder - b.sortOrder)

  // 1. Configuration hash: proves options/participants were not altered after Lock.
  if (v.configurationHash) {
    const canonical = buildConfigurationCanonical(
      v.drawId,
      orderedOptions.map((o) => o.text),
      orderedParticipants.map((p) => p.name),
    )
    const recomputed = await sha256Hex(canonical)
    checks.push({
      label: 'La configuración (opciones y participantes) no fue modificada tras el bloqueo',
      status: recomputed === v.configurationHash ? 'ok' : 'fail',
    })
  } else {
    checks.push({ label: 'Configuración bloqueada', status: 'pending', detail: 'El sorteo aún no ha sido bloqueado.' })
  }

  // 2. Each revealed (number, secret) pair must hash to its published commitment.
  const revealByParticipant = new Map(v.reveals.map((r) => [r.participantId, r]))
  if (v.commitments.length === 0) {
    checks.push({ label: 'Commitments publicados', status: 'pending' })
  } else {
    let allCommitmentsValid = true
    let anyPending = false
    for (const commitment of v.commitments) {
      const reveal = revealByParticipant.get(commitment.participantId)
      if (!reveal) {
        anyPending = true
        continue
      }
      const recomputed = await computeCommitment(v.drawId, reveal.participantId, reveal.number, reveal.secretHex)
      if (recomputed !== commitment.commitmentHash) allCommitmentsValid = false
    }
    checks.push({
      label: 'Todos los números revelados coinciden con su commitment original',
      status: !allCommitmentsValid ? 'fail' : anyPending ? 'pending' : 'ok',
    })
  }

  // 3. Everyone committed must have revealed, for a Completed draw.
  if (v.state === 'Completed') {
    checks.push({
      label: 'Todos los participantes revelaron correctamente',
      status: v.reveals.length === v.participants.length && v.commitments.length === v.participants.length ? 'ok' : 'fail',
    })
  }

  // 4. Result hash + winner index, recomputed independently from the revealed data alone.
  if (v.result) {
    const revealTuples = v.reveals.map((r) => ({ participantId: r.participantId, number: r.number }))
    const recomputedResultHash = await computeResultHash(v.drawId, revealTuples)
    const resultHashOk = recomputedResultHash === v.result.resultHash
    checks.push({ label: 'El hash del resultado coincide', status: resultHashOk ? 'ok' : 'fail' })

    const { winnerIndex, attemptsUsed } = await selectWinnerIndex(v.result.resultHash, orderedOptions.length)
    const indexOk = winnerIndex === v.result.winnerIndex && attemptsUsed === v.result.attemptsUsed
    checks.push({ label: 'El índice ganador coincide (sin sesgo de módulo)', status: indexOk ? 'ok' : 'fail' })

    const expectedOption = orderedOptions[winnerIndex]
    const optionOk = expectedOption?.id === v.result.winnerOptionId
    checks.push({
      label: 'La opción ganadora corresponde al índice calculado',
      status: optionOk ? 'ok' : 'fail',
      detail: expectedOption ? `Opción esperada: ${expectedOption.text}` : undefined,
    })
  } else {
    checks.push({ label: 'Resultado publicado', status: 'pending' })
  }

  return checks
}
