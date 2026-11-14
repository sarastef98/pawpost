import PetAdList from '../petAds/PetAdList'

export default function HomePage() {
  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-2">
        <h1 className="text-2xl">Welcome to PawsPost</h1>
        <p className="text-base text-muted">
          Find a new friend or help a pet find a loving home.
        </p>
      </div>
      <PetAdList />
    </div>
  )
}
