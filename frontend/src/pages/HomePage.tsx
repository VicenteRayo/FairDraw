import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { BrandSwoosh, FairScene } from '../components/Doodles'

export default function HomePage() {
  const navigate = useNavigate()
  const [joinCode, setJoinCode] = useState('')

  function handleJoin(e: React.FormEvent) {
    e.preventDefault()
    const code = joinCode.trim().toUpperCase()
    if (code) navigate(`/draw/${code}`)
  }

  return (
    <div className="page page-centered">
      <div className="card home-card">
        <FairScene />
        <div className="brand-wrap">
          <h1 className="brand">FairDraw</h1>
          <BrandSwoosh />
        </div>
        <p className="subtitle">Sorteos entre amigos que cualquiera puede comprobar.</p>

        <ol className="how">
          <li>
            <b>Cada uno elige</b>Todos escogen un número en secreto, sellado criptográficamente.
          </li>
          <li>
            <b>Se revela</b>Los números se destapan a la vez y deciden el resultado.
          </li>
          <li>
            <b>Se comprueba</b>Cualquiera puede recalcularlo y ver que nadie hizo trampa.
          </li>
        </ol>

        <button className="btn btn-primary" onClick={() => navigate('/draws/new')}>
          Crear sorteo
        </button>

        <div className="divider">o únete con un código</div>

        <form className="join-form" onSubmit={handleJoin}>
          <input
            className="input"
            placeholder="Código del sorteo"
            value={joinCode}
            onChange={(e) => setJoinCode(e.target.value)}
            maxLength={8}
            aria-label="Código del sorteo"
          />
          <button className="btn btn-secondary" type="submit">
            Unirse a sorteo
          </button>
        </form>
      </div>
    </div>
  )
}
