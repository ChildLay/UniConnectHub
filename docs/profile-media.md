# Profile and cover photo uploads

The authenticated profile API accepts JPEG, PNG, and WebP uploads up to 5 MB:

- `POST /api/v1/profiles/me/media/profile` with multipart form field `file`
- `DELETE /api/v1/profiles/me/media/profile`
- `POST /api/v1/profiles/me/media/cover` with multipart form field `file`
- `DELETE /api/v1/profiles/me/media/cover`
- `GET /api/v1/profiles/{userId}/media/{kind}/{fileName}` serves the image only after the caller passes the normal profile visibility and block checks.

The profile response includes `profileImageUrl` and `coverImageUrl`. The browser uses those API paths and sends its bearer token when loading image bytes. User files are validated by MIME type and file signature, stored outside the web root, and named with random identifiers. The `Media:StoragePath` setting defaults to `App_Data/profile-media` under the API content root. Set it to durable shared storage for a multi-instance or container deployment; all API instances must see the same files.

Development startup adds the nullable `Profiles.CoverImageUrl` column if needed. For an existing UAT or production SQL Server database, apply `docs/cover-photo-schema.sql` before deploying this API version. The photo files themselves must also be backed up and persisted according to the configured media path.
