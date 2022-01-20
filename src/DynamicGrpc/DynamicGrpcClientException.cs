namespace DynamicGrpc;

/// <summary>
/// Exception that can be thrown by <see cref="DynamicGrpcClient"/>.
/// </summary>
public sealed class DynamicGrpcClientException : Exception
{
    /// <summary>
    /// Creates a new instance of this class.
    /// </summary>
    /// <param name="message">The message of the exception.</param>
    public DynamicGrpcClientException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Creates a new instance of this class.
    /// </summary>
    /// <param name="message">The message of the exception.</param>
    /// <param name="innerException">The nested exception.</param>
    public DynamicGrpcClientException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}