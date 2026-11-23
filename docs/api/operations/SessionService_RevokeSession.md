# POST /v1/sessions:revokeSession

- **Method:** `POST`
- **Path:** `/v1/sessions:revokeSession`
- **Operation ID:** `SessionService_RevokeSession`
- **Tags:** SessionService

Revokes the session presented as the bearer credential for this call.

## Effective servers

- `/`

## Request body

**Required:** `true`

**Content type:** `application/json`

`object`

Request for RevokeSession.

## Responses

### 200 OK

**Content type:** `application/json`

`object`

Response for RevokeSession.
