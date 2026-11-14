# PawsPost design brief

## Feel
Warm, friendly and trustworthy. The animal photos are the hero. Plain, short language.

## Audience
People adopting or selling pets, many of them on phones. Design mobile first, then widen.

## Colors
| Token | Value | Use |
|---|---|---|
| primary | #C2410C | main buttons, links |
| primary-hover | #9A3412 | hover and pressed |
| primary-soft | #FFEDD5 | highlights, selected items |
| background | #FFFBF5 | page background |
| surface | #FFFFFF | cards, forms |
| text | #292524 | body text |
| muted | #57534E | secondary text |
| border | #E7E5E4 | lines and input borders |
| success | #15803D | Adoption badge, confirmations |
| danger | #B91C1C | errors, destructive actions |

Text must have a contrast ratio of at least 4.5:1 against its background.

## Typography
One sans-serif font (Inter, falling back to the system font). Sizes 14, 16, 20, 24, 32. Headings semibold, body regular.

## Spacing and shape
4px spacing scale. Cards and inputs have 8px rounded corners, badges are fully rounded. Soft shadows only. Page content is at most 1100px wide and centered.

## Components
- Buttons: primary, secondary (outlined), danger. One primary button per screen.
- Inputs: a visible label above, helper or error text below, clear focus outline.
- Ad card: photo on top, title, a badge (Adoption in success color, Sale in primary-soft), price for Sale ads, location.
- Header: logo on the left, navigation on the right. Simple footer.

## States
- Loading: a spinner or skeleton, never a blank page.
- Empty: a friendly sentence and one clear action.
- Error: say what went wrong in plain words and what to do, with a retry where it makes sense.

## Tone of text
Friendly, short, no jargon. Errors never show technical details.

## Accessibility
Visible keyboard focus, every control usable with a keyboard, labels on all inputs, alt text on images.
