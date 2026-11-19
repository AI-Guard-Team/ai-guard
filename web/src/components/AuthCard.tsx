import type { ReactNode } from 'react'

interface AuthCardProps {
  title: string
  lead: string
  children: ReactNode
}

export function AuthCard({ title, lead, children }: AuthCardProps) {
  return (
    <main className="auth-screen">
      <section className="card auth-card">
        <p className="auth-brand">AI-Guard</p>
        <h1>{title}</h1>
        <p className="auth-lead">{lead}</p>
        {children}
      </section>
    </main>
  )
}

export function FormAlert({ message }: { message: string | null }) {
  if (!message) return null
  return (
    <p className="form-alert" role="alert">
      {message}
    </p>
  )
}
