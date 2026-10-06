import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import App from './App.tsx'
import './index.css'

// import.meta.env.BASE_URL reflects Vite's `base` build/dev option (e.g. "/proyectos/fairdraw/"
// in production, "/" in local dev) — reusing it here keeps React Router's client-side routes
// aligned with wherever the app is actually served from, without a separate env var to keep in sync.
const basename = import.meta.env.BASE_URL.replace(/\/$/, '') || '/'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter basename={basename}>
      <App />
    </BrowserRouter>
  </StrictMode>,
)
