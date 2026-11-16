# GET /v1/feeds/{feed}/articles/{article}

- **Method:** `GET`
- **Path:** `/v1/feeds/{feed}/articles/{article}`
- **Operation ID:** `RssService_GetArticle`
- **Tags:** RssService

Reads an article without changing its read state. Unknown names return
NOT_FOUND. Entry content is untrusted source text, not sanitized HTML.

## Effective servers

- `/`

## Path parameters

- **`feed` (required)**: `string`

  The feed id.
- **`article` (required)**: `string`

  The article id.

## Responses

### 200 OK

**Content type:** `application/json`

schema: [Article](../models/Article.md)

A feed entry persisted by the RSS ingestion pipeline.

- **`content`**: `string`, readOnly

  Output only. The entry content or summary. It may contain untrusted HTML;
  a rendering client must sanitize it before displaying it as HTML.
- **`name`**: `string`

  Identifier. The article name, `feeds/{feed}/articles/{article}`.
- **`publishTime`**: `string`, format: `date-time`, readOnly

  Output only. The source publication time, omitted when absent or invalid.
- **`read`**: `boolean`, readOnly

  Output only. Whether the user has marked this article as read.
- **`title`**: `string`, readOnly

  Output only. The source entry title, which can be empty.
- **`uri`**: `string`, readOnly

  Output only. The resolved entry URI, or empty when none was supplied.
