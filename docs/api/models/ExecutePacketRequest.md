# ExecutePacketRequest

**Type:** `object`

Request for ExecutePacket.

- **`input` (required)**: `string`, format: `bytes`

  Required. A UTF-8 JSON packet with a typed value and optional data reference.
- **`instance` (required)**: `string`

  Required. The configured execution instance expected by the coordinator.
- **`nodeType` (required)**: `string`

  Required. The registered node type that defines the settings and packet codec.
- **`settings` (required)**: `string`, format: `bytes`

  Required. The node settings encoded as a UTF-8 JSON object.
- **`repeatable`**: `boolean`

  Optional. True for a repeatable transform. The worker checks the execution
  mode against its current registration before invoking the implementation.
- **`requiredResources`**: `array of string`

  Optional. Resources that must still be available when executing this packet.

**Example:**

```json
{
  "instance": "",
  "nodeType": "",
  "settings": "",
  "input": "",
  "requiredResources": [
    ""
  ],
  "repeatable": true
}
```
