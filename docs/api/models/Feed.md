# Feed

**Type:** `object`

A locally persisted RSS or Atom subscription.

- **`uri` (required)**: `string`

  Required. The absolute HTTP or HTTPS feed URI. Subscribing stores this URI
  without fetching it. Each URI can have one subscription in this workspace.
- **`displayName`**: `string`

  Optional. A reader-facing label. An omitted or empty value uses the URI host.
- **`name`**: `string`

  Identifier. The subscription name, in the form `feeds/{feed}`.

**Example:**

```json
{
  "name": "",
  "uri": "",
  "displayName": ""
}
```
