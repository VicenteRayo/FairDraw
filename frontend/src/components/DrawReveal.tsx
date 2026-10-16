import { useEffect, useRef, useState, type ReactNode } from 'react'
import type { OptionDto } from '../api/types'

/**
 * Presentación del resultado: un resaltado salta de carta en carta, frena hasta caer en la opción
 * ganadora y lanza confeti. Es puramente visual — el ganador ya viene calculado por el backend.
 * La animación se reproduce una sola vez por sorteo y pestaña; al recargar se muestra el estado final.
 */

const CONFETTI_COLORS = ['#e2432f', '#f6c22f', '#2f77c6', '#4c9c44', '#ef8a2a', '#e86d9a']

function seenKey(drawId: string): string {
  return `fairdraw:revealed:${drawId}`
}

function alreadySeen(drawId: string): boolean {
  try {
    return sessionStorage.getItem(seenKey(drawId)) === '1'
  } catch {
    return false
  }
}

function markSeen(drawId: string) {
  try {
    sessionStorage.setItem(seenKey(drawId), '1')
  } catch {
    // Sin almacenamiento disponible: la animación se repetirá al recargar, sin más consecuencias.
  }
}

function prefersReducedMotion(): boolean {
  return window.matchMedia?.('(prefers-reduced-motion: reduce)').matches === true
}

/** Lanza confeti en el canvas y devuelve una función para detenerlo. */
function launchConfetti(canvas: HTMLCanvasElement): () => void {
  const ctx = canvas.getContext('2d')
  if (!ctx) return () => {}
  canvas.width = window.innerWidth
  canvas.height = window.innerHeight

  const pieces = Array.from({ length: 120 }, () => ({
    x: window.innerWidth / 2 + (Math.random() - 0.5) * 120,
    y: window.innerHeight * 0.4,
    vx: (Math.random() - 0.5) * 14,
    vy: -Math.random() * 14 - 4,
    size: Math.random() * 6 + 4,
    angle: Math.random() * 6,
    spin: (Math.random() - 0.5) * 0.4,
    color: CONFETTI_COLORS[Math.floor(Math.random() * CONFETTI_COLORS.length)],
  }))

  let frame = 0
  let rafId = 0
  const tick = () => {
    ctx.clearRect(0, 0, canvas.width, canvas.height)
    for (const p of pieces) {
      p.vy += 0.45
      p.vx *= 0.99
      p.x += p.vx
      p.y += p.vy
      p.angle += p.spin
      ctx.save()
      ctx.translate(p.x, p.y)
      ctx.rotate(p.angle)
      ctx.fillStyle = p.color
      ctx.strokeStyle = '#34302c'
      ctx.lineWidth = 1.5
      ctx.fillRect(-p.size / 2, -p.size / 3, p.size, p.size / 1.5)
      ctx.strokeRect(-p.size / 2, -p.size / 3, p.size, p.size / 1.5)
      ctx.restore()
    }
    if (++frame < 150) rafId = requestAnimationFrame(tick)
    else ctx.clearRect(0, 0, canvas.width, canvas.height)
  }
  rafId = requestAnimationFrame(tick)

  return () => {
    cancelAnimationFrame(rafId)
    ctx.clearRect(0, 0, canvas.width, canvas.height)
  }
}

export default function DrawReveal({
  drawId,
  options,
  winnerOptionId,
  children,
}: {
  drawId: string
  /** Opciones ya ordenadas por sortOrder. */
  options: OptionDto[]
  winnerOptionId: string
  /** Contenido que aparece cuando termina la animación (el acta del resultado). */
  children: ReactNode
}) {
  const winner = options.findIndex((o) => o.id === winnerOptionId)
  const [skipAnimation] = useState(() => winner < 0 || prefersReducedMotion() || alreadySeen(drawId))
  const [lit, setLit] = useState<number | null>(null)
  const [done, setDone] = useState(skipAnimation)
  const canvasRef = useRef<HTMLCanvasElement>(null)

  useEffect(() => {
    if (skipAnimation) return

    let cancelled = false
    let timer: ReturnType<typeof setTimeout> | undefined
    let stopConfetti: (() => void) | undefined

    // Tres vueltas completas y luego hasta la ganadora, cada salto un poco más lento.
    const count = options.length
    const hops = Array.from({ length: count * 3 + winner + 1 }, (_, i) => i % count)
    let k = 0

    const step = () => {
      if (cancelled) return
      if (k < hops.length) {
        setLit(hops[k])
        const delay = 70 + Math.pow(k / hops.length, 3) * 420
        k++
        timer = setTimeout(step, delay)
        return
      }
      setLit(null)
      setDone(true)
      markSeen(drawId)
      if (canvasRef.current) stopConfetti = launchConfetti(canvasRef.current)
    }
    step()

    return () => {
      cancelled = true
      clearTimeout(timer)
      stopConfetti?.()
    }
  }, [skipAnimation, options.length, winner, drawId])

  return (
    <>
      <div className="reveal">
        <div className={`deck${done ? ' deck-done' : ''}`}>
          {options.map((option, i) => {
            const classes = ['deck-card']
            if (lit === i) classes.push('deck-card-lit')
            if (done && i === winner) classes.push('deck-card-win')
            return (
              <div key={option.id} className={classes.join(' ')}>
                <span className="deck-card-index">#{i}</span>
                <span className="deck-card-text">{option.text}</span>
                {done && i === winner && <span className="deck-card-badge">¡Ganadora!</span>}
              </div>
            )
          })}
        </div>
        <p className="reveal-status" aria-live="polite">
          {done ? '' : 'Barajando…'}
        </p>
      </div>

      {done && children}

      <canvas ref={canvasRef} className="confetti" aria-hidden="true" />
    </>
  )
}
