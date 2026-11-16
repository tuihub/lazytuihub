# GET /v1/feeds/{feed}

- **Method:** `GET`
- **Path:** `/v1/feeds/{feed}`
- **Operation ID:** `RssService_GetFeed`
- **Tags:** RssService

Reads a subscription through the shared reading pipeline. Unknown names
return NOT_FOUND.

## Effective servers

- `/`

## Path parameters

- **`feed` (required)**: `string`

  The feed id.

## Responses

### 200 OK

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
