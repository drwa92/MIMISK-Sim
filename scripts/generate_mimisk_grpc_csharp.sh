#!/usr/bin/env bash
set -e

MIMISK_ROOT="$HOME/mimisk_minirov_unity/mimisk_minirov_v2"
cd "$MIMISK_ROOT"

TOOLS_ROOT="$(find "$HOME/.nuget/packages/grpc.tools" -maxdepth 1 -mindepth 1 -type d 2>/dev/null | sort -V | tail -n 1)"

if [ -z "$TOOLS_ROOT" ]; then
    echo "[MIMISK] Grpc.Tools not found. Installing through local dotnet locator project."

    mkdir -p tools/grpc_tools_locator

    dotnet new classlib \
      -n GrpcToolsLocator \
      -o tools/grpc_tools_locator/GrpcToolsLocator \
      --force

    cd tools/grpc_tools_locator/GrpcToolsLocator
    dotnet add package Grpc.Tools
    dotnet restore

    cd "$MIMISK_ROOT"

    TOOLS_ROOT="$(find "$HOME/.nuget/packages/grpc.tools" -maxdepth 1 -mindepth 1 -type d | sort -V | tail -n 1)"
fi

PROTOC="$TOOLS_ROOT/tools/linux_x64/protoc"
PLUGIN="$TOOLS_ROOT/tools/linux_x64/grpc_csharp_plugin"

if [ ! -f "$PROTOC" ]; then
    echo "[MIMISK] ERROR: Linux protoc not found at: $PROTOC"
    echo "[MIMISK] Available tools:"
    find "$TOOLS_ROOT/tools" -maxdepth 2 -type f | sort
    exit 1
fi

if [ ! -f "$PLUGIN" ]; then
    echo "[MIMISK] ERROR: Linux grpc_csharp_plugin not found at: $PLUGIN"
    echo "[MIMISK] Available tools:"
    find "$TOOLS_ROOT/tools" -maxdepth 2 -type f | sort
    exit 1
fi

chmod +x "$PROTOC"
chmod +x "$PLUGIN"

echo "[MIMISK] Using Grpc.Tools root: $TOOLS_ROOT"
echo "[MIMISK] Using protoc: $PROTOC"
echo "[MIMISK] Using grpc_csharp_plugin: $PLUGIN"

mkdir -p Assets/MIMISK/Scripts/Bridge/Grpc/Generated

"$PROTOC" \
  -I proto \
  --csharp_out=Assets/MIMISK/Scripts/Bridge/Grpc/Generated \
  --grpc_out=Assets/MIMISK/Scripts/Bridge/Grpc/Generated \
  --plugin=protoc-gen-grpc="$PLUGIN" \
  proto/mimisk_bridge.proto

echo "[MIMISK] Generated Unity C# gRPC files:"
ls -lh Assets/MIMISK/Scripts/Bridge/Grpc/Generated

echo "[MIMISK] Checking generated namespace/client:"
grep -R "namespace MIMISK.Grpc\|class MIMISKBridgeClient" \
  Assets/MIMISK/Scripts/Bridge/Grpc/Generated || true
