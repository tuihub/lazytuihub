# GET /v1/articles

- **Method:** `GET`
- **Path:** `/v1/articles`
- **Operation ID:** `ArticleService_ListArticles`
- **Tags:** ArticleService

Lists articles newest-ingested first through the reading pipeline.
The optional filter supports source equality,
`read = true` and `read = false`; an empty filter includes both states.
Pagination is a live keyset traversal, not a snapshot across refreshes or
read-state changes. A token is bound to the filter.

## Effective servers

- `/`

## Query parameters

- **`pageSize`**: `integer`, format: `int32`

  Optional. Number of articles per page. Zero uses 50; negative values return
  INVALID_ARGUMENT. This prototype has no additional maximum.
- **`pageToken`**: `string`

  Optional. An opaque token from a response with the same filter.
- **`filter`**: `string`

  Optional. Equality on source (a quoted string) and read (true or false).
  Combine the two with AND, for example `source = "feeds/abc" AND read = false`.

## Responses

### 200 OK

**Content type:** `application/json`

schema: [ListArticlesResponse](../models/ListArticlesResponse.md)

Response for ListArticles.

- **`articles`**: array of [Article](../models/Article.md)

  The articles in this page.
- **`nextPageToken`**: `string`

  A continuation token, or empty at the end of the collection.
