import { useId } from 'react'
import type { InputHTMLAttributes } from 'react'

type TextFieldProps = InputHTMLAttributes<HTMLInputElement> & {
  label: string
  helperText?: string
  errorText?: string
}

export default function TextField({
  label,
  helperText,
  errorText,
  className = '',
  ...props
}: TextFieldProps) {
  const id = useId()
  const messageId = `${id}-message`
  const message = errorText ?? helperText

  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={id} className="text-sm font-semibold text-text">
        {label}
      </label>
      <input
        id={id}
        aria-invalid={errorText ? true : undefined}
        aria-describedby={message ? messageId : undefined}
        className={`min-h-11 w-full rounded-card border bg-surface px-3 py-2 text-base text-text ${errorText ? 'border-danger' : 'border-border'} ${className}`}
        {...props}
      />
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
