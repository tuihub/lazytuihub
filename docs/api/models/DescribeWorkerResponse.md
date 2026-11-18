# DescribeWorkerResponse

**Type:** `object`

Response for DescribeWorker.

- **`instance`**: `string`, readOnly

  Output only. The identity of this worker.
- **`ownedOperations`**: `array of string`, readOnly

  Output only. Exported operations bound to one resource owner and one attempt.
- **`repeatableOperations`**: `array of string`, readOnly

  Output only. Exported transforms that may run again after an unavailable response.
- **`resources`**: `array of string`, readOnly

  Output only. Resource names contributed by this instance.

**Example:**

```json
{
  "instance": "",
  "resources": [
    ""
  ],
  "repeatableOperations": [
    ""
  ],
  "ownedOperations": [
    ""
  ]
}
```
