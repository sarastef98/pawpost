import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import Button from '../../components/Button'
import Spinner from '../../components/Spinner'
import { useAuth } from './useAuth'
import { LOGOUT_FAILED_MESSAGE } from './authErrors'

const linkClasses =
  'inline-flex min-h-11 items-center text-base font-semibold text-primary'

export default function AuthNav() {
  const { status, user, logout } = useAuth()
  const navigate = useNavigate()
  const [busy, setBusy] = useState(false)
  const [failed, setFailed] = useState(false)

  async function handleLogout() {
    if (busy) {
      return
    }
    setBusy(true)
    setFailed(false)
    const result = await logout()
    setBusy(false)
    if (result.ok) {
      navigate('/')
    } else {
      setFailed(true)
    }
  }

  if (status === 'loading') {
    return <Spinner />
  }

  if (status === 'loggedOut') {
    return (
      <div className="flex items-center gap-4">
        <Link to="/login" className={linkClasses}>
          Log in
        </Link>
        <Link to="/register" className={linkClasses}>
          Register
        </Link>
      </div>
    )
  }

  return (
    <div className="flex flex-wrap items-center gap-4">
      {failed && (
        <p role="alert" className="text-sm text-danger">
          {LOGOUT_FAILED_MESSAGE}
        </p>
      )}
      <Link to="/assistant" className={linkClasses}>
        Ask the assistant
      </Link>
      <Link to="/pet-ads/new" className={linkClasses}>
        Post an ad
      </Link>
      {user !== null && (
        <Link
          to={`/creators/${encodeURIComponent(user.id)}`}
          className={linkClasses}
        >
          My profile
        </Link>
      )}
      <Button variant="secondary" onClick={handleLogout} disabled={busy}>
        Log out
      </Button>
    </div>
  )
}
