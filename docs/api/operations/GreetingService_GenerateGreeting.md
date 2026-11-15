# POST /v1:generateGreeting

- **Method:** `POST`
- **Path:** `/v1:generateGreeting`
- **Operation ID:** `GreetingService_GenerateGreeting`
- **Tags:** GreetingService

Generates a greeting for the requested recipient.

Returns `Hello, recipient!`, substituting `World` when `recipient` is omitted
or empty. Other values, including Unicode text, are used unchanged.
This stateless computation does not read or modify persistent resources.
Repeating the same request returns the same result and has no side effects.

Example request:

```json
{"recipient":"Ada"}
```

Example successful response (HTTP 200):

```json
{"message":"Hello, Ada!"}
```

Example HTTP request, with `BASE_URL` set to the server's base URL:

```sh
curl -X POST "${BASE_URL}/v1:generateGreeting" \
  -H 'Content-Type: application/json' \
  -d '{"recipient":"Ada"}'
```

## Effective servers

- `/`

## Request body

**Required:** `true`

**Content type:** `application/json`

schema: [GenerateGreetingRequest](../models/GenerateGreetingRequest.md)

Request for GenerateGreeting.

- **`recipient`**: `string`

  Optional. The recipient of the greeting. An omitted or empty value uses
  `World`. Non-empty values are used unchanged, including Unicode text.

## Responses

### 200 OK

**Content type:** `application/json`

schema: [GenerateGreetingResponse](../models/GenerateGreetingResponse.md)

Response for GenerateGreeting.

- **`message`**: `string`, readOnly

  Output only. The generated greeting in the form `Hello, recipient!`.
  When the request omits `recipient` or supplies an empty value, this is
  `Hello, World!`.
