# AuthenticateUserResponse

**Type:** `object`

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

**Example:**

```json
{
  "token": "",
  "account": "",
  "expireTime": "2000-01-01T00:00:00.000Z"
}
```
