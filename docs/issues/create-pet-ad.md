# Create a pet ad

## Summary
Let a user post an ad for an animal, either to give it away for adoption or to sell it.

## User story
As a user, I want to post an ad for an animal, so that I can find a new home for it or sell it.

## Acceptance criteria
- [ ] An authenticated user can create an ad with type `Adoption` or `Sale`.
- [ ] An ad requires a photo, species, age and a description.
- [ ] A `Sale` ad requires a price.
- [ ] A request with a missing or invalid field is rejected with 400 and a `ProblemDetails` body.
- [ ] A request from a user who is not logged in is rejected with 401.
- [ ] The created ad is owned by the user who created it.

## Out of scope
- Messaging between buyer and owner (mentioned as "maybe later").
- Browsing ads (separate issue).

## Open questions
- Can an `Adoption` ad have a small fee? The client is not sure. Until decided, price is absent for `Adoption` (project default).
- Are breed and location required? The notes do not mention them (both are in the default domain model).
- How many photos can an ad have, and what are the size and format limits? The notes only say "a photo".
- Does a new ad start as `Draft` or `Active`? The notes do not say.
- What are the length limits for the description, and what format and unit is age (years, months)?
- Is the list of species fixed or free text?
- How do users sign up and log in? The notes do not cover accounts.

## Source
The client wants users to post ads for animals, either for adoption or for sale. An ad needs a photo, species, age and description. Sale ads need a price.
