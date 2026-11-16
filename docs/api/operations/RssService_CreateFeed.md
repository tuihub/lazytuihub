# POST /v1/feeds

- **Method:** `POST`
- **Path:** `/v1/feeds`
- **Operation ID:** `RssService_CreateFeed`
- **Tags:** RssService

Creates a subscription without making a network request.
A duplicate ID or URI returns ALREADY_EXISTS. A repeated request_id with
the same inputs returns the original subscription, including after restart.
Reusing that ID for different inputs returns INVALID_ARGUMENT.

Example HTTP request, with BASE_URL set to the service base URL:

```sh
curl -X POST "${BASE_URL}/v1/feeds?requestId=123e4567-e89b-42d3-a456-426614174000" \
  -H 'Content-Type: application/json' \
  -d '{"uri":"https://example.com/feed.xml","displayName":"Example"}'
```

## Effective servers

- `/`

## Query parameters

- **`feedId`**: `string`

  Optional. A lowercase, hyphenated UUID for the subscription. Omission
  generates one. An existing ID returns ALREADY_EXISTS.
- **`requestId`**: `string`

  Optional. A UUID version 4 identifying this creation request. Successful
  request IDs remain deduplicated for the lifetime of the subscription.

## Request body

**Required:** `true`

**Content type:** `application/json`

schema: [Feed](../models/Feed.md)

A locally persisted RSS or Atom subscription.

- **`uri` (required)**: `string`

  Required. The absolute HTTP or HTTPS feed URI. Subscribing stores this URI
  without fetching it. Each URI can have one subscription in this workspace.
- **`displayName`**: `string`

  Optional. A reader-facing label. An omitted or empty value uses the URI host.
- **`name`**: `string`

  Identifier. The subscription name, in the form `feeds/{feed}`.

## Responses

### 200 OK

**Content type:** `application/json`

*Schema `Feed` is shown above.*
