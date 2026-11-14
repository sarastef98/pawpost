import { Link, Outlet } from 'react-router-dom'
import AuthNav from '../features/auth/AuthNav'

const currentYear = new Date().getFullYear()

export default function Layout() {
  return (
    <div className="flex min-h-screen flex-col">
      <header className="border-b border-border bg-surface">
        <div className="mx-auto flex max-w-content flex-wrap items-center justify-between gap-2 px-4 py-3">
          <Link to="/" className="text-xl font-semibold text-primary">
            PawsPost
          </Link>
          <nav aria-label="Main">
            <AuthNav />
          </nav>
        </div>
      </header>
      <main className="mx-auto w-full max-w-content flex-1 px-4 py-6">
        <Outlet />
      </main>
      <footer className="border-t border-border bg-surface">
        <p className="mx-auto max-w-content px-4 py-4 text-sm text-muted">
          PawsPost {currentYear}
        </p>
      </footer>
    </div>
  )
}
