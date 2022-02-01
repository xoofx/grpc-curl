# Changelog

## 1.3.5 (1 Feb 2022)
- Update to latest CommandLineUtils to fix issue with RequiredAttribute
- Bump to update formula with latest dotnet-releaser

## 1.3.4 (30 Jan 2022)
- Fix exception with RequiredAttribute constructor not found

## 1.3.3 (30 Jan 2022)
- Use CommandLineUtils to parse command line args
- Add source link for DynamicGrpc library

## 1.3.2 (29 Jan 2022)
- Use dotnet-releaser for releasing binaries

## 1.3.1 (27 Jan 2022)
- Fix message fields having packed attribute
- Fix warnings with trimming
- Prepare application for self-contained app and trimming

## 1.3.0 (22 Jan 2022)
- Add support for pretty printing all services and messages supported by a server with reflection (`--describe` with `grpc-curl`).
- Add support for pretty printing proto descriptor back to proto language (`ToProtoString()` API with `DynamicGrpc`)

## 1.2.0 (21 Jan 2022)
- Add support for all calling modes (unary, client streaming, server streaming and full-duplex)
- Add cancellation token when fetching reflection from server.
- Add support for default values.
- Allow to parse data from stdin
- Allow to force http if address:host is passed, use https by default

## 1.1.0 (21 Jan 2022)
- Add support for any

## 1.0.0 (20 Jan 2022)

- Initial version