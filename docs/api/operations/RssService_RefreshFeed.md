# POST /v1/feeds/{feed}:refresh

- **Method:** `POST`
- **Path:** `/v1/feeds/{feed}:refresh`
- **Operation ID:** `RssService_RefreshFeed`
- **Tags:** RssService

Fetches one subscription and streams its entries through parsing and storage.
Waits for this finite pipeline execution. Repeated refreshes upsert entries
by their source identity and preserve read state; HTTP fetches can repeat.
Identity uses the RSS guid or Atom id, then the resolved entry link, and
finally a content hash. Without a stable ID or link, content edits can
produce a new article identity.
Cancellation or a node error can leave already committed entries in place.
This method does not promise atomic feed snapshots or exactly-once delivery.
An unknown subscription returns NOT_FOUND. Upstream or parsing failure
returns UNAVAILABLE; callers can retry a refresh without duplicating entries.
A missing node in the configured pipeline returns FAILED_PRECONDITION.

## Effective servers

- `/`

## Path parameters

- **`feed` (required)**: `string`

  The feed id.

## Request body

**Required:** `true`

**Content type:** `application/json`

schema: [RefreshFeedRequest](../models/RefreshFeedRequest.md)

Request for RefreshFeed.

- **`name` (required)**: `string`

  Required. The subscription name, `feeds/{feed}`.

## Responses

### 200 OK

**Content type:** `application/json`

schema: [RefreshFeedResponse](../models/RefreshFeedResponse.md)

Response for RefreshFeed.

- **`processedArticleCount`**: `integer`, format: `int32`

  Number of entries processed, including existing entries that were updated.
