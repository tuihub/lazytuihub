# POST /v1/articles/{article}:markRead

- **Method:** `POST`
- **Path:** `/v1/articles/{article}:markRead`
- **Operation ID:** `ArticleService_MarkArticleRead`
- **Tags:** ArticleService

Marks an article as read and returns its current state. Repeating the call
has the same effect. Unknown names return NOT_FOUND.

## Effective servers

- `/`

## Path parameters

- **`article` (required)**: `string`

  The article id.

## Request body

**Required:** `true`

**Content type:** `application/json`

schema: [MarkArticleReadRequest](../models/MarkArticleReadRequest.md)

Request for MarkArticleRead.

- **`name` (required)**: `string`

  Required. The article name, `articles/{article}`.

## Responses

### 200 OK

**Content type:** `application/json`

schema: [Article](../models/Article.md)

An article collected from a source in this workspace.

- **`content`**: `string`, readOnly

  Output only. The entry content or summary. It may contain untrusted HTML;
  a rendering client must sanitize it before displaying it as HTML.
- **`name`**: `string`

  Identifier. The article name, `articles/{article}`.
- **`publishTime`**: `string`, format: `date-time`, readOnly

  Output only. The source publication time, omitted when absent or invalid.
- **`read`**: `boolean`, readOnly

  Output only. Whether the user has marked this article as read.
- **`source`**: `string`, readOnly

  Output only. The source resource name supplied by the producer, such as `feeds/{feed}`.
- **`title`**: `string`, readOnly

  Output only. The source entry title, which can be empty.
- **`uri`**: `string`, readOnly

  Output only. The resolved entry URI, or empty when none was supplied.
