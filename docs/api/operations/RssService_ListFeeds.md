# GET /v1/feeds

- **Method:** `GET`
- **Path:** `/v1/feeds`
- **Operation ID:** `RssService_ListFeeds`
- **Tags:** RssService

Lists subscriptions in stable resource-ID order through the reading pipeline.

## Effective servers

- `/`

## Query parameters

- **`pageSize`**: `integer`, format: `int32`

  Optional. Number of subscriptions per page. Zero uses 50; negative values
  return INVALID_ARGUMENT. This prototype has no additional maximum.
- **`pageToken`**: `string`

  Optional. An opaque continuation token from a previous response.

## Responses

### 200 OK

**Content type:** `application/json`

schema: [ListFeedsResponse](../models/ListFeedsResponse.md)

Response for ListFeeds.

- **`feeds`**: array of [Feed](../models/Feed.md)

  The subscriptions in this page.
- **`nextPageToken`**: `string`

  A continuation token, or empty at the end of the collection.
