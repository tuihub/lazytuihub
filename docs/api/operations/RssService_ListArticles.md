# GET /v1/feeds/{feed}/articles

- **Method:** `GET`
- **Path:** `/v1/feeds/{feed}/articles`
- **Operation ID:** `RssService_ListArticles`
- **Tags:** RssService

Lists articles newest-ingested first through the reading pipeline.
Use `feeds/-` to aggregate subscriptions. The optional filter supports
`read = true` and `read = false`; an empty filter includes both states.
Pagination is a live keyset traversal, not a snapshot across refreshes or
read-state changes. A token is bound to the parent and filter.

## Effective servers

- `/`

## Path parameters

- **`feed` (required)**: `string`

  The feed id.

## Query parameters

- **`pageSize`**: `integer`, format: `int32`

  Optional. Number of articles per page. Zero uses 50; negative values return
  INVALID_ARGUMENT. This prototype has no additional maximum.
- **`pageToken`**: `string`

  Optional. An opaque token from a response with the same parent and filter.
- **`filter`**: `string`

  Optional. Empty, `read = true`, or `read = false`.

## Responses

### 200 OK

**Content type:** `application/json`

schema: [ListArticlesResponse](../models/ListArticlesResponse.md)

Response for ListArticles.

- **`articles`**: array of [Article](../models/Article.md)

  The articles in this page.
- **`nextPageToken`**: `string`

  A continuation token, or empty at the end of the collection.
