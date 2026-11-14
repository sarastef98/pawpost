Issue: #1 https://github.com/sarastef98/pawpost/issues/1

# Create a pet ad

## Summary
Let users post an ad for an animal, either for adoption or for sale. An ad has a photo, species, age and description, and Sale ads also have a price.

## User story
As a user, I want to post an ad for an animal, so that I can give it away for adoption or sell it.

## Acceptance criteria
- [ ] A logged-in user can create an ad with type `Adoption` or `Sale`.
- [ ] An ad requires a photo, species, age and description; a request missing any of them is rejected with 400 and `ProblemDetails`.
- [ ] A `Sale` ad requires a price; a `Sale` request without a price is rejected with 400.
- [ ] Input is validated on the server (lengths, ranges, enum values).
- [ ] The photo upload is checked for size, content type and extension, and stored under a server-generated file name.
- [ ] The ad is stored with the logged-in user as its owner.
- [ ] A request from a user who is not logged in is rejected with 401.
- [ ] A successful create returns 201 with a response DTO that does not expose the owner's email or phone.

## Out of scope
- Messaging between buyer and owner ("maybe later" in the notes).
- Browsing ads (separate issue).
- Editing and closing ads.

## Open questions
- Can an `Adoption` ad have a small fee? The client is not sure. Until decided, `Adoption` ads have no price.
- Is one photo enough, or are multiple photos allowed? The notes say "a photo".
- Does a new ad go live (`Active`) right away, or start as `Draft`?
- Are breed and location required for an ad? The notes do not mention them.
- What are the limits for photo size and file types, description length, and the age format (years, months, free text)?
- Is species a fixed list or free text?
- Which currency is used for the price?
- How do users sign up and log in? The notes do not cover accounts.

## Source
The client wants users to post ads for animals, either given away for adoption or sold. An ad needs a photo, species, age and description. Sale ads need a price. The client is unsure whether adoption ads can carry a small fee.
