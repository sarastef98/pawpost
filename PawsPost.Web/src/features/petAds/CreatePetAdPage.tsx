import { Navigate } from 'react-router-dom'
import Spinner from '../../components/Spinner'
import { useAuth } from '../auth/useAuth'
import CreatePetAdForm from './CreatePetAdForm'

// The guard. The form is only mounted once the visitor is known to be
// logged in.
export default function CreatePetAdPage() {
  const { status } = useAuth()

  if (status === 'loggedOut') return <Navigate to="/login" replace />

  if (status === 'loading') {
    return (
      <section className="flex flex-col gap-4">
        <h1 className="text-2xl">Post an ad</h1>
        <Spinner />
      </section>
    )
  }

  return <CreatePetAdForm />
}
