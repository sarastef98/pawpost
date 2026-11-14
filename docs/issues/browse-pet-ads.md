Issue: #2 https://github.com/sarastef98/pawpost/issues/2

# Browse pet ads without an account

## Summary
Let anyone, including visitors without an account, browse the list of ads.

## User story
As a visitor, I want to browse ads without creating an account, so that I can find an animal to adopt or buy.

## Acceptance criteria
- [ ] The ad list endpoint can be called without authentication.
- [ ] Only `Active` ads are returned.
- [ ] The list is paginated and has a maximum page size.
- [ ] Responses use DTOs and never expose the owner's email or phone.
- [ ] A page size above the maximum, or an invalid page number, is rejected with 400 and `ProblemDetails`.

## Out of scope
- Messaging between buyer and owner ("maybe later" in the notes).
- Creating, editing and closing ads.

## Open questions
- Should visitors also be able to open a single ad's detail page without an account? The notes only say "browse".
- Are filters needed (species, type, price, location)? The notes do not mention them.
- What sort order should the list use (newest first by default)?
- What should the default and maximum page size be?
- How can a buyer contact an owner while contact details are private and messaging is only "maybe later"?

## Source
The client said anyone should be able to browse ads without an account.
