using System.Diagnostics.CodeAnalysis;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Grpc.Reflection.V1Alpha;

namespace DynamicGrpc;

/// <summary>
/// Internal class used to manage all serializers for a set of <see cref="FileDescriptor"/>.
/// </summary>
internal sealed class DynamicFileDescriptorSet
{
    private readonly FileDescriptor[] _descriptorSet;
    private readonly Dictionary<string, DynamicServiceDescriptor> _services;
    private readonly Dictionary<string, DynamicMessageSerializer> _messageTypes;

    public DynamicFileDescriptorSet(FileDescriptor[] descriptorSet)
    {
        _descriptorSet = descriptorSet;
        _services = new Dictionary<string, DynamicServiceDescriptor>();
        _messageTypes = new Dictionary<string, DynamicMessageSerializer>();
        Initialize();
    }

    public FileDescriptor[] Files => _descriptorSet;

    private void Initialize()
    {
        foreach (var file in _descriptorSet)
        {
            foreach (var service in file.Services)
            {
                var dynamicService = new DynamicServiceDescriptor(service);
                var key = $"{file.Package}.{service.Name}";
                if (!_services.ContainsKey(key))
                {
                    _services.Add(key, dynamicService);
                }
            }

            foreach (var message in file.MessageTypes)
            {
                ProcessMessageType(message, file.Package);
            }

            // TODO: Anything from file.Options?
        }
    }

    private void ProcessMessageType(MessageDescriptor messageDescriptor, string parentKey)
    {
        var keyType = $"{parentKey}.{messageDescriptor.Name}";
        if (!_messageTypes.ContainsKey(keyType))
        {
            _messageTypes.Add(keyType, new DynamicMessageSerializer(this, messageDescriptor));
        }

        foreach (var messageDescriptorNestedType in messageDescriptor.NestedTypes)
        {
            ProcessMessageType(messageDescriptorNestedType, keyType);
        }
    }

    public (Marshaller<IDictionary<string, object>> request, Marshaller<IDictionary<string, object>> response) GetMarshaller(string serviceName, string methodName, DynamicGrpcClientContext context)
    {
        if (!TryFindMethodDescriptorProto(serviceName, methodName, out var methodProto))
        {
            throw new InvalidOperationException($"The service/method `{serviceName}/{methodName}` was not found.");
        }

        if (!TryFindMessageDescriptorProto(methodProto.InputType.FullName, out var inputMessageProto))
        {
            throw new InvalidOperationException($"The input message type`{methodProto.InputType}` for the service/method {serviceName}/{methodName} was not found.");
        }

        if (!TryFindMessageDescriptorProto(methodProto.OutputType.FullName, out var outputMessageProto))
        {
            throw new InvalidOperationException($"The output message type `{methodProto.InputType}` for the service/method {serviceName}/{methodName} was not found.");
        }

        return (inputMessageProto.GetMarshaller(context), outputMessageProto.GetMarshaller(context));
    }

    public static async Task<DynamicFileDescriptorSet> FromServerReflection(ChannelBase channel)
    {
        // Step 1 - Fetch all services we can interact with
        var client = new ServerReflection.ServerReflectionClient(channel);
        var response = await SingleRequestAsync(client, new ServerReflectionRequest
        {
            ListServices = ""
        });

        // Step 2 - Fetch all proto files associated with the service we got.
        // NOTE: The proto files are all transitive, but not correctly ordered!
        var protosLoaded = new Dictionary<string, (ByteString, FileDescriptorProto)>();
        var listOfProtosToLoad = new List<ByteString>();
        foreach (var service in response.ListServicesResponse.Service)
        {
            var serviceResponse = await SingleRequestAsync(client, new ServerReflectionRequest
            {
                FileContainingSymbol = service.Name
            });

            listOfProtosToLoad.AddRange(serviceResponse.FileDescriptorResponse.FileDescriptorProto.ToList());
        }

        // Workaround for https://github.com/protocolbuffers/protobuf/issues/9431
        // Step 3 - Order proto files correctly because of 2 problems:
        // 1) as FileContainingSymbol doesn't seem to return proto files in the correct order
        // 2) FileDescriptor.BuildFromByteStrings doesn't support passing files in random order, so we need to reorder them with protos
        // It is very unfortunate, as we are doubling the deserialization of FileDescriptorProto
        var resolved = new HashSet<string>();
        var orderedList = new List<ByteString>();

        foreach (var buffer in listOfProtosToLoad)
        {
            var proto = FileDescriptorProto.Parser.ParseFrom(buffer.ToByteArray());
            protosLoaded.Add(proto.Name, (buffer, proto));
        }

        while (protosLoaded.Count > 0)
        {
            var (buffer, nextProto) = protosLoaded.Values.FirstOrDefault(x => x.Item2.Dependency.All(dep => resolved.Contains(dep)));
            if (nextProto == null)
            {
                throw new InvalidOperationException($"Invalid proto dependencies. Unable to resolve remaining protos [{string.Join(",", protosLoaded.Values.Select(x => x.Item2.Name))}] that don't have all their dependencies available.");
            }

            resolved.Add(nextProto.Name);
            protosLoaded.Remove(nextProto.Name);
            orderedList.Add(buffer);
        }

        // Step 4 - Build FileDescriptor from properly ordered list
        var descriptors = FileDescriptor.BuildFromByteStrings(orderedList.ToList());
        return new DynamicFileDescriptorSet(descriptors.ToArray());
    }

    public static DynamicFileDescriptorSet FromFileDescriptorProtos(IEnumerable<FileDescriptorProto> protos)
    {
        // Workaround for https://github.com/protocolbuffers/protobuf/issues/9431
        // Step 1 - FileDescriptor.BuildFromByteStrings doesn't support passing files in random order, so we need to reorder them with protos
        // It is very unfortunate, as we are doubling the deserialization of FileDescriptorProto
        var resolved = new HashSet<string>();
        var orderedList = new List<ByteString>();
        var unorderedList = new List<FileDescriptorProto>(protos);

        while (unorderedList.Count > 0)
        {
            var proto = unorderedList.FirstOrDefault(x => x.Dependency.All(dep => resolved.Contains(dep)));
            if (proto == null)
            {
                throw new InvalidOperationException($"Invalid proto dependencies. Unable to resolve remaining protos [{string.Join(",", unorderedList.Select(x => x.Name))}] that don't have all their dependencies available.");
            }

            resolved.Add(proto.Name);
            unorderedList.Remove(proto);
            orderedList.Add(proto.ToByteString());
        }

        // Step 2 - Build FileDescriptor from properly ordered list
        var descriptors = FileDescriptor.BuildFromByteStrings(orderedList.ToList());
        return new DynamicFileDescriptorSet(descriptors.ToArray());
    }

    public static DynamicFileDescriptorSet FromFileDescriptors(IEnumerable<FileDescriptor> descriptors)
    {
        // Workaround for https://github.com/protocolbuffers/protobuf/issues/9431
        // Step 1 - FileDescriptor.BuildFromByteStrings doesn't support passing files in random order, so we need to reorder them with protos
        // It is very unfortunate, as we are doubling the deserialization of FileDescriptorProto
        var resolved = new HashSet<string>();
        var orderedList = new List<FileDescriptor>();
        var unorderedList = new List<FileDescriptor>(descriptors);

        while (unorderedList.Count > 0)
        {
            var descriptor = unorderedList.FirstOrDefault(x => x.Dependencies.All(dep => resolved.Contains(dep.Name)));
            if (descriptor == null)
            {
                throw new InvalidOperationException($"Invalid proto dependencies. Unable to resolve remaining protos [{string.Join(",", unorderedList.Select(x => x.Name))}] that don't have all their dependencies available.");
            }

            resolved.Add(descriptor.Name);
            unorderedList.Remove(descriptor);
            orderedList.Add(descriptor);
        }

        // Step 2 - Build FileDescriptor from properly ordered list
        return new DynamicFileDescriptorSet(orderedList.ToArray());
    }

    private static async Task<ServerReflectionResponse> SingleRequestAsync(ServerReflection.ServerReflectionClient client, ServerReflectionRequest request)
    {
        using var call = client.ServerReflectionInfo();
        await call.RequestStream.WriteAsync(request);
        var result = await call.ResponseStream.MoveNext();
        if (!result)
        {
            throw new InvalidOperationException();
        }

        var response = call.ResponseStream.Current;
        await call.RequestStream.CompleteAsync();
        return response;
    }

    public bool TryFindMethodDescriptorProto(string serviceName, string methodName, [NotNullWhen(true)] out MethodDescriptor? methodProto)
    {
        methodProto = null;
        return _services.TryGetValue($"{serviceName}", out var dynamicServiceDescriptor) && dynamicServiceDescriptor.TryGetValue(methodName, out methodProto);
    }


    public bool TryFindMessageDescriptorProto(string typeName, [NotNullWhen(true)] out DynamicMessageSerializer? messageDescriptorProto)
    {
        messageDescriptorProto = null;
        return _messageTypes.TryGetValue(typeName, out messageDescriptorProto);
    }
}