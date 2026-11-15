# Client meeting, 14 Nov

- The home page shows the list of active pet ads as cards, newest first. No login needed.
- Each card shows the ad's title, a badge for Adoption or Sale, the price for Sale ads, and the location.
- Visitors can filter the list by species and by type (Adoption or Sale).
- The list is paged, 20 ads per page, with Previous and Next buttons.
- Clicking a card opens a details page for that ad with all its information.
- The details page is only for logged-in users. A visitor who is not logged in and opens it is redirected to the login page.
- If there are no ads, or no ads match the filters, show a friendly message and a way to clear the filters.
- If the list can't be loaded, show a clear error with a Try again button.
- Not now: photos, search by text, sorting options, saving favorites.
- Not decided yet: whether the details API itself should require login, and whether a visitor is sent back to the ad they wanted after logging in.
