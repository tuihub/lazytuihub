# Article

**Type:** `object`

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

**Example:**

```json
{
  "name": "",
  "title": "",
  "uri": "",
  "content": "",
  "publishTime": "2000-01-01T00:00:00.000Z",
  "read": true
}
```
