using Google.Protobuf;

namespace DynamicGrpc;

/// <summary>
/// Internal class used for passing options around and keep read tags.
/// This class should reflect <see cref="DynamicGrpcClientOptions"/>.
/// </summary>
internal sealed class DynamicGrpcClientContext
{
    private readonly Queue<uint> _nextTags;

    public DynamicGrpcClientContext(DynamicGrpcClientOptions options)
    {
        UseJsonNaming = options.UseJsonNaming;
        UseNumberedEnums = options.UseNumberedEnums;
        Factory = options.MessageFactory;
        _nextTags = new Queue<uint>();
    }

    public bool UseJsonNaming { get; set; }

    public bool UseNumberedEnums { get; set; }

    public Func<IDictionary<string, object>> Factory { get; set; }

    internal uint ReadTag(ref ParseContext input)
    {
        return _nextTags.Count > 0 ? _nextTags.Dequeue() : input.ReadTag();
    }
    
    internal uint SkipTag(ref ParseContext input)
    {
        return _nextTags.Dequeue();
    }

    internal uint PeekTak(ref ParseContext input)
    {
        if (_nextTags.Count > 0) return _nextTags.Peek();
        var tag = input.ReadTag();
        _nextTags.Enqueue(tag);
        return tag;
    }

    internal void EnqueueTag(uint tag)
    {
        _nextTags.Enqueue(tag);
    }
}