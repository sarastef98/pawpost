---
paths:
  - "PawsPost/Features/**/*.cs"
  - "PawsPost/Data/**/*.cs"
---

# Creator and public profile rules

Source: `docs/meeting-notes/2026-11-15-ad-creator-profile.md`. Adds to the privacy rules in CLAUDE.md.

- Public responses show the creator's **public display name** only. Never the email, the phone, or `UserName` (it is the email today).
- The display name is a separate field on the user. It is required at registration, chosen by the user, and must be unique. A taken name is rejected with a clear error (400 or 409 as `ProblemDetails`).
- Existing users get a generated default name (for example "Pet lover 1234"). Generated names are also unique.
- Editing the display name is a separate feature. Do not add it here.
- The public profile endpoint shows only the public name and the creator's ads. Nothing else.
- The profile lists only `Active` ads. Drafts, reserved and closed ads never appear. The list is paginated with a maximum page size.
- Same access rule as browsing ads: everyone who can see the ads can see the creator and the profile.
- Return DTOs with public fields only. Never return the user entity.
- Tests: assert that responses contain no email or phone, that only `Active` ads are listed, that a duplicate name is rejected, and that a user without a display name gets a default. Use synthetic users only.
