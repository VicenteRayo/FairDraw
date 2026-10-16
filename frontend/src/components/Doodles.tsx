import type { CSSProperties } from 'react'

/**
 * Ilustraciones de feria "dibujadas a mano" (SVG + filtros #wobble definidos en index.html).
 * Son decorativas: todas llevan aria-hidden. Las animaciones viven en index.css y se apagan con
 * prefers-reduced-motion.
 */

const LINE = '#34302c'

/** Degradados de "cera" reutilizables dentro de un mismo <svg>: claro arriba, oscuro abajo. */
function CrayonGradients({ prefix }: { prefix: string }) {
  const stops: Record<string, [string, string, string]> = {
    red: ['#f6735c', '#e2432f', '#c53321'],
    yellow: ['#ffe07a', '#f6c22f', '#e3a512'],
    blue: ['#6aa6e8', '#2f77c6', '#215c9c'],
    green: ['#7cc56d', '#4c9c44', '#367a30'],
    orange: ['#fbb064', '#ef8a2a', '#d26d17'],
    pink: ['#f7a3c2', '#e86d9a', '#cc4f7d'],
  }
  return (
    <>
      {Object.entries(stops).map(([name, [a, b, c]]) => (
        <linearGradient key={name} id={`${prefix}-${name}`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor={a} />
          <stop offset="0.55" stopColor={b} />
          <stop offset="1" stopColor={c} />
        </linearGradient>
      ))}
    </>
  )
}

const GRADIENT_NAMES = ['red', 'yellow', 'blue', 'green', 'orange', 'pink']

/** Guirnalda de banderines que cuelga en lo alto de todas las páginas y se mece con el viento. */
export function Bunting() {
  const flags = 16
  const width = 600
  // Cuerda: curva cuadrática de (0,6) a (600,6) que cae hasta y≈30 en el centro.
  const at = (t: number) => ({ x: t * width, y: 6 + 4 * 24 * t * (1 - t) })

  return (
    <svg className="bunting" viewBox="0 0 600 60" preserveAspectRatio="none" aria-hidden="true">
      <defs>
        <CrayonGradients prefix="bt" />
      </defs>
      <g filter="url(#wobble)" stroke={LINE} strokeWidth="2" strokeLinejoin="round">
        <path d="M0 6 Q300 54 600 6" fill="none" />
        {Array.from({ length: flags }, (_, i) => {
          const a = at((i + 0.15) / flags)
          const b = at((i + 0.85) / flags)
          const tipX = (a.x + b.x) / 2
          const tipY = (a.y + b.y) / 2 + 22
          return (
            <path
              key={i}
              className="flag"
              style={{ animationDelay: `${-i * 0.37}s` } as CSSProperties}
              d={`M${a.x} ${a.y} L${b.x} ${b.y} L${tipX} ${tipY} Z`}
              fill={`url(#bt-${GRADIENT_NAMES[i % GRADIENT_NAMES.length]})`}
            />
          )
        })}
      </g>
    </svg>
  )
}

/** Escena de portada: noria girando detrás y carrusel de rayas delante, sobre un poco de hierba. */
export function FairScene() {
  const wheel = { cx: 92, cy: 78, r: 56 }
  const cabins = 8
  const roof = { apexX: 214, apexY: 46, y: 104, left: 136, right: 292 }
  const wedges = 8
  const wedgeW = (roof.right - roof.left) / wedges

  return (
    <svg className="fair-scene" viewBox="0 0 320 190" role="img" aria-hidden="true">
      <defs>
        <CrayonGradients prefix="fs" />
        <pattern id="hatch" width="6" height="6" patternUnits="userSpaceOnUse" patternTransform="rotate(30)">
          <line x1="0" y1="0" x2="0" y2="6" stroke="#fff" strokeWidth="1.6" strokeOpacity="0.3" />
        </pattern>
        <radialGradient id="fs-sun" cx="0.4" cy="0.35" r="0.7">
          <stop offset="0" stopColor="#fff2a8" />
          <stop offset="0.6" stopColor="#f6c22f" />
          <stop offset="1" stopColor="#ef8a2a" />
        </radialGradient>
        <linearGradient id="fs-cloud" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#ffffff" />
          <stop offset="1" stopColor="#dcebf7" />
        </linearGradient>
        <radialGradient id="fs-ground" cx="0.5" cy="0.4" r="0.6">
          <stop offset="0" stopColor="#fbb064" />
          <stop offset="1" stopColor="#d26d17" />
        </radialGradient>
      </defs>

      <g filter="url(#wobble)" stroke={LINE} strokeWidth="2.2" strokeLinejoin="round" strokeLinecap="round">
        {/* Sol con rayos que giran, y nube que va y viene */}
        <g className="scene-sun-rays" strokeWidth="2" stroke="#ef8a2a">
          {Array.from({ length: 10 }, (_, i) => {
            const a = (i / 10) * Math.PI * 2
            return (
              <line
                key={i}
                x1={290 + Math.cos(a) * 17}
                y1={22 + Math.sin(a) * 17}
                x2={290 + Math.cos(a) * 23}
                y2={22 + Math.sin(a) * 23}
              />
            )
          })}
        </g>
        <circle cx="290" cy="22" r="13" fill="url(#fs-sun)" />
        <path className="scene-cloud" d="M18 30 q6-12 18-6 q8-10 18 0 q10 0 8 10 h-44 q-6-2 0-4z" fill="url(#fs-cloud)" />

        {/* Noria: patas fijas, rueda que gira y cabinas que se mantienen derechas */}
        <path
          d={`M${wheel.cx - 30} 168 L${wheel.cx} ${wheel.cy} L${wheel.cx + 30} 168`}
          fill="none"
          stroke="url(#fs-blue)"
          strokeWidth="5"
        />
        <g className="wheel-spin" style={{ transformOrigin: `${wheel.cx}px ${wheel.cy}px` }}>
          <circle cx={wheel.cx} cy={wheel.cy} r={wheel.r} fill="none" />
          <circle cx={wheel.cx} cy={wheel.cy} r={wheel.r - 8} fill="none" strokeWidth="1.4" />
          {Array.from({ length: cabins }, (_, i) => {
            const angle = (i / cabins) * Math.PI * 2
            const x = wheel.cx + Math.cos(angle) * wheel.r
            const y = wheel.cy + Math.sin(angle) * wheel.r
            return (
              <g key={i}>
                <line x1={wheel.cx} y1={wheel.cy} x2={x} y2={y} strokeWidth="1.4" />
                <g className="cabin" style={{ transformOrigin: `${x}px ${y}px` }}>
                  <rect
                    x={x - 7}
                    y={y}
                    width="14"
                    height="11"
                    rx="2"
                    fill={`url(#fs-${GRADIENT_NAMES[i % GRADIENT_NAMES.length]})`}
                  />
                </g>
              </g>
            )
          })}
        </g>
        <circle cx={wheel.cx} cy={wheel.cy} r="6" fill="url(#fs-yellow)" />

        {/* Carrusel: banderín ondeando, tejado de gajos, faldón, caballitos y plataforma */}
        <line x1={roof.apexX} y1={roof.apexY} x2={roof.apexX} y2="22" />
        <path className="scene-flag" d={`M${roof.apexX} 22 l16 5 l-16 5z`} fill="url(#fs-red)" />

        {Array.from({ length: wedges }, (_, i) => {
          const d = `M${roof.apexX} ${roof.apexY} L${roof.left + i * wedgeW} ${roof.y} L${roof.left + (i + 1) * wedgeW} ${roof.y} Z`
          return (
            <g key={i}>
              <path d={d} fill={i % 2 ? 'url(#fs-yellow)' : 'url(#fs-red)'} />
              <path d={d} fill="url(#hatch)" stroke="none" />
            </g>
          )
        })}
        <path
          d={`M${roof.left} ${roof.y} ${Array.from({ length: wedges }, () => `a${wedgeW / 2} 9 0 0 0 ${wedgeW} 0`).join(' ')} Z`}
          fill="url(#fs-blue)"
        />

        {[150, 182, 246, 278].map((x) => (
          <line key={x} x1={x} y1={roof.y + 8} x2={x} y2="156" strokeWidth="2.6" />
        ))}
        <g className="horse horse-a">
          <line x1="201" y1={roof.y + 8} x2="201" y2="126" strokeWidth="1.6" />
          <rect x="190" y="126" width="22" height="16" rx="7" fill="url(#fs-pink)" />
        </g>
        <g className="horse horse-b">
          <line x1="229" y1={roof.y + 8} x2="229" y2="132" strokeWidth="1.6" />
          <rect x="218" y="132" width="22" height="16" rx="7" fill="url(#fs-green)" />
        </g>
        <ellipse cx={roof.apexX} cy="160" rx="86" ry="11" fill="url(#fs-ground)" />

        {/* Hierba */}
        <path
          d="M4 182 q12-10 24 0 t24 0 t24 0 t24 0 t24 0 t24 0 t24 0 t24 0 t24 0 t24 0 t24 0 t24 0 t24 0"
          fill="none"
          stroke="url(#fs-green)"
          strokeWidth="3"
        />
      </g>
    </svg>
  )
}

/** Trazo de cera que subraya el nombre de la marca; se "dibuja" al cargar. */
export function BrandSwoosh() {
  return (
    <svg className="brand-swoosh" viewBox="0 0 240 24" aria-hidden="true">
      <defs>
        <linearGradient id="sw" x1="0" y1="0" x2="1" y2="0">
          <stop offset="0" stopColor="#f6c22f" />
          <stop offset="0.5" stopColor="#ef8a2a" />
          <stop offset="1" stopColor="#e86d9a" />
        </linearGradient>
      </defs>
      <path
        filter="url(#wobble)"
        d="M8 15 C 60 4, 120 6, 232 11 M30 19 C 90 12, 150 13, 214 16"
        fill="none"
        stroke="url(#sw)"
        strokeWidth="5"
        strokeLinecap="round"
        pathLength={1}
      />
    </svg>
  )
}

/** Fondo de toda la app: cielo degradado, sol, nubes que cruzan, globos que suben y colinas. */
export function Scenery() {
  const clouds = [
    { top: '9%', scale: 1.1, duration: 95, delay: -20 },
    { top: '22%', scale: 0.7, duration: 130, delay: -75 },
    { top: '4%', scale: 0.55, duration: 160, delay: -120 },
    { top: '34%', scale: 0.9, duration: 115, delay: -40 },
  ]
  const balloons = [
    { left: '6%', color: 'red', duration: 34, delay: -4, size: 46 },
    { left: '88%', color: 'blue', duration: 41, delay: -18, size: 38 },
    { left: '15%', color: 'yellow', duration: 47, delay: -30, size: 32 },
    { left: '79%', color: 'green', duration: 38, delay: -9, size: 42 },
    { left: '94%', color: 'pink', duration: 52, delay: -36, size: 30 },
  ]

  return (
    <div className="scenery" aria-hidden="true">
      <div className="sky-sun" />

      {clouds.map((c, i) => (
        <svg
          key={i}
          className="sky-cloud"
          viewBox="0 0 160 70"
          style={{ top: c.top, '--s': c.scale, animationDuration: `${c.duration}s`, animationDelay: `${c.delay}s` } as CSSProperties}
        >
          <defs>
            <linearGradient id={`cl-${i}`} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0" stopColor="#ffffff" />
              <stop offset="1" stopColor="#d9eaf7" />
            </linearGradient>
          </defs>
          <path
            filter="url(#wobble)"
            d="M20 58 q-16 0 -14 -14 q2 -14 20 -12 q4 -22 30 -20 q14 -16 36 -6 q22 -8 30 12 q22 0 22 20 q0 20 -22 20 z"
            fill={`url(#cl-${i})`}
            stroke={LINE}
            strokeWidth="2"
            strokeOpacity="0.55"
          />
        </svg>
      ))}

      {balloons.map((b, i) => (
        <svg
          key={i}
          className="sky-balloon"
          viewBox="0 0 40 70"
          width={b.size}
          style={{ left: b.left, animationDuration: `${b.duration}s`, animationDelay: `${b.delay}s` } as CSSProperties}
        >
          <defs>
            <CrayonGradients prefix={`sb${i}`} />
          </defs>
          <g filter="url(#wobble)" stroke={LINE} strokeWidth="1.6">
            <path d="M20 44 q-4 8 0 13 t0 12" fill="none" />
            <path d="M20 2 C 36 2 38 22 30 34 C 26 40 22 43 20 44 C 18 43 14 40 10 34 C 2 22 4 2 20 2 Z" fill={`url(#sb${i}-${b.color})`} />
            <path d="M12 12 q2 -5 7 -6" fill="none" stroke="#fff" strokeOpacity="0.7" strokeWidth="2.4" strokeLinecap="round" />
          </g>
        </svg>
      ))}

      <svg className="sky-hills" viewBox="0 0 1200 220" preserveAspectRatio="none">
        <defs>
          <linearGradient id="hill-far" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0" stopColor="#b9dfa6" />
            <stop offset="1" stopColor="#8cc47a" />
          </linearGradient>
          <linearGradient id="hill-near" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0" stopColor="#7cc56d" />
            <stop offset="1" stopColor="#4c9c44" />
          </linearGradient>
        </defs>
        <g filter="url(#wobble-2)" stroke={LINE} strokeWidth="2" strokeOpacity="0.5">
          <path d="M0 120 C 160 60 320 70 470 110 S 760 50 930 90 S 1120 80 1200 70 V220 H0 Z" fill="url(#hill-far)" />
          <path d="M0 170 C 200 120 380 140 560 160 S 900 110 1200 150 V220 H0 Z" fill="url(#hill-near)" />
        </g>
      </svg>
    </div>
  )
}
