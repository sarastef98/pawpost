import { Route, Routes } from 'react-router-dom'
import Layout from './components/Layout'
import AssistantPage from './features/assistant/AssistantPage'
import LoginPage from './features/auth/LoginPage'
import RegisterPage from './features/auth/RegisterPage'
import HomePage from './features/home/HomePage'
import NotFoundPage from './features/notFound/NotFoundPage'
import CreatePetAdPage from './features/petAds/CreatePetAdPage'
import PetAdDetailsPage from './features/petAds/PetAdDetailsPage'
import StyleGuidePage from './features/styleguide/StyleGuidePage'

function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/" element={<HomePage />} />
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route path="/pet-ads/new" element={<CreatePetAdPage />} />
        <Route path="/pet-ads/:id" element={<PetAdDetailsPage />} />
        <Route path="/assistant" element={<AssistantPage />} />
        <Route path="/styleguide" element={<StyleGuidePage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  )
}

export default App
