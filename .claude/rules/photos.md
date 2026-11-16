---
paths:
  - "PawsPost/**/*Photo*.cs"
  - "PawsPost.Tests/**/*Photo*.cs"
---

# Photo rules

Source: `docs/meeting-notes/2026-11-15-ad-photos.md`. Adds to the file upload rules in CLAUDE.md.

## Storage

- Files go through the `IPhotoStorage` interface. Nothing else touches the disk. The first implementation uses a local folder outside the public web root and outside git, so cloud storage can replace it later.
- The database stores only metadata: ad, generated file name, content type, size, is-main flag. Never the image bytes.
- Photos are served through an API URL like `/api/pet-ads/{id}/photos/{photoId}`. Responses never contain storage paths or server folder names.

## Upload validation (server side, always)

- Maximum 5 MB per photo and 10 photos per ad.
- Allowed formats: JPG, PNG and WebP only. No HEIC, no GIF, no SVG (SVG can contain scripts).
- Check the real file content (magic bytes), not only the extension or the content type header.
- Generate our own file name. Never use the uploaded name for a path.

## Ad rules

- An ad needs at least one photo to be created or published.
- Only the owner can add, remove or change the main photo. Authentication alone is not enough.
- An ad always has exactly one main photo. The first uploaded photo is main by default. The owner can pick any other photo of the ad.
- If the main photo is deleted, the next photo becomes main. The last remaining photo cannot be deleted.
- Existing ads without a photo stay visible and show a default paw placeholder. The placeholder is only a default image in the response, never stored as a photo of the ad. The owner must add a real photo on the next edit.

## Tests

- Use a fake or temporary-folder `IPhotoStorage`. Tests never need real external storage.
- Use only small synthetic images, no real personal photos.
- Cover: wrong format, content that does not match the extension, too large, 11th photo, not logged in, not the owner, deleting the last photo, deleting the main photo.
