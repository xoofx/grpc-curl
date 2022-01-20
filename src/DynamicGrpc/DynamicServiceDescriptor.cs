using Google.Protobuf.Reflection;

namespace DynamicGrpc;

/// <summary>
/// Internal class used to map a method to a method descriptor.
/// </summary>
internal sealed class DynamicServiceDescriptor : Dictionary<string, MethodDescriptor>
{
    public DynamicServiceDescriptor(ServiceDescriptor proto)
    {
        Proto = proto;
        foreach (var method in proto.Methods)
        {
            this[method.Name] = method;
        }
    }

    public ServiceDescriptor Proto { get; }
}