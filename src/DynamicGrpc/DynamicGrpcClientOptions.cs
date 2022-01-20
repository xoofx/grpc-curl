using System.Dynamic;

namespace DynamicGrpc;

/// <summary>
/// Options to use with <see cref="DynamicGrpcClient"/>
/// </summary>
public sealed class DynamicGrpcClientOptions
{
    /// <summary>
    /// Creates a new instance of this class.
    /// </summary>
    public DynamicGrpcClientOptions()
    {
        MessageFactory = () => new ExpandoObject()!;
    }

    /// <summary>
    /// Gets or sets a boolean indicating whether to serialize/deserialize using JSON names. Default is <c>false</c>.
    /// </summary>
    public bool UseJsonNaming { get; set; }

    /// <summary>
    /// Gets or sets a boolean indicating whether to serialize/deserialize enum with numbers instead of strings. Default is <c>false</c>.
    /// </summary>
    public bool UseNumberedEnums { get; set; }

    /// <summary>
    /// Gets or sets the factory to instance deserialized messages. By default, creates a <see cref="ExpandoObject"/>.
    /// </summary>
    public Func<IDictionary<string, object>> MessageFactory { get; set; }

    /// <summary>
    /// Clones this instance.
    /// </summary>
    /// <returns>A clone of this instance.</returns>
    public DynamicGrpcClientOptions Clone()
    {
        return (DynamicGrpcClientOptions)this.MemberwiseClone();
    }
}