import { Navigate } from 'react-router-dom'
import Spinner from '../../components/Spinner'
import { useAuth } from '../auth/useAuth'
import AssistantSearch from './AssistantSearch'

// The guard. Nothing is requested before the visitor is known to be logged in.
export default function AssistantPage() {
  const { status } = useAuth()

  if (status === 'loggedOut') return <Navigate to="/login" replace />
  if (status === 'loading') {
    return (
      <section className="mx-auto flex w-full max-w-2xl flex-col gap-4">
        <h1 className="text-2xl">Find a pet with the assistant</h1>
        <Spinner />
      </section>
    )
  }
  return <AssistantSearch />
}
