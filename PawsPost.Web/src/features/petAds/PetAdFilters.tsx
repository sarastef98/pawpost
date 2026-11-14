import { useState } from 'react'
import type { FormEvent, Ref } from 'react'
import Button from '../../components/Button'
import Select from '../../components/Select'
import TextField from '../../components/TextField'
import { MAX_SPECIES_LENGTH } from './petAdListQuery'
import type { AdTypeFilter } from './petAdListQuery'

type PetAdFiltersProps = {
  type: AdTypeFilter
  species: string
  onApply: (type: AdTypeFilter, species: string) => void
  typeRef?: Ref<HTMLSelectElement>
}

const typeOptions = [
  { value: '', label: 'All types' },
  { value: 'Adoption', label: 'Adoption' },
  { value: 'Sale', label: 'Sale' },
]

function toTypeFilter(value: string): AdTypeFilter {
  return value === 'Adoption' || value === 'Sale' ? value : ''
}

// The form stays mounted, so the focused field keeps focus after Apply. When
// the URL filters change (Apply, Back or Forward when the filters differ,
// Clear filters) the inputs are set to the new URL values during render,
// without remounting the form.
export default function PetAdFilters({
  type,
  species,
  onApply,
  typeRef,
}: PetAdFiltersProps) {
  const [typeValue, setTypeValue] = useState<AdTypeFilter>(type)
  const [speciesValue, setSpeciesValue] = useState(species)
  const [synced, setSynced] = useState({ type, species })

  if (synced.type !== type || synced.species !== species) {
    setSynced({ type, species })
    setTypeValue(type)
    setSpeciesValue(species)
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    onApply(typeValue, speciesValue.trim())
  }

  return (
    <form
      aria-label="Filter ads"
      noValidate
      onSubmit={handleSubmit}
      className="flex flex-col gap-4 sm:flex-row sm:items-start"
    >
      <Select
        ref={typeRef}
        label="Type"
        options={typeOptions}
        value={typeValue}
        onChange={(event) => setTypeValue(toTypeFilter(event.target.value))}
      />
      <TextField
        label="Species"
        helperText="For example Dog or Cat"
        maxLength={MAX_SPECIES_LENGTH}
        value={speciesValue}
        onChange={(event) => setSpeciesValue(event.target.value)}
      />
      <Button type="submit" className="sm:mt-6">
        Apply filters
      </Button>
    </form>
  )
}
