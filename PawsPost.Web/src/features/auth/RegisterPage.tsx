import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import Button from '../../components/Button'
import Card from '../../components/Card'
import ErrorMessage from '../../components/ErrorMessage'
import TextField from '../../components/TextField'
import { useAuth } from './useAuth'
import type { FormErrors } from './authErrors'

const REGISTERED_NOTICE = 'Your account is ready. Please log in.'

export default function RegisterPage() {
  const { register } = useAuth()
  const navigate = useNavigate()

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [errors, setErrors] = useState<FormErrors>({})
  const [busy, setBusy] = useState(false)
  const submitting = useRef(false)
  const formRef = useRef<HTMLFormElement>(null)
  const alertRef = useRef<HTMLDivElement>(null)

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
    const result = await register(email.trim(), password)
    submitting.current = false
    setBusy(false)
    setPassword('')
    if ('errors' in result) {
      setErrors(result.errors)
    } else {
      navigate('/login', { state: { notice: REGISTERED_NOTICE } })
    }
  }

  return (
    <div className="mx-auto flex w-full max-w-md flex-col gap-4">
      <h1 className="text-2xl">Create your account</h1>
      <Card className="flex flex-col gap-4">
        {errors.general && (
          <div ref={alertRef} tabIndex={-1}>
            <ErrorMessage message={errors.general} />
            {errors.emailTaken && (
              <p className="text-center text-base">
                <Link
                  to="/login"
                  className="font-semibold text-primary underline"
                >
                  Log in
                </Link>
              </p>
            )}
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
            autoComplete="new-password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            errorText={errors.password}
            readOnly={busy}
          />
          <Button type="submit" disabled={busy} aria-busy={busy}>
            {busy ? 'Please wait...' : 'Create account'}
          </Button>
        </form>
      </Card>
      <p className="text-base text-muted">
        Already have an account?{' '}
        <Link to="/login" className="font-semibold text-primary underline">
          Log in
        </Link>
      </p>
    </div>
  )
}
