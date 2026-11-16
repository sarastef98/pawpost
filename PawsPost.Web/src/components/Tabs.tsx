import { useId, useRef, useState } from 'react'
import type { KeyboardEvent, ReactNode } from 'react'

export type TabItem = {
  id: string
  label: string
  content: () => ReactNode
}

type TabsProps = {
  label: string
  items: TabItem[]
}

const tabClasses = 'min-h-11 rounded-t-card px-4 text-base font-semibold'
const selectedClasses = 'border-b-2 border-primary bg-primary-soft text-primary'
const idleClasses = 'text-muted hover:text-primary'

// An accessible tab list (WAI-ARIA tabs, automatic activation). Only the
// selected panel renders its content, so a tab's data loads when it is opened.
export default function Tabs({ label, items }: TabsProps) {
  const baseId = useId()
  const [selected, setSelected] = useState(0)
  const tabRefs = useRef<(HTMLButtonElement | null)[]>([])

  function tabId(item: TabItem) {
    return `${baseId}-tab-${item.id}`
  }

  function panelId(item: TabItem) {
    return `${baseId}-panel-${item.id}`
  }

  function select(index: number) {
    setSelected(index)
    tabRefs.current[index]?.focus()
  }

  function handleKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    const last = items.length - 1
    let next: number
    if (event.key === 'ArrowRight') {
      next = selected >= last ? 0 : selected + 1
    } else if (event.key === 'ArrowLeft') {
      next = selected <= 0 ? last : selected - 1
    } else if (event.key === 'Home') {
      next = 0
    } else if (event.key === 'End') {
      next = last
    } else {
      return
    }
    event.preventDefault()
    select(next)
  }

  return (
    <div className="flex flex-col gap-4">
      <div
        role="tablist"
        aria-label={label}
        onKeyDown={handleKeyDown}
        className="flex flex-wrap gap-2 border-b border-border"
      >
        {items.map((item, index) => (
          <button
            key={item.id}
            ref={(element) => {
              tabRefs.current[index] = element
            }}
            type="button"
            role="tab"
            id={tabId(item)}
            aria-selected={index === selected}
            aria-controls={panelId(item)}
            tabIndex={index === selected ? 0 : -1}
            onClick={() => setSelected(index)}
            className={`${tabClasses} ${index === selected ? selectedClasses : idleClasses}`}
          >
            {item.label}
          </button>
        ))}
      </div>
      {items.map((item, index) => (
        <div
          key={item.id}
          role="tabpanel"
          id={panelId(item)}
          aria-labelledby={tabId(item)}
          tabIndex={0}
          hidden={index !== selected}
          className="flex flex-col gap-4"
        >
          {index === selected ? item.content() : null}
        </div>
      ))}
    </div>
  )
}
