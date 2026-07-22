# MIMISK gRPC API

The public protobuf definition is `proto/mimisk_bridge.proto`.

## Service

```proto
service MIMISKBridge {
  rpc Ping(PingRequest) returns (PingReply);
  rpc SendTelemetry(MIMISKTelemetryFrame) returns (TelemetryAck);
  rpc SendCameraFrame(CameraFrame) returns (TelemetryAck);
  rpc GetPendingCommands(CommandBatchRequest) returns (CommandBatch);
  rpc ReportCommandResult(CommandExecutionResult) returns (TelemetryAck);
  rpc GetManualControls(ManualControlRequest) returns (ManualControlBatch);
  rpc GetExternalReferences(ExternalReferenceRequest) returns (ExternalReferenceBatch);
}
```

## Message groups

### Telemetry

- `DroneState`
- `MiniROVState`
- `TetherState`
- `MIMISKTelemetryFrame`

### Commands

- `MIMISKCommand`
- `CommandBatchRequest`
- `CommandBatch`
- `CommandExecutionResult`

### Cameras

- `CameraFrame`

### Manual control

- `ManualControlCommand`
- `ManualControlRequest`
- `ManualControlBatch`

### External references

- `ExternalReferenceCommand`
- `ExternalReferenceRequest`
- `ExternalReferenceBatch`

## Generated files

Generated files should be placed under a `Generated/` folder and should not be edited manually.

Typical Unity generated files:

```text
Assets/MIMISK/Scripts/Bridge/Grpc/Generated/MimiskBridge.cs
Assets/MIMISK/Scripts/Bridge/Grpc/Generated/MimiskBridgeGrpc.cs
```

Typical Python generated files:

```text
ros2_ws/src/mimisk_grpc_adapter/mimisk_grpc_adapter/protobuf/mimisk_bridge_pb2.py
ros2_ws/src/mimisk_grpc_adapter/mimisk_grpc_adapter/protobuf/mimisk_bridge_pb2_grpc.py
```

## Regeneration

```bash
./scripts/build_mimisk_ros2_bridge.sh
./scripts/generate_mimisk_grpc_csharp.sh
```

## Design note

The gRPC API is intentionally MIMISK-specific rather than a generic ROS/protobuf translator. This keeps the bridge stable, explicit, and easy to document for simulator users.
