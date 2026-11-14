---
paths:
  - "PawsPost/Data/Migrations/**"
---

# Migration rules

- Create migrations only with `dotnet ef migrations add <Name> --project PawsPost`. Never write or hand-edit the generated files, including the model snapshot.
- Never edit a migration that is already in `main`. Add a new migration instead.
- One migration per issue. Name it after the change: `AddPetAds`, `AddOwnerToPetAds`.
- Read the generated migration before committing. If it drops a column or table, or changes a column type, stop and ask. That can lose data.
- If a migration is wrong and it is not in `main` yet, remove it with `dotnet ef migrations remove --project PawsPost`, fix the model, and create it again.
