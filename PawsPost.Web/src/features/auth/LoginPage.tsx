import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import Button from '../../components/Button'
import Card from '../../components/Card'
import ErrorMessage from '../../components/ErrorMessage'
import TextField from '../../components/TextField'
import { useAuth } from './useAuth'
import type { FormErrors } from './authErrors'

function readNotice(state: unknown): string | null {
  if (
    typeof state === 'object' &&
    state !== null &&
    'notice' in state &&
    typeof state.notice === 'string'
  ) {
    return state.notice
  }
  return null
}

export default function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const notice = readNotice(location.state)

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [errors, setErrors] = useState<FormErrors>({})
  const [busy, setBusy] = useState(false)
  const submitting = useRef(false)
  const formRef = useRef<HTMLFormElement>(null)
  const alertRef = useRef<HTMLDivElement>(null)
  const [shownNotice, setShownNotice] = useState('')

  // Screen readers announce a status region when its content changes, not
  // when it appears together with its content. So the empty region renders
  // first, and the notice is filled in on the next tick.
  useEffect(() => {
    const timer = setTimeout(() => setShownNotice(notice ?? ''), 0)
    return () => clearTimeout(timer)
  }, [notice])

  // After a failed submit, move focus to the first field with an error, or
  // to the general message, so keyboard and screen reader users find it.
  // TextField marks a field with an error as aria-invalid.
  useEffect(() => {
    const invalidField = formRef.current?.querySelector<HTMLInputElement>(
      'input[aria-invalid="true"]',
    )
    if (invalidField) {
      invalidField.focus()
    } else if (errors.general) {
      alertRef.current?.focus()
    }
  }, [errors])

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (submitting.current) {
      return
    }

    const clientErrors: FormErrors = {}
    if (email.trim() === '') {
      clientErrors.email = 'Please enter your email.'
    }
    if (password === '') {
      clientErrors.password = 'Please enter your password.'
    }
    if (clientErrors.email || clientErrors.password) {
      setErrors(clientErrors)
      return
    }

    submitting.current = true
    setBusy(true)
    setErrors({})
    const result = await login(email.trim(), password)
    submitting.current = false
    setBusy(false)
    setPassword('')
    if ('errors' in result) {
      setErrors(result.errors)
    } else {
      navigate('/')
    }
  }

  return (
    <div className="mx-auto flex w-full max-w-md flex-col gap-4">
      <h1 className="text-2xl">Log in</h1>
      <Card className="flex flex-col gap-4">
        {/* Always in the page. While empty it is visually hidden, so it adds
            no gap, but stays known to screen readers. */}
        <p
          role="status"
          className={shownNotice ? 'text-base text-success' : 'sr-only'}
        >
          {shownNotice}
        </p>
        {errors.general && (
          <div ref={alertRef} tabIndex={-1}>
            <ErrorMessage message={errors.general} />
          </div>
        )}
        <form
          ref={formRef}
          noValidate
          onSubmit={handleSubmit}
          className="flex flex-col gap-4"
        >
          <TextField
            label="Email"
            type="email"
            autoComplete="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            errorText={errors.email}
            readOnly={busy}
          />
          <TextField
            label="Password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            errorText={errors.password}
            readOnly={busy}
          />
          <Button type="submit" disabled={busy} aria-busy={busy}>
            {busy ? 'Please wait...' : 'Log in'}
          </Button>
        </form>
      </Card>
      <p className="text-base text-muted">
        New here?{' '}
        <Link to="/register" className="font-semibold text-primary underline">
          Create an account
        </Link>
      </p>
    </div>
  )
}
