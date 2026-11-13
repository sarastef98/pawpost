# Browse pet ads without an account

## Summary
Let anyone, including visitors without an account, browse the list of ads.

## User story
As a visitor, I want to browse ads without creating an account, so that I can find an animal before deciding to sign up.

## Acceptance criteria
- [ ] An unauthenticated request to list ads succeeds with 200.
- [ ] The list only contains `Active` ads (project default).
- [ ] The list is paginated and has a maximum page size.
- [ ] An ad response does not expose the owner's email or phone.

## Out of scope
- Messaging between buyer and owner (mentioned as "maybe later").
- Creating ads (separate issue).

## Open questions
- Is a detail view for a single ad needed, or only the list?
- Does the client want filtering or sorting (species, type, price)? The notes do not mention it.
- Can visitors see how to contact the owner? Contact details stay private by default.

## Source
The client said anyone should be able to browse ads without an account.
