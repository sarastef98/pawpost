import { useId } from 'react'
import type { Ref, SelectHTMLAttributes } from 'react'

type SelectOption = {
  value: string
  label: string
}

type SelectProps = Omit<
  SelectHTMLAttributes<HTMLSelectElement>,
  'id' | 'children'
> & {
  label: string
  options: SelectOption[]
  helperText?: string
  errorText?: string
  ref?: Ref<HTMLSelectElement>
}

export default function Select({
  label,
  options,
  helperText,
  errorText,
  className = '',
  'aria-describedby': ariaDescribedBy,
  ...props
}: SelectProps) {
  const id = useId()
  const messageId = `${id}-message`
  const message = errorText ?? helperText
  const describedBy =
    [ariaDescribedBy, message ? messageId : undefined]
      .filter(Boolean)
      .join(' ') || undefined

  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={id} className="text-sm font-semibold text-text">
        {label}
      </label>
      <select
        {...props}
        id={id}
        aria-invalid={errorText ? true : props['aria-invalid']}
        aria-describedby={describedBy}
        className={`min-h-11 w-full rounded-card border bg-surface px-3 py-2 text-base text-text ${errorText ? 'border-danger' : 'border-border'} ${className}`}
      >
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
      {message && (
        <p
          id={messageId}
          className={`text-sm ${errorText ? 'text-danger' : 'text-muted'}`}
        >
          {message}
        </p>
      )}
    </div>
  )
}
