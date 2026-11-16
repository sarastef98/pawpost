import { useEffect, useId, useRef } from 'react'
import type { KeyboardEvent, ReactNode, RefObject } from 'react'
import { createPortal } from 'react-dom'

type DialogProps = {
  title: string
  description: ReactNode
  children: ReactNode
  onCancel: () => void
  cancelDisabled?: boolean
  initialFocusRef: RefObject<HTMLElement | null>
}

const focusableSelector =
  'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'

// A modal dialog. The parent mounts it only while it is open. While open, the
// page behind is inert and does not scroll, focus stays inside the dialog and
// returns to the opener when the dialog unmounts. A click on the backdrop does
// nothing, so a touch cannot dismiss it by accident.
export default function Dialog({
  title,
  description,
  children,
  onCancel,
  cancelDisabled = false,
  initialFocusRef,
}: DialogProps) {
  const titleId = useId()
  const descriptionId = useId()
  const panelRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const opener = document.activeElement
    const inerted: HTMLElement[] = []
    for (const child of Array.from(document.body.children)) {
      if (child instanceof HTMLElement && !child.contains(panelRef.current)) {
        if (!child.hasAttribute('inert')) {
          child.setAttribute('inert', '')
          inerted.push(child)
        }
      }
    }
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'

    const first =
      panelRef.current?.querySelector<HTMLElement>(focusableSelector)
    ;(initialFocusRef.current ?? first)?.focus()

    return () => {
      for (const child of inerted) child.removeAttribute('inert')
      document.body.style.overflow = previousOverflow
      if (opener instanceof HTMLElement && document.contains(opener)) {
        opener.focus()
      }
    }
  }, [initialFocusRef])

  function handleKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (event.key === 'Escape') {
      event.stopPropagation()
      if (!cancelDisabled) onCancel()
      return
    }
    if (event.key !== 'Tab') return
    const items = Array.from(
      panelRef.current?.querySelectorAll<HTMLElement>(focusableSelector) ?? [],
    )
    if (items.length === 0) {
      event.preventDefault()
      return
    }
    const firstItem = items[0]
    const lastItem = items[items.length - 1]
    const active = document.activeElement
    if (
      event.shiftKey &&
      (active === firstItem || active === panelRef.current)
    ) {
      event.preventDefault()
      lastItem.focus()
    } else if (!event.shiftKey && active === lastItem) {
      event.preventDefault()
      firstItem.focus()
    } else if (!panelRef.current?.contains(active)) {
      event.preventDefault()
      firstItem.focus()
    }
  }

  return createPortal(
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-text/50 p-4">
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={descriptionId}
        onKeyDown={handleKeyDown}
        className="flex w-full max-w-md flex-col gap-4 rounded-card bg-surface p-6 shadow-card"
      >
        <h2 id={titleId} className="text-xl break-words">
          {title}
        </h2>
        <div id={descriptionId} className="text-base">
          {description}
        </div>
        {children}
      </div>
    </div>,
    document.body,
  )
}
