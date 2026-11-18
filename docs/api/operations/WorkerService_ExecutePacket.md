# POST /v1:executePacket

- **Method:** `POST`
- **Path:** `/v1:executePacket`
- **Operation ID:** `WorkerService_ExecutePacket`
- **Tags:** WorkerService

Runs one registered operation and returns one complete output packet.
The coordinator supplies its worker bearer credential over a trusted channel.
Repeatable transforms may move to another candidate after loss of a response.
For an owner-bound write a lost response leaves the outcome unknown.
UNAVAILABLE covers missing capabilities and temporarily unreachable inputs.
Invalid packets and rejected operations must not be retried on another worker.

## Effective servers

- `/`

## Request body

**Required:** `true`

**Content type:** `application/json`

schema: [ExecutePacketRequest](../models/ExecutePacketRequest.md)

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

## Responses

### 200 OK

**Content type:** `application/json`

schema: [ExecutePacketResponse](../models/ExecutePacketResponse.md)

Response for ExecutePacket.

- **`output`**: `string`, format: `bytes`, readOnly

  Output only. A UTF-8 JSON packet containing the result value and its data reference.
