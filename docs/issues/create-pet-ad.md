Issue: #1 https://github.com/sarastef98/pawpost/issues/1

# Create a pet ad

## Summary
Let users post an ad for an animal, either for adoption or for sale. An ad has species, age and description, and Sale ads also have a price. A photo is not required in this issue.

## User story
As a user, I want to post an ad for an animal, so that I can give it away for adoption or sell it.

## Requirements
- A new ad starts in the `Active` state.
- `Adoption` ads have no price.
- `Sale` ads require a price greater than zero.
- Photo upload is out of scope for this issue and will be handled in a separate issue.

## Acceptance criteria
- [ ] A logged-in user can create an ad with type `Adoption` or `Sale`.
- [ ] A new ad is created in the `Active` state.
- [ ] An ad requires species, age and description; a request missing any of them is rejected with 400 and `ProblemDetails`.
- [ ] `Adoption` ads do not include a price.
- [ ] A `Sale` ad requires a price greater than zero; a `Sale` request without a price or with a non-positive price is rejected with 400.
- [ ] Input is validated on the server (lengths, ranges, enum values).
- [ ] Photo upload is out of scope for this issue and will be handled in a separate issue.
- [ ] The ad is stored with the logged-in user as its owner.
- [ ] A request from a user who is not logged in is rejected with 401.
- [ ] A successful create returns 201 with a response DTO that does not expose the owner's email or phone.

## Out of scope
- Photo upload (separate issue).
- Messaging between buyer and owner ("maybe later" in the notes).
- Browsing ads (separate issue).
- Editing and closing ads.

## Open questions
- Are breed and location required for an ad? The notes do not mention them.
- Is species a fixed list or free text?
- Which currency is used for the price?
- How do users sign up and log in? The notes do not cover accounts.

## Source
The client wants users to post ads for animals, either given away for adoption or sold. New ads start as `Active`. Adoption ads have no price, while Sale ads require a price greater than zero. Photo upload is deferred to a separate issue.
