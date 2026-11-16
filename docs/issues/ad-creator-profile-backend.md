Issue: #44
URL: https://github.com/sarastef98/pawpost/issues/44

# Add public display name, ad creator and public creator profile API

Layer: backend
Depends on: #3, #2, #18

## Summary
Give every user a unique public display name (chosen at registration, generated for existing users), show the creator in the ad list and ad details responses, and add a public profile endpoint that returns the creator's display name and their `Active` ads. The email, phone and `UserName` (the email today) are never exposed.

Decisions (client): the profile URL identifies the creator by the user id (a Guid). The creator's id is a public field in the list, details and profile responses; it is not contact data. The profile endpoint is public, like browsing the list.

## User story
As a visitor, I want to see who created an ad and open that person's profile with all their ads, so that I can find more ads from the same person.

## Acceptance criteria
- [ ] The user has a separate public display name field. It is not the email and not `UserName`. It is added by an EF migration.
- [ ] `POST /api/auth/register` gets a new required field for the display name, chosen by the user. Missing, empty or invalid input returns 400 with `ValidationProblemDetails` on that field. The rest of the register contract from #3 stays as it is.
- [ ] Display name rules: 3 to 30 characters after trimming; allowed characters are letters, digits, spaces, hyphen and underscore. The name is trimmed before it is validated and stored.
- [ ] Display names are unique, and uniqueness is case-insensitive ("Anna" and "anna" are the same name). Registering with a name that is already taken returns 409 with `ProblemDetails` and a clear message. The check also holds when two requests arrive at the same time (a unique, case-insensitive index in the database, not only a check in code).
- [ ] The migration gives every existing user a generated default name in the format "Pet lover <4 digits>" (for example "Pet lover 1234"). Generated names are unique (made unique by retrying or adjusting the number) and also follow the case-insensitive uniqueness rule.
- [ ] A user without a display name never exists after the migration (the column is required).
- [ ] The creator is a public object in the responses with the creator's id (Guid) and the public display name. The id is public and is not contact data. If the existing `OwnerId` field is already public in a response, it keeps its name there; otherwise the id is exposed only as the creator id inside the creator object. Email, phone and `UserName` stay private and are never in any response.
- [ ] The ad details response (`GET /api/pet-ads/{id}`, #18) includes the creator (id and display name). Existing fields and the login rules of #18 stay as they are.
- [ ] Each item of the ad list response (`GET /api/pet-ads`, #2) includes the creator (id and display name). Existing fields and behavior stay as they are.
- [ ] A new public profile endpoint (route chosen in the plan, using the creator's user id, for example `GET /api/creators/{id}` or under `/api/users`) returns 200 with the creator's display name and a paginated list of the creator's ads. It requires no login. Nothing else about the user is returned.
- [ ] The profile lists only `Active` ads. `Draft`, `Reserved` and `Closed` ads never appear, also not for the owner viewing their own profile.
- [ ] The profile list is paginated with `page` (starting at 1) and `pageSize`: default 20, maximum 100, as in #2. A `pageSize` above the maximum or a page below 1 returns 400 with `ValidationProblemDetails`. The ad items have the same public fields as the list items in #2.
- [ ] Ads are sorted newest first. The query is `AsNoTracking()` and lives in the service. An index is added by the migration if the profile filter needs one (for example owner, status and created date).
- [ ] An unknown creator returns 404 with `ProblemDetails`. A creator with no `Active` ads returns 200 with an empty list and `totalCount` 0.
- [ ] Access rule: the profile endpoint is public, like browsing the list. The creator shown on the details response follows the existing rules of #18 (no change to its login rules).
- [ ] No response (register, list, details, profile) contains an email, phone or `UserName`. DTOs have public fields only. The user entity is never returned.
- [ ] The display name is not written to logs together with other personal data, and the email is not logged (existing rule).
- [ ] Business rules (uniqueness, default name generation) live in services. Controllers only call services.
- [ ] Tests cover: registration with a display name succeeds; missing or invalid display name (400, including too short, too long and disallowed characters, and trimming); duplicate name rejected with 409, also with different letter case; existing user without a name gets a default in the "Pet lover <4 digits>" format, and generated names are unique; creator (id and display name) shown in the list and details responses; profile success without login; profile shows only `Active` ads (draft, reserved and closed are hidden); profile pagination, the default page size of 20 and the page size cap (400); unknown creator (404); empty profile; and that no email, phone or `UserName` is in any of these responses. Tests use synthetic users only.

## Out of scope
- Editing the display name later (separate feature).
- Any other profile content (bio, photo, contact details, ratings). The profile shows only the name and the ads.
- Showing the creator's non-`Active` ads, including to the owner.
- Screens, links and layout (see `ad-creator-profile-frontend.md`).
- Regenerating the frontend `schema.d.ts` (separate step after merge).
- Messaging the creator.
- Adding the creator to the 201 response of `POST /api/pet-ads` and to `GET /api/auth/me`.

## Open questions
None.

## Source
Meeting of 15 Nov (`docs/meeting-notes/2026-11-15-ad-creator-profile.md`). Every ad shows who created it, with a link to a profile page that lists all the creator's ads. The public name is chosen at registration, is never the email, and is unique. Existing users get a generated unique default name. The profile shows only the name and the creator's `Active` ads, paginated. Contact details stay private. Editing the name is a separate feature.
