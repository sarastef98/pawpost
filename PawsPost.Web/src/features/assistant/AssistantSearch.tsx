import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { Navigate } from 'react-router-dom'
import { client } from '../../api/client'
import Button from '../../components/Button'
import Card from '../../components/Card'
import ErrorMessage from '../../components/ErrorMessage'
import Spinner from '../../components/Spinner'
import TextField from '../../components/TextField'
import AssistantAnswer from './AssistantAnswer'
import { SEARCH_FAILED_MESSAGE, mapQuestionError } from './assistantErrors'
import { buildSearchBody, validateQuestion } from './assistantQuestion'
import { toAssistantResult } from './assistantResult'
import type { AssistantResult } from './assistantResult'

type SearchState =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'success'; result: AssistantResult }
  | { status: 'error'; question: string }

export default function AssistantSearch() {
  const [question, setQuestion] = useState('')
  const [fieldError, setFieldError] = useState<string | undefined>()
  const [state, setState] = useState<SearchState>({ status: 'idle' })
  const [unauthorized, setUnauthorized] = useState(false)
  const submitting = useRef(false)
  const unmounted = useRef(false)
  const pendingFocus = useRef(false)
  const formRef = useRef<HTMLFormElement>(null)
  const answerRef = useRef<HTMLHeadingElement>(null)
  const errorRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    unmounted.current = false
    return () => {
      unmounted.current = true
    }
  }, [])

  // A server 400 moves focus to the field (it is marked aria-invalid).
  useEffect(() => {
    if (fieldError) {
      formRef.current
        ?.querySelector<HTMLElement>('input[aria-invalid="true"]')
        ?.focus()
    }
  }, [fieldError])

  // The Try again button is replaced by the spinner, so focus goes to the
  // answer heading or the error once the result settles.
  useEffect(() => {
    if (state.status === 'loading' || !pendingFocus.current) return
    pendingFocus.current = false
    if (state.status === 'success') answerRef.current?.focus()
    if (state.status === 'error') errorRef.current?.focus()
  }, [state])

  async function ask(text: string) {
    if (submitting.current) return
    submitting.current = true
    setFieldError(undefined)
    setState({ status: 'loading' })
    try {
      const result = await client.POST('/api/assistant/search', {
        body: buildSearchBody(text),
      })
      if (unmounted.current) return
      const status = result.response.status
      if (status === 401) {
        setUnauthorized(true)
        return
      }
      if (status === 400) {
        setFieldError(mapQuestionError(result.error))
        setState({ status: 'idle' })
        return
      }
      const converted = result.response.ok
        ? toAssistantResult(result.data)
        : null
      setState(
        converted
          ? { status: 'success', result: converted }
          : { status: 'error', question: text },
      )
    } catch {
      if (!unmounted.current) setState({ status: 'error', question: text })
    } finally {
      submitting.current = false
    }
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (submitting.current) return
    const message = validateQuestion(question)
    if (message) {
      setFieldError(message)
      return
    }
    void ask(question)
  }

  function retry() {
    if (state.status !== 'error') return
    pendingFocus.current = true
    void ask(state.question)
  }

  if (unauthorized) return <Navigate to="/login" replace />

  const busy = state.status === 'loading'
  let announcement = ''
  if (state.status === 'success') {
    const count = state.result.ads.length
    announcement =
      count === 0
        ? 'Answer ready. No ads found.'
        : `Answer ready. ${count} ${count === 1 ? 'ad' : 'ads'} found.`
  }

  return (
    <section className="mx-auto flex w-full max-w-2xl flex-col gap-4">
      <h1 className="text-2xl">Find a pet with the assistant</h1>
      <p className="text-base text-muted">
        Describe the pet you are looking for in your own words.
      </p>
      <Card>
        <form
          ref={formRef}
          noValidate
          aria-busy={busy}
          onSubmit={handleSubmit}
          className="flex flex-col gap-4"
        >
          <TextField
            label="Your question"
            placeholder="For example: a calm dog that is good with small children"
            helperText="Up to 500 characters."
            value={question}
            onChange={(e) => setQuestion(e.target.value)}
            errorText={fieldError}
            readOnly={busy}
          />
          {/* aria-disabled instead of disabled: a disabled button loses focus,
              so a keyboard user would lose their place. handleSubmit ignores
              submits while a request is running. */}
          <Button type="submit" aria-disabled={busy} aria-busy={busy}>
            {busy ? 'Please wait...' : 'Ask'}
          </Button>
        </form>
      </Card>
      <p role="status" className="sr-only">
        {announcement}
      </p>
      {state.status === 'loading' && <Spinner />}
      {state.status === 'success' && (
        <AssistantAnswer {...state.result} headingRef={answerRef} />
      )}
      {state.status === 'error' && (
        <div ref={errorRef} tabIndex={-1}>
          <ErrorMessage message={SEARCH_FAILED_MESSAGE} onRetry={retry} />
        </div>
      )}
    </section>
  )
}
