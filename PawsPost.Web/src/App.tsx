import { Route, Routes } from 'react-router-dom'
import Layout from './components/Layout'
import HomePage from './features/home/HomePage'
import StyleGuidePage from './features/styleguide/StyleGuidePage'

function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/" element={<HomePage />} />
        <Route path="/styleguide" element={<StyleGuidePage />} />
      </Route>
    </Routes>
  )
}

export default App
