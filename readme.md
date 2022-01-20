# grpc-curl [![Build Status](https://github.com/xoofx/grpc-curl/workflows/ci/badge.svg?branch=main)](https://github.com/xoofx/grpc-curl/actions) [![Coverage Status](https://coveralls.io/repos/github/xoofx/grpc-curl/badge.svg?branch=main)](https://coveralls.io/github/xoofx/grpc-curl?branch=master) [![NuGet](https://img.shields.io/nuget/v/grpc-curl.svg)](https://www.nuget.org/packages/grpc-curl/)

<img align="right" width="160px" height="160px" src="https://raw.githubusercontent.com/xoofx/grpc-curl/main/img/grpc-curl.png">

`grpc-curl` is a command line tool for interacting with gRPC servers. 

This tool is the .NET equivalent of the popular [gRPCurl](https://github.com/fullstorydev/grpcurl) written in Golang.

> NOTE: `grpc-curl` doesn't not support yet all the features that `gRPCurl` is providing.

## Usage

`grpc-curl` currently requires that the gRPC server has activated gRPC reflection.

```powershell
./grpc-curl --json -d "{""getStatus"":{}}" 192.168.100.1:9200 SpaceX.API.Device.Device/Handle
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
## Usage API

The functionality of `grpc-curl` is also accessible through the NuGet package [DynamicGrpc](https://www.nuget.org/packages/DynamicGrpc/).

```c#
var channel = GrpcChannel.ForAddress("http://192.168.100.1:9200");
// Fetch reflection data from server
var client = await DynamicGrpcClient.FromServerReflection(channel);

// Call the method `Handle` on the service `SpaceX.API.Device.Device`
var result = await client.AsyncUnaryCall("SpaceX.API.Device.Device", "Handle", new Dictionary<string, object>()
{
    { "get_status", new Dictionary<string, object>() }
});
```

## Features

- Build on top of the `DynamicGrpc` library available as a separate NuGet package.
- `DynamicGrpc` supports all the kind of gRPC calls (unary blocking, unary async, streaming, full-duplex...)
- `grpc-curl` supports only blocking/async method for now (streaming should follow)
- Support for plain Protocol Buffers naming conventions or JSON.

## Binaries

If you have dotnet 6.0 installed, you can install this tool via NuGet:

```
dotnet tool install --global grpc-curl --version 1.0.0
```

> Debian and macOS packages for x64/am64 will be released later.

## License

This software is released under the [BSD-Clause 2 license](https://opensource.org/licenses/BSD-2-Clause). 

## Author

Alexandre Mutel aka [xoofx](http://xoofx.com).