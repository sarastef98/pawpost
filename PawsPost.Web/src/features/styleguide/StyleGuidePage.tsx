import type { ReactNode } from 'react'
import Badge from '../../components/Badge'
import Button from '../../components/Button'
import Card from '../../components/Card'
import EmptyState from '../../components/EmptyState'
import ErrorMessage from '../../components/ErrorMessage'
import Spinner from '../../components/Spinner'
import TextField from '../../components/TextField'

type SectionProps = {
  title: string
  children: ReactNode
}

function Section({ title, children }: SectionProps) {
  return (
    <section className="flex flex-col gap-4">
      <h2 className="text-xl">{title}</h2>
      {children}
    </section>
  )
}

export default function StyleGuidePage() {
  return (
    <div className="flex flex-col gap-8">
      <h1 className="text-2xl">Style guide</h1>

      <Section title="Button">
        <div className="flex flex-wrap gap-4">
          <Button variant="primary">Primary</Button>
          <Button variant="secondary">Secondary</Button>
          <Button variant="danger">Danger</Button>
        </div>
      </Section>

      <Section title="TextField">
        <div className="flex max-w-sm flex-col gap-4">
          <TextField label="Pet name" />
          <TextField label="Breed" helperText="For example, Labrador." />
          <TextField label="Location" errorText="Please add a location." />
        </div>
      </Section>

      <Section title="Card">
        <Card>
          <p className="text-base">
            Meet Luna, a friendly cat looking for a home.
          </p>
        </Card>
      </Section>

      <Section title="Badge">
        <div className="flex flex-wrap gap-4">
          <Badge variant="adoption" />
          <Badge variant="sale" />
        </div>
      </Section>

      <Section title="Spinner">
        <Spinner />
      </Section>

      <Section title="EmptyState">
        <EmptyState message="No pets here yet.">
          <Button>Post an ad</Button>
        </EmptyState>
      </Section>

      <Section title="ErrorMessage">
        <ErrorMessage message="Something went wrong." />
        <ErrorMessage
          message="We could not load the pets."
          onRetry={() => {}}
        />
      </Section>
    </div>
  )
}
