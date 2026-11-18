# GET /v1/articles/{article}

- **Method:** `GET`
- **Path:** `/v1/articles/{article}`
- **Operation ID:** `ArticleService_GetArticle`
- **Tags:** ArticleService

Reads an article without changing its read state. Unknown names return
NOT_FOUND. Entry content is untrusted source text, not sanitized HTML.

## Effective servers

- `/`

## Path parameters

- **`article` (required)**: `string`

  The article id.

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
