import { Route, Routes } from 'react-router-dom'
import HomePage from './pages/HomePage'
import CreateDrawPage from './pages/CreateDrawPage'
import PublicDrawPage from './pages/PublicDrawPage'
import VerifyPage from './pages/VerifyPage'

export default function App() {
  return (
    <div className="app-shell">
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/draws/new" element={<CreateDrawPage />} />
        <Route path="/draw/:code" element={<PublicDrawPage />} />
        <Route path="/draw/:code/verify" element={<VerifyPage />} />
      </Routes>
    </div>
  )
}
