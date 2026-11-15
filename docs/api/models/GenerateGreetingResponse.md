# GenerateGreetingResponse

**Type:** `object`

Response for GenerateGreeting.

- **`message`**: `string`, readOnly

  Output only. The generated greeting in the form `Hello, recipient!`.
  When the request omits `recipient` or supplies an empty value, this is
  `Hello, World!`.

**Example:**

```json
{
  "message": ""
}
```
