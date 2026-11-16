import type { Ref } from 'react'
import { Link } from 'react-router-dom'
import EmptyState from '../../components/EmptyState'
import PetAdCard from '../petAds/PetAdCard'
import type { AssistantResult } from './assistantResult'

type AssistantAnswerProps = AssistantResult & {
  headingRef?: Ref<HTMLHeadingElement>
}

// The answer is shown as plain text (never as HTML). The ads come only from
// the response, the answer text is never searched for ads.
export default function AssistantAnswer({
  answer,
  ads,
  headingRef,
}: AssistantAnswerProps) {
  return (
    <div className="flex flex-col gap-4">
      <section className="flex flex-col gap-2">
        <h2 ref={headingRef} tabIndex={-1} className="text-xl">
          Answer
        </h2>
        <p className="text-base break-words whitespace-pre-line text-text">
          {answer}
        </p>
      </section>
      {ads.length > 0 ? (
        <section className="flex flex-col gap-4">
          <h2 className="text-xl">Matching ads</h2>
          <ul className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            {ads.map((ad) => (
              <li key={ad.id}>
                <PetAdCard ad={ad} />
              </li>
            ))}
          </ul>
        </section>
      ) : (
        <EmptyState message="No ads match your question yet. Try describing the pet in other words.">
          <Link to="/" className="font-semibold text-primary underline">
            Browse all ads
          </Link>
        </EmptyState>
      )}
    </div>
  )
}
