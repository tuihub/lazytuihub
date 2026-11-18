# GET /v1:describeWorker

- **Method:** `GET`
- **Path:** `/v1:describeWorker`
- **Operation ID:** `WorkerService_DescribeWorker`
- **Tags:** WorkerService

Reports this instance's current resources and exported operations.
The coordinator reads this inventory when it plans a workflow.

## Effective servers

- `/`

## Responses

### 200 OK

**Content type:** `application/json`

schema: [DescribeWorkerResponse](../models/DescribeWorkerResponse.md)

Response for DescribeWorker.

- **`instance`**: `string`, readOnly

  Output only. The identity of this worker.
- **`ownedOperations`**: `array of string`, readOnly

  Output only. Exported operations bound to one resource owner and one attempt.
- **`repeatableOperations`**: `array of string`, readOnly

  Output only. Exported transforms that may run again after an unavailable response.
- **`resources`**: `array of string`, readOnly

  Output only. Resource names contributed by this instance.
