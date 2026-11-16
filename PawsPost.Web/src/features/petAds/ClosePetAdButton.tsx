import { useEffect, useRef, useState } from 'react'
import { Navigate } from 'react-router-dom'
import Button from '../../components/Button'
import Dialog from '../../components/Dialog'
import ErrorMessage from '../../components/ErrorMessage'
import { useClosePetAd } from './useClosePetAd'

type ClosePetAdButtonProps = {
  adId: string
  adTitle?: string
  className?: string
  onClosed: () => void
  onGone: () => void
}

const FORBIDDEN_TEXT = 'You can only close your own ads.'
const GONE_TEXT = 'This ad is already closed or no longer exists.'
const ERROR_TEXT = 'We could not close this ad. Please try again.'

// The "Close ad" trigger and its confirmation dialog. The server decides who
// may close an ad; the messages here only map the status code to friendly text.
export default function ClosePetAdButton({
  adId,
  adTitle,
  className = '',
  onClosed,
  onGone,
}: ClosePetAdButtonProps) {
  const { state, close, reset } = useClosePetAd()
  const [open, setOpen] = useState(false)
  const cancelRef = useRef<HTMLButtonElement>(null)
  const okRef = useRef<HTMLButtonElement>(null)
  const onClosedRef = useRef(onClosed)

  useEffect(() => {
    onClosedRef.current = onClosed
  }, [onClosed])

  useEffect(() => {
    if (state !== 'closed') return
    onClosedRef.current()
  }, [state])

  // The confirm button is replaced by the result, so focus moves to the
  // button that is left, instead of dropping to the page behind the dialog.
  useEffect(() => {
    if (state === 'forbidden' || state === 'gone') okRef.current?.focus()
    if (state === 'error') cancelRef.current?.focus()
  }, [state])

  if (state === 'unauthorized') return <Navigate to="/login" replace />

  const pending = state === 'pending'
  const finished = state === 'forbidden' || state === 'gone'

  function openDialog() {
    reset()
    setOpen(true)
  }

  function cancel() {
    if (pending) return
    setOpen(false)
  }

  function confirm() {
    void close(adId)
  }

  function acknowledge() {
    setOpen(false)
    if (state === 'gone') onGone()
  }

  return (
    <>
      <Button variant="secondary" className={className} onClick={openDialog}>
        Close ad
        {adTitle !== undefined && <span className="sr-only">: {adTitle}</span>}
      </Button>
      {open && state !== 'closed' && (
        <Dialog
          title="Close this ad?"
          description="Closing removes this ad for everyone. This cannot be undone."
          onCancel={cancel}
          cancelDisabled={pending}
          initialFocusRef={cancelRef}
        >
          {finished && (
            <>
              <p role="alert" className="text-base text-danger">
                {state === 'forbidden' ? FORBIDDEN_TEXT : GONE_TEXT}
              </p>
              <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
                <Button ref={okRef} onClick={acknowledge}>
                  OK
                </Button>
              </div>
            </>
          )}
          {state === 'error' && (
            <ErrorMessage message={ERROR_TEXT} onRetry={confirm} />
          )}
          {!finished && (
            <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
              <Button
                ref={cancelRef}
                variant="secondary"
                aria-disabled={pending ? true : undefined}
                onClick={cancel}
              >
                Cancel
              </Button>
              {state !== 'error' && (
                <Button
                  variant="danger"
                  aria-disabled={pending ? true : undefined}
                  onClick={confirm}
                >
                  {pending ? 'Closing...' : 'Close ad'}
                </Button>
              )}
            </div>
          )}
        </Dialog>
      )}
    </>
  )
}
