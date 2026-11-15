import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { client } from '../../api/client'
import Button from '../../components/Button'
import Card from '../../components/Card'
import ErrorMessage from '../../components/ErrorMessage'
import Select from '../../components/Select'
import TextArea from '../../components/TextArea'
import TextField from '../../components/TextField'
import {
  EMPTY_VALUES,
  SPECIES_OPTIONS,
  TYPE_OPTIONS,
  buildRequestBody,
  validateForm,
} from './petAdFormRules'
import type {
  AgeUnit,
  FieldErrors,
  FormValues,
  PetAdType,
  PetSpecies,
} from './petAdFormRules'
import { mapCreateAdErrors, networkCreateErrors } from './petAdFormErrors'

const SUCCESS_NOTICE = 'Your ad is live.'

const typeOptions = [{ value: '', label: 'Choose a type' }, ...TYPE_OPTIONS]
const speciesOptions = [
  { value: '', label: 'Choose a species' },
  ...SPECIES_OPTIONS,
]

const ageUnits: { unit: AgeUnit; label: string }[] = [
  { unit: 'months', label: 'Months' },
  { unit: 'years', label: 'Years' },
]

export default function CreatePetAdForm() {
  const navigate = useNavigate()
  const [values, setValues] = useState<FormValues>(EMPTY_VALUES)
  const [errors, setErrors] = useState<FieldErrors>({})
  const [general, setGeneral] = useState<string | undefined>()
  const [unauthorized, setUnauthorized] = useState(false)
  const [busy, setBusy] = useState(false)
  const submitting = useRef(false)
  const formRef = useRef<HTMLFormElement>(null)
  const alertRef = useRef<HTMLDivElement>(null)

  // After a failed submit, move focus to the first invalid field in form
  // order, or to the general message. The field components mark a field with
  // an error as aria-invalid.
  useEffect(() => {
    const invalidField = formRef.current?.querySelector<HTMLElement>(
      'input[aria-invalid="true"], select[aria-invalid="true"], textarea[aria-invalid="true"]',
    )
    if (invalidField) {
      invalidField.focus()
    } else if (general) {
      alertRef.current?.focus()
    }
  }, [errors, general])

  function setField<K extends keyof FormValues>(key: K, value: FormValues[K]) {
    setValues((current) => ({ ...current, [key]: value }))
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (submitting.current) return

    const clientErrors = validateForm(values)
    const body = buildRequestBody(values)
    if (Object.keys(clientErrors).length > 0 || body === null) {
      setGeneral(undefined)
      setErrors(clientErrors)
      return
    }

    submitting.current = true
    setBusy(true)
    setErrors({})
    setGeneral(undefined)
    try {
      const result = await client.POST('/api/pet-ads', { body })
      if (result.response.status === 401) {
        setUnauthorized(true)
        return
      }
      if (result.response.ok && result.data !== undefined) {
        void navigate(`/pet-ads/${result.data.id}`, {
          state: { notice: SUCCESS_NOTICE },
        })
        return
      }
      const mapped = mapCreateAdErrors(result.response.status, result.error)
      setErrors(mapped.fields)
      setGeneral(mapped.general)
    } catch {
      const mapped = networkCreateErrors()
      setErrors(mapped.fields)
      setGeneral(mapped.general)
    } finally {
      submitting.current = false
      setBusy(false)
    }
  }

  if (unauthorized) return <Navigate to="/login" replace />

  return (
    <div className="mx-auto flex w-full max-w-md flex-col gap-4">
      <h1 className="text-2xl">Post an ad</h1>
      <Card className="flex flex-col gap-4">
        {general && (
          <div ref={alertRef} tabIndex={-1}>
            <ErrorMessage message={general} />
          </div>
        )}
        <form
          ref={formRef}
          noValidate
          aria-busy={busy}
          onSubmit={handleSubmit}
          className="flex flex-col gap-4"
        >
          <TextField
            label="Title"
            value={values.title}
            onChange={(e) => setField('title', e.target.value)}
            errorText={errors.title}
            readOnly={busy}
          />
          <Select
            label="Type"
            options={typeOptions}
            value={values.type}
            onChange={(e) => setField('type', e.target.value as '' | PetAdType)}
            errorText={errors.type}
            disabled={busy}
          />
          <Select
            label="Species"
            options={speciesOptions}
            value={values.species}
            onChange={(e) =>
              setField('species', e.target.value as '' | PetSpecies)
            }
            errorText={errors.species}
            disabled={busy}
          />
          <TextField
            label="Breed"
            helperText="Optional"
            value={values.breed}
            onChange={(e) => setField('breed', e.target.value)}
            errorText={errors.breed}
            readOnly={busy}
          />
          <TextField
            label="Age"
            type="number"
            inputMode="decimal"
            min={0}
            step="any"
            value={values.age}
            onChange={(e) => setField('age', e.target.value)}
            errorText={errors.age}
            readOnly={busy}
          />
          <fieldset className="flex flex-col gap-1" disabled={busy}>
            <legend className="text-sm font-semibold text-text">
              Age unit
            </legend>
            <div className="flex gap-4">
              {ageUnits.map(({ unit, label }) => (
                <label
                  key={unit}
                  className="inline-flex min-h-11 items-center gap-2 text-base"
                >
                  <input
                    type="radio"
                    name="ageUnit"
                    value={unit}
                    checked={values.ageUnit === unit}
                    onChange={() => setField('ageUnit', unit)}
                  />
                  {label}
                </label>
              ))}
            </div>
          </fieldset>
          <TextArea
            label="Description"
            rows={5}
            value={values.description}
            onChange={(e) => setField('description', e.target.value)}
            errorText={errors.description}
            readOnly={busy}
          />
          {values.type === 'Sale' && (
            <TextField
              label="Price"
              inputMode="decimal"
              value={values.price}
              onChange={(e) => setField('price', e.target.value)}
              errorText={errors.price}
              readOnly={busy}
            />
          )}
          <TextField
            label="Location"
            helperText="Optional"
            value={values.location}
            onChange={(e) => setField('location', e.target.value)}
            errorText={errors.location}
            readOnly={busy}
          />
          <Button type="submit" disabled={busy} aria-busy={busy}>
            {busy ? 'Please wait...' : 'Post ad'}
          </Button>
        </form>
      </Card>
    </div>
  )
}
