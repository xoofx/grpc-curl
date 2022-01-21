//using System.Dynamic;

namespace DynamicGrpc;

/// <summary>
/// Used to serialize/deserialize back the Any type.
/// </summary>
public static class DynamicAnyExtensions
{
    // https://github.com/protocolbuffers/protobuf/blob/41e22cde8d8a44c35127a26c19e08b180e0b30a4/src/google/protobuf/any.proto#L97-L124
    internal const string GoogleTypeAnyFullName = "google.protobuf.Any";
    internal const string GoogleTypeUrlKey = "type_url";
    internal const string GoogleValueKey = "value";

    public const string TypeKey = "@type";

    /// <summary>
    /// Adds the property @type to serialize a dictionary as any type
    /// </summary>
    /// <typeparam name="TAny">Type of the dictionary.</typeparam>
    /// <param name="any">The any dictionary.</param>
    /// <param name="typeName">The type associated to this dictionary.</param>
    /// <returns>The input any dictionary with the proper @type information.</returns>
    public static TAny WithAny<TAny>(this TAny any, string typeName) where TAny : IDictionary<string, object>
    {
        any[TypeKey] = $"type.googleapis.com/{typeName}";
        return any;
    }
}