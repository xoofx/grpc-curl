using System.Diagnostics.CodeAnalysis;
using Google.Protobuf.Reflection;
using Grpc.Core;

namespace DynamicGrpc;

/// <summary>
/// Main client interface for dynamically calling a gRPC service.
/// </summary>
public sealed class DynamicGrpcClient : ClientBase
{
    private readonly DynamicFileDescriptorSet _dynamicDescriptorSet;
    private readonly DynamicGrpcClientOptions _options;

    private DynamicGrpcClient(CallInvoker callInvoker, DynamicFileDescriptorSet dynamicDescriptorSet, DynamicGrpcClientOptions options) : base(callInvoker)
    {
        _dynamicDescriptorSet = dynamicDescriptorSet;
        _options = options;
    }

    /// <summary>
    /// List of <see cref="FileDescriptor"/> used by this instance.
    /// </summary>
    public IReadOnlyList<FileDescriptor> Files => _dynamicDescriptorSet.Files;

    /// <summary>
    /// Tries to find the <see cref="MethodDescriptor"/> associated to the specified service and method name.
    /// </summary>
    /// <param name="serviceName">The service.</param>
    /// <param name="methodName">The method.</param>
    /// <param name="methodDescriptor">The method descriptor or null if none found.</param>
    /// <returns><c>true</c> if the method was found; <c>false</c> otherwise.</returns>
    public bool TryFindMethod(string serviceName, string methodName, [NotNullWhen(true)]  out MethodDescriptor? methodDescriptor)
    {
        return _dynamicDescriptorSet.TryFindMethodDescriptorProto(serviceName, methodName, out methodDescriptor);
    }

    /// <summary>
    /// Creates a client by using the specified <see cref="FileDescriptor"/>. The descriptors must appear in reverse dependency order (if A depends on B, B should comes first).
    /// </summary>
    /// <param name="channel">The gRPC channel to fetch reflection data from.</param>
    /// <param name="descriptors">The file descriptors./></param>
    /// <param name="options">Options for this client.</param>
    /// <returns>A dynamic client gRPC instance.</returns>
    public static DynamicGrpcClient FromDescriptors(ChannelBase channel, FileDescriptor[] descriptors, DynamicGrpcClientOptions? options = null)
    {
        return FromDescriptors(channel.CreateCallInvoker(), descriptors, options);
    }

    /// <summary>
    /// Creates a client by using the specified <see cref="FileDescriptor"/>. The descriptors must appear in reverse dependency order (if A depends on B, B should comes first).
    /// </summary>
    /// <param name="callInvoker">The gRPC CallInvoker to fetch reflection data from.</param>
    /// <param name="descriptors">The file descriptors./></param>
    /// <param name="options">Options for this client.</param>
    /// <returns>A dynamic client gRPC instance.</returns>
    public static DynamicGrpcClient FromDescriptors(CallInvoker callInvoker, FileDescriptor[] descriptors, DynamicGrpcClientOptions? options = null)
    {
        options ??= new DynamicGrpcClientOptions();
        return new DynamicGrpcClient(callInvoker, DynamicFileDescriptorSet.FromFileDescriptors(descriptors), options);
    }

    /// <summary>
    /// Creates a client by using the specified <see cref="FileDescriptorProto"/>. The descriptors must appear in reverse dependency order (if A depends on B, B should comes first).
    /// </summary>
    /// <param name="channel">The gRPC channel to fetch reflection data from.</param>
    /// <param name="descriptorProtos">The file proto descriptors./></param>
    /// <param name="options">Options for this client.</param>
    /// <returns>A dynamic client gRPC instance.</returns>
    public static DynamicGrpcClient FromDescriptorProtos(ChannelBase channel, FileDescriptorProto[] descriptorProtos, DynamicGrpcClientOptions? options = null)
    {
        return FromDescriptorProtos(channel.CreateCallInvoker(), descriptorProtos, options);
    }

    /// <summary>
    /// Creates a client by using the specified <see cref="FileDescriptorProto"/>. The descriptors must appear in reverse dependency order (if A depends on B, B should comes first).
    /// </summary>
    /// <param name="callInvoker">The gRPC CallInvoker to fetch reflection data from.</param>
    /// <param name="descriptorProtos">The file proto descriptors./></param>
    /// <param name="options">Options for this client.</param>
    /// <returns>A dynamic client gRPC instance.</returns>
    public static DynamicGrpcClient FromDescriptorProtos(CallInvoker callInvoker, FileDescriptorProto[] descriptorProtos, DynamicGrpcClientOptions? options = null)
    {
        options ??= new DynamicGrpcClientOptions();
        return new DynamicGrpcClient(callInvoker, DynamicFileDescriptorSet.FromFileDescriptorProtos(descriptorProtos), options);
    }

    /// <summary>
    /// Creates a client by fetching reflection data from the server. Might trigger an exception if the server doesn't support exception.
    /// </summary>
    /// <param name="channel">The gRPC channel to fetch reflection data from.</param>
    /// <param name="options">Options for this client.</param>
    /// <param name="timeoutInMillis">Timeout in milliseconds. Default is 10000ms (10 seconds).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A dynamic client gRPC instance.</returns>
    public static Task<DynamicGrpcClient> FromServerReflection(ChannelBase channel, DynamicGrpcClientOptions? options = null, int timeoutInMillis = 10000, CancellationToken cancellationToken = default)
    {
        return FromServerReflection(channel.CreateCallInvoker(), options, timeoutInMillis, cancellationToken);
    }

    /// <summary>
    /// Creates a client by fetching reflection data from the server. Might trigger an exception if the server doesn't support exception.
    /// </summary>
    /// <param name="callInvoker">The gRPC CallInvoker to fetch reflection data from.</param>
    /// <param name="options">Options for this client.</param>
    /// <param name="timeoutInMillis">Timeout in milliseconds. Default is 10000ms (10 seconds).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A dynamic client gRPC instance.</returns>
    public static async Task<DynamicGrpcClient> FromServerReflection(CallInvoker callInvoker, DynamicGrpcClientOptions? options = null, int timeoutInMillis = 10000, CancellationToken cancellationToken = default)
    {
        options ??= new DynamicGrpcClientOptions();
        var dynamicDescriptorSet = await DynamicFileDescriptorSet.FromServerReflection(callInvoker, timeoutInMillis, cancellationToken);
        return new DynamicGrpcClient(callInvoker, dynamicDescriptorSet, options);
    }

    /// <summary>Invokes a simple remote call in a blocking fashion.</summary>
    public IDictionary<string, object> BlockingUnaryCall(string serviceName, string methodName, IDictionary<string, object> request, string? host = null, CallOptions? options = null)
    {
        return CallInvoker.BlockingUnaryCall(GetMethod(serviceName, methodName), host, options ?? new CallOptions(), request);
    }

    /// <summary>
    /// Invoke the method asynchronously and adapt dynamically whether the method is a unary call, a client streaming, server streaming or full duplex.
    /// </summary>
    /// <param name="serviceName">The name of the service to access.</param>
    /// <param name="methodName">The name of the method in the service.</param>
    /// <param name="input">The input.</param>
    /// <returns>The result of the call.</returns>
    /// <exception cref="InvalidOperationException">If the service/method was not found</exception>
    public async IAsyncEnumerable<IDictionary<string, object>> AsyncDynamicCall(string serviceName, string methodName, IAsyncEnumerable<IDictionary<string, object>> input, string? host = null, CallOptions? options = null)
    {
        if (!TryFindMethod(serviceName, methodName, out var methodDescriptor))
        {
            throw new InvalidOperationException($"Unable to find the method `{serviceName}/{methodName}`");
        }

        if (methodDescriptor.IsClientStreaming)
        {
            if (methodDescriptor.IsServerStreaming)
            {
                // Full streaming duplex
                var call = AsyncDuplexStreamingCall(serviceName, methodName, host, options);
                await foreach (var item in input)
                {
                    await call.RequestStream.WriteAsync(item);
                }
                await call.RequestStream.CompleteAsync();

                var responseStream = call.ResponseStream;
                while (await responseStream.MoveNext())
                {
                    yield return responseStream.Current;
                }
            }
            else
            {
                // Client streaming only
                var call = AsyncClientStreamingCall(serviceName, methodName, host, options);
                await foreach (var item in input)
                {
                    await call.RequestStream.WriteAsync(item);
                }
                await call.RequestStream.CompleteAsync();
                var result = await call.ResponseAsync;
                yield return result;
            }

        }
        else if (methodDescriptor.IsServerStreaming)
        {
            // Server streaming only
            IDictionary<string, object>? firstInput = null;
            await foreach (var item in input)
            {
                firstInput = item;
                break; // Take only the first element
            }

            var call = AsyncServerStreamingCall(serviceName, methodName, firstInput ?? new Dictionary<string, object>(), host, options);
            var responseStream = call.ResponseStream;
            while (await responseStream.MoveNext())
            {
                yield return responseStream.Current;
            }
        }
        else
        {
            // Standard call
            IDictionary<string, object>? firstInput = null;
            await foreach (var item in input)
            {
                firstInput = item;
                break; // Take only the first element
            }

            var result = await AsyncUnaryCall(serviceName, methodName, firstInput ?? new Dictionary<string, object>(), host, options);
            yield return result;
        }
    }

    /// <summary>Invokes a simple remote call asynchronously.</summary>
    /// <param name="serviceName">The name of the service to access.</param>
    /// <param name="methodName">The name of the method in the service.</param>
    /// <param name="request">The input.</param>
    /// <param name="host">Override for the host.</param>
    /// <param name="options">Optional options for this call.</param>
    /// <returns>The result of the call.</returns>
    public AsyncUnaryCall<IDictionary<string, object>> AsyncUnaryCall(string serviceName, string methodName, IDictionary<string, object> request, string? host = null, CallOptions? options = null)
    {
        return CallInvoker.AsyncUnaryCall(GetMethod(serviceName, methodName), host, options ?? new CallOptions(), request);
    }

    /// <summary>
    /// Invokes a server streaming call asynchronously.
    /// In server streaming scenario, client sends on request and server responds with a stream of responses.
    /// </summary>
    /// <param name="serviceName">The name of the service to access.</param>
    /// <param name="methodName">The name of the method in the service.</param>
    /// <param name="request">The input.</param>
    /// <param name="host">Override for the host.</param>
    /// <param name="options">Optional options for this call.</param>
    /// <returns>A call object to interact with streaming request/response streams.</returns>
    public Grpc.Core.AsyncServerStreamingCall<IDictionary<string, object>> AsyncServerStreamingCall(
        string serviceName, string methodName,
        IDictionary<string, object> request, string? host = null, CallOptions? options = null)
    {
        return CallInvoker.AsyncServerStreamingCall(GetMethod(serviceName, methodName), host, options ?? new CallOptions(), request);
    }

    ///// <summary>
    ///// Invokes a client streaming call asynchronously.
    ///// In client streaming scenario, client sends a stream of requests and server responds with a single response.
    ///// </summary>
    /// <param name="serviceName">The name of the service to access.</param>
    /// <param name="methodName">The name of the method in the service.</param>
    /// <param name="host">Override for the host.</param>
    /// <param name="options">Optional options for this call.</param>
    /// <returns>A call object to interact with streaming request/response streams.</returns>
    public Grpc.Core.AsyncClientStreamingCall<IDictionary<string, object>, IDictionary<string, object>> AsyncClientStreamingCall(
        string serviceName, string methodName,
        string? host = null,
        CallOptions? options = null)
    {
        return CallInvoker.AsyncClientStreamingCall(GetMethod(serviceName, methodName), host, options ?? new CallOptions());
    }

    /// <summary>
    /// Invokes a duplex streaming call asynchronously.
    /// In duplex streaming scenario, client sends a stream of requests and server responds with a stream of responses.
    /// The response stream is completely independent and both side can be sending messages at the same time.
    /// </summary>
    /// <param name="serviceName">The name of the service to access.</param>
    /// <param name="methodName">The name of the method in the service.</param>
    /// <param name="host">Override for the host.</param>
    /// <param name="options">Optional options for this call.</param>
    /// <returns>A call object to interact with streaming request/response streams.</returns>
    public Grpc.Core.AsyncDuplexStreamingCall<IDictionary<string, object>, IDictionary<string, object>> AsyncDuplexStreamingCall(
        string serviceName, string methodName,
        string? host = null,
        CallOptions? options = null)
    {
        return CallInvoker.AsyncDuplexStreamingCall(GetMethod(serviceName, methodName), host, options ?? new CallOptions());
    }

    private Method<IDictionary<string, object>, IDictionary<string, object>> GetMethod(string serviceName, string methodName)
    {
        var (marshalIn, marshalOut) = _dynamicDescriptorSet.GetMarshaller(serviceName, methodName, new DynamicGrpcClientContext(_options));
        return new Method<IDictionary<string, object>, IDictionary<string, object>>(MethodType.Unary, serviceName, methodName, marshalIn, marshalOut);
    }
}