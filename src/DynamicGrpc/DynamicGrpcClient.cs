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

    private DynamicGrpcClient(ChannelBase channel, DynamicFileDescriptorSet dynamicDescriptorSet, DynamicGrpcClientOptions options) : base(channel)
    {
        _dynamicDescriptorSet = dynamicDescriptorSet;
        _options = options;
    }

    /// <summary>
    /// List of <see cref="FileDescriptor"/> used by this instance.
    /// </summary>
    public IReadOnlyList<FileDescriptor> Files => _dynamicDescriptorSet.Files;

    /// <summary>
    /// Creates a client by using the specified <see cref="FileDescriptor"/>. The descriptors must appear in reverse dependency order (if A depends on B, B should comes first).
    /// </summary>
    /// <param name="channel">The gRPC channel to fetch reflection data from.</param>
    /// <param name="descriptors">The file descriptors./></param>
    /// <param name="options">Options for this client.</param>
    /// <returns>A dynamic client gRPC instance.</returns>
    public static DynamicGrpcClient FromDescriptors(ChannelBase channel, FileDescriptor[] descriptors, DynamicGrpcClientOptions? options = null)
    {
        options ??= new DynamicGrpcClientOptions();
        return new DynamicGrpcClient(channel, DynamicFileDescriptorSet.FromFileDescriptors(descriptors), options);
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
        options ??= new DynamicGrpcClientOptions();
        return new DynamicGrpcClient(channel, DynamicFileDescriptorSet.FromFileDescriptorProtos(descriptorProtos), options);
    }

    /// <summary>
    /// Creates a client by fetching reflection data from the server. Might trigger an exception if the server doesn't support exception.
    /// </summary>
    /// <param name="channel">The gRPC channel to fetch reflection data from.</param>
    /// <param name="options">Options for this client.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A dynamic client gRPC instance.</returns>
    public static async Task<DynamicGrpcClient> FromServerReflection(ChannelBase channel, DynamicGrpcClientOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new DynamicGrpcClientOptions();
        var dynamicDescriptorSet = await DynamicFileDescriptorSet.FromServerReflection(channel, cancellationToken);
        return new DynamicGrpcClient(channel, dynamicDescriptorSet, options);
    }

    /// <summary>Invokes a simple remote call in a blocking fashion.</summary>
    public IDictionary<string, object> BlockingUnaryCall(string serviceName, string methodName, IDictionary<string, object> request, string? host = null, CallOptions? options = null)
    {
        return CallInvoker.BlockingUnaryCall(GetMethod(serviceName, methodName), host, options ?? new CallOptions(), request);
    }

    /// <summary>Invokes a simple remote call asynchronously.</summary>
    public AsyncUnaryCall<IDictionary<string, object>> AsyncUnaryCall(string serviceName, string methodName, IDictionary<string, object> request, string? host = null, CallOptions? options = null)
    {
        return CallInvoker.AsyncUnaryCall(GetMethod(serviceName, methodName), host, options ?? new CallOptions(), request);
    }

    /// <summary>
    /// Invokes a server streaming call asynchronously.
    /// In server streaming scenario, client sends on request and server responds with a stream of responses.
    /// </summary>
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