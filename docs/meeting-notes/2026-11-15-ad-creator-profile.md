# Meeting notes: Ad creator and creator profile

Date: 2026-11-15
Topic: Show who created each ad, and let visitors open the creator's profile with all of their ads.

## What the client wants

- Every ad shows who created it.
- The ad details page shows the creator and links to the creator's profile.
- The creator's profile page shows all of the creator's ads.
- Everyone who can see the ads can see the creator's name and profile (same access rule as browsing ads).

## Decided

- The public name is chosen by the user at registration (new required field on the register form). It is never the email.
- Existing users get a generated default name (for example "Pet lover 1234") until they can change it.
- The profile shows only the public name and the list of the creator's ads. Nothing else.
- The profile lists only `Active` ads. Drafts, reserved and closed ads are not shown.
- Public names must be unique. Registration with a name that is already taken is rejected with a clear error. Generated default names are also unique.
- Editing the public name later is a separate feature, not part of this work.

## Technical constraints (from the team)

- Contact details stay private. The creator's email and phone must never appear on the ad, in the profile or in any public response (rule from CLAUDE.md).
- Today a user's `UserName` is their email address, so it cannot be shown publicly. The creator needs a separate public display name.
- The profile lists only ads that are visible to everyone (status `Active`). Draft ads stay private to their owner.
- The profile list is paginated with a maximum page size, like every list.
- The API returns DTOs with only public fields. Never return the user entity.
- Existing users have no display name yet. They need a default until they set one.
- Use only synthetic demo users and ads.

## Layers

- Backend: yes (public display name on the user, creator in the browse and details responses, public profile endpoint with the creator's ads).
- Frontend: yes (creator shown on the browse cards and on the details page, link to the profile, profile page with the creator's ads).
- Both layers should be separate issues. The frontend depends on the backend.

## Not decided yet

- Nothing. All questions are answered.
