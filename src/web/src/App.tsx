import { Navigate, Route, Routes } from 'react-router-dom'
import { Layout } from './components/Layout'
import { AskPage } from './pages/AskPage'
import { KnowledgePage } from './pages/KnowledgePage'
import { NewRunPage } from './pages/NewRunPage'
import { RunDetailPage } from './pages/RunDetailPage'
import { RunsPage } from './pages/RunsPage'

export default function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<RunsPage />} />
        <Route path="runs/new" element={<NewRunPage />} />
        <Route path="runs/:id" element={<RunDetailPage />} />
        <Route path="ask" element={<AskPage />} />
        <Route path="knowledge" element={<KnowledgePage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}
