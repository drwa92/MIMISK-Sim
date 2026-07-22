# Unity Bridge Public Release Package

This document defines the recommended public Unity bridge package. It is separate from the full Unity simulator release.

## Purpose

The Unity bridge package lets users connect a MIMISK Unity scene to the public ROS 2 / gRPC interface without exposing all Unity assets, models, or internal prototype scenes.

## Recommended Unity bridge scripts

```text
Assets/MIMISK/Scripts/Bridge/Grpc/MIMISKGrpcConnection.cs
Assets/MIMISK/Scripts/Bridge/Grpc/MIMISKGrpcTelemetryBridge.cs
Assets/MIMISK/Scripts/Bridge/Grpc/MIMISKGrpcCommandBridge.cs
Assets/MIMISK/Scripts/Bridge/Grpc/MIMISKGrpcCameraBridge.cs
Assets/MIMISK/Scripts/Bridge/Grpc/MIMISKGrpcManualControlBridge.cs
Assets/MIMISK/Scripts/Bridge/Grpc/MIMISKGrpcExternalReferenceBridge.cs
Assets/MIMISK/Scripts/Bridge/Grpc/Generated/MimiskBridge.cs
Assets/MIMISK/Scripts/Bridge/Grpc/Generated/MimiskBridgeGrpc.cs
```

## Unity plugin/runtime dependencies

The bridge requires:

```text
Google.Protobuf
Grpc.Core
Grpc.Core.Api
System.Diagnostics.DiagnosticSource
System.Runtime.CompilerServices.Unsafe
```

For public release, either:

1. include the plugin DLLs if licensing permits, or
2. document how to obtain them using the same Grpc.Tools / NuGet workflow used during development.

## Recommended scene object

Create an empty GameObject:

```text
MIMISK_ROS2_GRPC_Bridge
```

Add components:

```text
MIMISK Grpc Connection
MIMISK Grpc Telemetry Bridge
MIMISK Grpc Command Bridge
MIMISK Grpc Camera Bridge
MIMISK Grpc Manual Control Bridge
MIMISK Grpc External Reference Bridge
```

Recommended connection settings:

```text
Server IP = localhost
Server Port = 30052
Connect On Start = true
Reconnect If Failed = true
```


