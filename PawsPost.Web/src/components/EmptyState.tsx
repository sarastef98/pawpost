import type { ReactNode } from 'react'

type EmptyStateProps = {
  message: string
  children?: ReactNode
}

export default function EmptyState({ message, children }: EmptyStateProps) {
  return (
    <div className="flex flex-col items-center gap-4 p-6 text-center">
      <p className="text-lg text-muted">{message}</p>
      {children}
    </div>
  )
}
