# grpc-curl [![Build Status](https://github.com/xoofx/grpc-curl/workflows/ci/badge.svg?branch=main)](https://github.com/xoofx/grpc-curl/actions) [![Coverage Status](https://coveralls.io/repos/github/xoofx/grpc-curl/badge.svg?branch=main)](https://coveralls.io/github/xoofx/grpc-curl?branch=master) [![NuGet](https://img.shields.io/nuget/v/grpc-curl.svg)](https://www.nuget.org/packages/grpc-curl/)

<img align="right" width="160px" height="160px" src="https://raw.githubusercontent.com/xoofx/grpc-curl/main/img/grpc-curl.png">

`grpc-curl` is a command line tool for interacting with gRPC servers. 

All the functionalities of `grpc-curl` are also accessible through the NuGet package [DynamicGrpc](https://www.nuget.org/packages/DynamicGrpc/) that is part of this repository.

This tool is the .NET equivalent of the popular [gRPCurl](https://github.com/fullstorydev/grpcurl) written in Golang.

> NOTE: `grpc-curl` doesn't not support yet all the features that `gRPCurl` is providing.
## Features

- Allows to **invoke method services** for all gRPC calling modes (unary, client streaming, server streaming, full-duplex).
- Allows to **print proto reflection descriptors** back to **proto language** (via `--describe` with `grpc-curl`, or via the API `.ToProtoString()` with `DynamicGrpc`)
- Supports for plain Protocol Buffers naming conventions and JSON.
- Supports for `google.protobuf.Any`: The type has to be encoded - and is decoded with the shadow property `@type` on a dictionary (e.g `@type = "type.googleapis.com/YourTypeName"`).
- Build on top of the `DynamicGrpc` library available as a separate [NuGet package](https://www.nuget.org/packages/DynamicGrpc/).
- Build for `net6.0+`
## Usage

`grpc-curl` currently requires that the gRPC server has activated gRPC reflection.

```
Copyright (C) 2022 Alexandre Mutel. All Rights Reserved
grpc-curl - Version: 1.3.0

Usage: grpc-curl [options] address service/method

  address: A http/https URL or a simple host:address.
           If only host:address is used, HTTPS is used by default
           unless the options --http is passed.

## Options

  -d, --data=VALUE           Data for string content.
      --http                 Use HTTP instead of HTTPS unless the protocol is
                               specified directly on the address.
      --json                 Use JSON naming for input and output.
      --describe             Describe the service or dump all services
                               available.
  -v, --verbosity[=VALUE]    Set verbosity.
  -h, --help                 Show this help.
```

### Query a service

```powershell
./grpc-curl --json -d "{""getStatus"":{}}" http://192.168.100.1:9200 SpaceX.API.Device.Device/Handle
```
Will print the following result:

```json
{
  "apiVersion": 4,
  "dishGetStatus": {
    "deviceInfo": {
      "id": "0000000000-00000000-00000000",
      "hardwareVersion": "rev2_proto3",
      "softwareVersion": "992cafb5-61c7-46a3-9ef7-5907c8cf90fd.uterm.release",
      "countryCode": "FR",
      "utcOffsetS": 1
    },
    "deviceState": {
      "uptimeS": 667397
    },
    "obstructionStats": {
      "fractionObstructed": 2.2786187E-06,
      "wedgeFractionObstructed": [
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0
      ],
      "wedgeAbsFractionObstructed": [
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0
      ],
      "validS": 667070.0,
      "avgProlongedObstructionIntervalS": "NaN"
    },
    "alerts": {
      "roaming": true
    },
    "downlinkThroughputBps": 461012.72,
    "uplinkThroughputBps": 294406.6,
    "popPingLatencyMs": 30.35,
    "boresightAzimuthDeg": 0.7464048,
    "boresightElevationDeg": 65.841354,
    "gpsStats": {
      "gpsValid": true,
      "gpsSats": 12
    }
  }
}
```

### Describe a service

```powershell
./grpc-curl --describe http://192.168.100.1:9200 SpaceX.API.Device.Device
```
Will print:

```proto
// SpaceX.API.Device.Device is a service:
service Device {
  rpc Stream ( .SpaceX.API.Device.ToDevice ) returns ( .SpaceX.API.Device.FromDevice );
  rpc Handle ( .SpaceX.API.Device.Request ) returns ( .SpaceX.API.Device.Response );
}
```

### Describe all proto files serviced via reflection

```powershell
./grpc-curl --describe http://192.168.100.1:9200
```
Will print:

```proto
// spacex/api/common/status/status.proto is a proto file.
syntax = "proto3";

package SpaceX.API.Status;

// SpaceX.API.Status.Status is a message:
message Status {
  int32 code = 1;
  string message = 2;
}


// spacex/api/device/command.proto is a proto file.
syntax = "proto3";

package SpaceX.API.Device;

// SpaceX.API.Device.PublicKey is a message:
message PublicKey {
  string key = 1;
  repeated Capability capabilities = 2;
}

// ....... and more prints ........
```

## Usage API

All the functionalities of `grpc-curl` are also accessible through the NuGet package [DynamicGrpc](https://www.nuget.org/packages/DynamicGrpc/).

```c#
var channel = GrpcChannel.ForAddress("http://192.168.100.1:9200");
// Fetch reflection data from server
var client = await DynamicGrpcClient.FromServerReflection(channel);

// Call the method `Handle` on the service `SpaceX.API.Device.Device`
var result = await client.AsyncUnaryCall("SpaceX.API.Device.Device", "Handle", new Dictionary<string, object>()
{
    { "get_status", new Dictionary<string, object>() }
});

// Print a proto descriptor
FileDescriptor descriptor = client.Files[0];
Console.WriteLine(descriptor.ToProtoString());
```
## Binaries

If you have dotnet 6.0 installed, you can install this tool via NuGet:

```
dotnet tool install --global grpc-curl --version 1.2.0
```

> Debian and macOS packages for x64/am64 will be released later.

## License

This software is released under the [BSD-Clause 2 license](https://opensource.org/licenses/BSD-2-Clause). 

## Author

Alexandre Mutel aka [xoofx](http://xoofx.com).