# Client meeting, 15 Nov

- Logged-in users get a page to create a pet ad. It is linked from the header with a clear "Post an ad" button.
- A visitor who is not logged in and opens the page is redirected to the login page.
- The form fields are: title, type (Adoption or Sale), species, breed (optional), age, description and location (optional).
- Age is entered in months.
- The price field is only shown when the type is Sale, and it is required then. Adoption ads have no price.
- Every field shows a clear message next to it when it is wrong, using the rules the server already enforces (required fields, maximum lengths, price above 0 for Sale).
- The Submit button is disabled while the ad is being saved, so an ad can't be posted twice by double clicking.
- If the server rejects the form, show its messages next to the matching fields. If saving fails for any other reason, show a clear error and keep what the user typed.
- After the ad is created, the user is taken to the new ad's details page and sees a short success message.
- New ads are active right away.
- Not now: photos, saving a draft, editing or closing an ad, a preview before posting.
- Not decided yet: whether the species is a free text field or a fixed list (dog, cat, and so on), and whether the age is entered as months only or as years and months.
