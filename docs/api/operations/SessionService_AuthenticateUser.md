# POST /v1/sessions:authenticateUser

- **Method:** `POST`
- **Path:** `/v1/sessions:authenticateUser`
- **Operation ID:** `SessionService_AuthenticateUser`
- **Tags:** SessionService

Exchanges a username and password for a revocable user session.

## Effective servers

- `/`

## Request body

**Required:** `true`

**Content type:** `application/json`

schema: [AuthenticateUserRequest](../models/AuthenticateUserRequest.md)

Request for AuthenticateUser.

- **`password` (required)**: `string`

  Required. The user's password.
- **`username` (required)**: `string`

  Required. The user's configured sign-in name.

## Responses

### 200 OK

**Content type:** `application/json`

schema: [AuthenticateUserResponse](../models/AuthenticateUserResponse.md)

Response for AuthenticateUser.

- **`account`**: `string`, readOnly

  Output only. The user's personal account ID. Business calls default to this
  account. Use the X-LazyTuiHub-Account header to select an account that
  shares specific resources with the user.
- **`expireTime`**: `string`, format: `date-time`, readOnly

  Output only. Time at which the credential expires.
- **`token`**: `string`, readOnly

  Output only. Present this credential in the Authorization header as a
  bearer token. RevokeSession invalidates the token before it would expire.
