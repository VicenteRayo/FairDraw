import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { createDraw } from '../api/client'
import { saveCreatorIdentity } from '../crypto/storage'

function EditableList({
  label,
  placeholder,
  items,
  onChange,
}: {
  label: string
  placeholder: string
  items: string[]
  onChange: (items: string[]) => void
}) {
  function update(index: number, value: string) {
    const next = [...items]
    next[index] = value
    onChange(next)
  }

  function remove(index: number) {
    onChange(items.filter((_, i) => i !== index))
  }

  return (
    <fieldset className="field-group">
      <legend>{label}</legend>
      {items.map((value, i) => (
        <div className="list-row" key={i}>
          <input className="input" value={value} placeholder={placeholder} onChange={(e) => update(i, e.target.value)} />
          <button type="button" className="btn btn-icon" aria-label="Eliminar" onClick={() => remove(i)}>
            ✕
          </button>
        </div>
      ))}
      <button type="button" className="btn btn-link" onClick={() => onChange([...items, ''])}>
        + Añadir
      </button>
    </fieldset>
  )
}

export default function CreateDrawPage() {
  const navigate = useNavigate()
  const [name, setName] = useState('')
  const [options, setOptions] = useState<string[]>(['', ''])
  const [participants, setParticipants] = useState<string[]>(['', ''])
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)

    const cleanOptions = options.map((o) => o.trim()).filter(Boolean)
    const cleanParticipants = participants.map((p) => p.trim()).filter(Boolean)

    if (!name.trim()) {
      setError('Ponle un nombre al sorteo.')
      return
    }
    if (cleanOptions.length < 2) {
      setError('Añade al menos dos opciones.')
      return
    }
    if (cleanParticipants.length < 2) {
      setError('Añade al menos dos participantes.')
      return
    }

    setSubmitting(true)
    try {
      const created = await createDraw(name.trim(), cleanOptions, cleanParticipants)
      saveCreatorIdentity({ drawId: created.drawId, creatorToken: created.creatorToken })
      navigate(`/draw/${created.drawId}`)
    } catch {
      setError('No se ha podido crear el sorteo. Inténtalo de nuevo.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="page">
      <div className="page-head">
        <Link to="/" className="back-link" aria-label="Volver al inicio">
          ←
        </Link>
        <h1>Crear sorteo</h1>
      </div>
      <p className="hint">Los datos del sorteo (incluido su resultado) se eliminan automáticamente 30 días después de crearlo.</p>

      <form onSubmit={handleSubmit} className="card stack">
        <label className="field">
          Nombre del sorteo
          <input className="input" value={name} onChange={(e) => setName(e.target.value)} placeholder="p. ej. Destino del viaje" />
        </label>

        <EditableList label="Opciones" placeholder="p. ej. Villaconejos" items={options} onChange={setOptions} />
        <EditableList label="Participantes" placeholder="p. ej. Agapito" items={participants} onChange={setParticipants} />

        {error && <p className="error">{error}</p>}

        <button className="btn btn-primary" type="submit" disabled={submitting}>
          {submitting ? 'Creando…' : 'Crear sorteo'}
        </button>
      </form>
    </div>
  )
}
