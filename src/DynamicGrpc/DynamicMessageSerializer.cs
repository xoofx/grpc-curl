using System.Collections;
using System.Diagnostics;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.Reflection;
using Grpc.Core;

namespace DynamicGrpc;

/// <summary>
/// Internal class used to serialize/deserialize a message.
/// </summary>
internal sealed class DynamicMessageSerializer
{
    private readonly Dictionary<string, FieldDescriptor> _nameToField;
    private readonly Dictionary<string, FieldDescriptor> _jsonNameToField;
    private readonly Dictionary<uint, FieldDescriptor> _tagToField;

    internal DynamicMessageSerializer(DynamicFileDescriptorSet descriptorSet, MessageDescriptor messageDescriptor)
    {
        DescriptorSet = descriptorSet;
        _tagToField = new Dictionary<uint, FieldDescriptor>();
        _nameToField = new Dictionary<string, FieldDescriptor>();
        _jsonNameToField = new Dictionary<string, FieldDescriptor>();
        Descriptor = messageDescriptor;
        Initialize();
    }

    public DynamicFileDescriptorSet DescriptorSet { get; }

    public MessageDescriptor Descriptor { get; }

    private void Initialize()
    {
        var fields = Descriptor.Fields;

        foreach (var field in fields.InDeclarationOrder())
        {
            var tag = GetTagForField(field);
            _tagToField.Add(tag, field);
            _nameToField.Add(field.Name, field);
            _jsonNameToField.Add(field.JsonName, field);
        }
    }

    internal Marshaller<IDictionary<string, object>> GetMarshaller(DynamicGrpcClientContext context)
    {
        var parser = new MessageParser<DynamicMessage>(() =>
        {
            var value = context.Factory();
            return new DynamicMessage(this, value, context);
        });

        Func<DeserializationContext, IDictionary<string, object>> deserializer = (ctx) =>
        {
            var message = parser.ParseFrom(ctx.PayloadAsReadOnlySequence());
            return message.Value;
        };

        Action<IDictionary<string, object>, SerializationContext> serializer = (value, ctx) =>
        {
            var writer = ctx.GetBufferWriter();
            var message = new DynamicMessage(this, value, context);
            message.WriteTo(writer);
            ctx.Complete();
        };

        return new Marshaller<IDictionary<string, object>>(serializer, deserializer);
    }

    private static uint GetTagForField(FieldDescriptor field)
    {
        var isRepeatedPacked = IsRepeatedPacked(field);
        var wireType = GetWireType(field.FieldType, isRepeatedPacked);
        var tag = WireFormat.MakeTag(field.FieldNumber, wireType);
        return tag;
    }

    public IDictionary<string, object> ReadFrom(ref ParseContext input, DynamicGrpcClientContext context)
    {
        var result = context.Factory();

        while (true)
        {
            var tag = context.ReadTag(ref input);
            if (tag == 0) break;

            if (!_tagToField.ContainsKey(tag))
            {
                throw new DynamicGrpcClientException($"Invalid tag 0x{tag:x8} received when deserializing message `{Descriptor.FullName}`.");
            }

            var fieldDescriptor = _tagToField[tag];

            object value;

            if (fieldDescriptor.IsMap)
            {
                var map = new Dictionary<object, object>();

                var fieldMessageType = fieldDescriptor.MessageType;
                var keyField = fieldMessageType.Fields.InDeclarationOrder()[0];
                var keyTag = GetTagForField(keyField);
                var valueField = fieldMessageType.Fields.InDeclarationOrder()[1];
                var valueTag = GetTagForField(valueField);
                var keyDefaultValue = DefaultValueHelper.GetDefaultValue(keyField.FieldType);
                var valueDefaultValue = DefaultValueHelper.GetDefaultValue(valueField.FieldType);

                bool isFirst = true;
                while (true)
                {
                    if (!isFirst)
                    {
                        var nextTag = context.PeekTak(ref input);

                        // If the next tag is not the one that we expect, queue it to make it available for the next ReadTag
                        if (nextTag != tag)
                        {
                            break;
                        }

                        context.SkipTag(ref input);
                    }

                    // Not used
                    var length = input.ReadLength();

                    object? keyRead = keyDefaultValue;
                    object? valueRead = valueDefaultValue;

                    // Read key
                    var tagRead = context.PeekTak(ref input);
                    if (tagRead == keyTag)
                    {
                        context.SkipTag(ref input);
                        keyRead = ReadFieldValue(ref input, fieldMessageType, keyField, context);
                    }
                    else
                    {
                        // We didn't have a key, so key is default value, try next the value directly
                        goto readValue;
                    }

                    // Read value
                    tagRead = context.PeekTak(ref input);
                    readValue:
                    
                    if (tagRead == valueTag)
                    {
                        context.SkipTag(ref input);
                        valueRead = ReadFieldValue(ref input, fieldMessageType, valueField, context);
                    }

                    Debug.Assert(keyRead != null);
                    Debug.Assert(valueRead != null);
                    map[keyRead!] = valueRead!;
                    isFirst = false;
                }

                value = map;
            }
            else if (fieldDescriptor.IsRepeated)
            {
                switch (fieldDescriptor.FieldType)
                {
                    case FieldType.Double:
                    {
                        var repeated = new RepeatedField<double>();
                        repeated.AddEntriesFrom(ref input, FieldCodec.ForDouble(tag));
                        value = new List<double>(repeated);
                    }
                        break;
                    case FieldType.Float:
                    {
                        var repeated = new RepeatedField<float>();
                        repeated.AddEntriesFrom(ref input, FieldCodec.ForFloat(tag));
                        value = new List<float>(repeated);
                    }
                        break;
                    case FieldType.Int64:
                    {
                        var repeated = new RepeatedField<long>();
                        repeated.AddEntriesFrom(ref input, FieldCodec.ForInt64(tag));
                        value = new List<long>(repeated);
                    }
                        break;
                    case FieldType.UInt64:
                    {
                        var repeated = new RepeatedField<ulong>();
                        repeated.AddEntriesFrom(ref input, FieldCodec.ForUInt64(tag));
                        value = new List<ulong>(repeated);
                    }
                        break;
                    case FieldType.Int32:
                    {
                        var repeated = new RepeatedField<int>();
                        repeated.AddEntriesFrom(ref input, FieldCodec.ForInt32(tag));
                        value = new List<int>(repeated);
                    }
                        break;
                    case FieldType.Fixed64:
                    {
                        var repeated = new RepeatedField<ulong>();
                        repeated.AddEntriesFrom(ref input, FieldCodec.ForFixed64(tag));
                        value = new List<ulong>(repeated);
                    }
                        break;
                    case FieldType.Fixed32:
                    {
                        var repeated = new RepeatedField<uint>();
                        repeated.AddEntriesFrom(ref input, FieldCodec.ForFixed32(tag));
                        value = new List<uint>(repeated);
                    }
                        break;
                    case FieldType.Bool:
                    {
                        var repeated = new RepeatedField<bool>();
                        repeated.AddEntriesFrom(ref input, FieldCodec.ForBool(tag));
                        value = new List<bool>(repeated);
                    }
                        break;
                    case FieldType.String:
                    {
                        var repeated = new RepeatedField<string>();
                        repeated.AddEntriesFrom(ref input, FieldCodec.ForString(tag));
                        value = new List<string>(repeated);
                    }
                        break;
                    case FieldType.Group:
                    {
                        var endTag = GetGroupEndTag(tag);
                        var descriptorForRepeatedMessage = GetSafeDescriptor(fieldDescriptor.MessageType.FullName);
                        var parser = new MessageParser<DynamicMessage>(() => new DynamicMessage(descriptorForRepeatedMessage, context.Factory(), context));
                        var repeated = new RepeatedField<DynamicMessage>();
                        repeated.AddEntriesFrom(ref input, FieldCodec.ForGroup(tag, endTag, parser));
                        var dict = new List<IDictionary<string, object>>(repeated.Count);
                        foreach (var item in repeated)
                        {
                            dict.Add(item.Value);
                        }

                        value = dict;
                    }
                        break;
                    case FieldType.Message:
                    {
                        var descriptorForRepeatedMessage = GetSafeDescriptor(fieldDescriptor.MessageType.FullName);
                        var parser = new MessageParser<DynamicMessage>(() => new DynamicMessage(descriptorForRepeatedMessage, context.Factory(), context));
                        var repeated = new RepeatedField<DynamicMessage>();
                        repeated.AddEntriesFrom(ref input, FieldCodec.ForMessage(tag, parser));
                        var dict = new List<IDictionary<string, object>>(repeated.Count);
                        foreach (var item in repeated)
                        {
                            dict.Add(item.Value);
                        }

                        value = dict;
                    }
                        break;
                    case FieldType.Bytes:
                    {
                        var repeated = new RepeatedField<ByteString>();
                        repeated.AddEntriesFrom(ref input, FieldCodec.ForBytes(tag));
                        var list = new List<byte[]>();
                        foreach (var item in repeated)
                        {
                            list.Add(item.ToByteArray());
                        }

                        value = list;
                    }
                        break;
                    case FieldType.UInt32:
                    {
                        var repeated = new RepeatedField<uint>();
                        repeated.AddEntriesFrom(ref input, FieldCodec.ForUInt32(tag));
                        value = new List<uint>(repeated);
                    }
                        break;
                    case FieldType.SFixed32:
                    {
                        var repeated = new RepeatedField<int>();
                        repeated.AddEntriesFrom(ref input, FieldCodec.ForSFixed32(tag));
                        value = new List<int>(repeated);
                    }
                        break;
                    case FieldType.SFixed64:
                    {
                        var repeated = new RepeatedField<long>();
                        repeated.AddEntriesFrom(ref input, FieldCodec.ForSFixed64(tag));
                        value = new List<long>(repeated);
                    }
                        break;
                    case FieldType.SInt32:
                    {
                        var repeated = new RepeatedField<int>();
                        repeated.AddEntriesFrom(ref input, FieldCodec.ForSInt32(tag));
                        value = new List<int>(repeated);
                    }
                        break;
                    case FieldType.SInt64:
                    {
                        var repeated = new RepeatedField<long>();
                        repeated.AddEntriesFrom(ref input, FieldCodec.ForSInt64(tag));
                        value = new List<long>(repeated);
                    }
                        break;
                    case FieldType.Enum:
                    {
                        var enumType = fieldDescriptor.EnumType;
                        var repeated = new RepeatedField<int>();
                        repeated.AddEntriesFrom(ref input, FieldCodec.ForEnum(tag, null, i => i));
                        if (context.UseNumberedEnums)
                        {
                            value = new List<int>(repeated);
                        }
                        else
                        {
                            var listString = new List<string>(repeated.Count);
                            foreach (var i in repeated)
                            {
                                listString.Add(enumType.FindValueByNumber(i).Name);
                            }
                            value = listString;
                        }
                    }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            else
            {
                value = ReadFieldValue(ref input, Descriptor, fieldDescriptor, context);
            }

            result.Add(context.UseJsonNaming ? fieldDescriptor.JsonName: fieldDescriptor.Name, value);
        }

        return result;
    }

    private static uint GetGroupEndTag(uint tag)
    {
        Debug.Assert(WireFormat.GetTagWireType(tag) == WireFormat.WireType.StartGroup);
        return WireFormat.MakeTag(WireFormat.GetTagFieldNumber(tag), WireFormat.WireType.EndGroup);
    }

    private object ReadFieldValue(ref ParseContext input, MessageDescriptor parentDescriptor, FieldDescriptor fieldDescriptor, DynamicGrpcClientContext context)
    {
        object value;
        switch (fieldDescriptor.FieldType)
        {
            case FieldType.Double:
                value = input.ReadDouble();
                break;
            case FieldType.Float:
                value = input.ReadFloat();
                break;
            case FieldType.Int64:
                value = input.ReadInt64();
                break;
            case FieldType.UInt64:
                value = input.ReadUInt64();
                break;
            case FieldType.Int32:
                value = input.ReadInt32();
                break;
            case FieldType.Fixed64:
                value = input.ReadFixed64();
                break;
            case FieldType.Fixed32:
                value = input.ReadFixed32();
                break;
            case FieldType.Bool:
                value = input.ReadBool();
                break;
            case FieldType.String:
                value = input.ReadString();
                break;
            case FieldType.Group:
            {
                var descriptor = GetSafeDescriptor(fieldDescriptor.MessageType.FullName);
                var message = new DynamicMessage(descriptor, context.Factory(), context);
                input.ReadGroup(message);
                value = message.Value;
            }
                break;
            case FieldType.Message:
            {
                var descriptor = GetSafeDescriptor(fieldDescriptor.MessageType.FullName);
                var message = new DynamicMessage(descriptor, context.Factory(), context);
                input.ReadMessage(message);
                value = message.Value;
            }
                break;
            case FieldType.Bytes:
                value = input.ReadBytes().ToByteArray();
                break;
            case FieldType.UInt32:
                value = input.ReadUInt32();
                break;
            case FieldType.SFixed32:
                value = input.ReadSFixed32();
                break;
            case FieldType.SFixed64:
                value = input.ReadSFixed64();
                break;
            case FieldType.SInt32:
                value = input.ReadSInt32();
                break;
            case FieldType.SInt64:
                value = input.ReadSInt64();
                break;
            case FieldType.Enum:
                if (context.UseNumberedEnums)
                {
                    value = input.ReadEnum();
                }
                else
                {
                    var number = input.ReadEnum();
                    value = fieldDescriptor.EnumType.FindValueByNumber(number).Name;
                }

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return value;
    }

    /// <summary>
    /// return true if the field is a repeated packed field, false otherwise.
    /// </summary>
    private static bool IsRepeatedPacked(FieldDescriptor field)
    {
        // Workaround as field.IsPacked will throw when GetOptions() is null
        bool isRepeatedPacked = false;
        var options = field.GetOptions();
        if (options == null)
        {
            if (field.File.Syntax == Syntax.Proto3)
            {
                isRepeatedPacked = field.IsRepeated;
            }
        }
        else
        {
            isRepeatedPacked = field.IsRepeated && field.IsPacked;
        }

        return isRepeatedPacked;
    }

    public void WriteTo(IDictionary<string, object> value, ref WriteContext output, DynamicGrpcClientContext context)
    {
        var nameToField = context.UseJsonNaming ? _jsonNameToField : _nameToField;

        foreach (var keyValue in value)
        {
            if (!nameToField.TryGetValue(keyValue.Key, out var fieldDescriptor))
            {
                throw new DynamicGrpcClientException($"Field `{keyValue.Key}` not found in message type `{Descriptor.FullName}`.");
            }

            var tag  = GetTagForField(fieldDescriptor);

            if (fieldDescriptor.IsMap)
            {
                if (keyValue.Value is not IDictionary values)
                {
                    throw new DynamicGrpcClientException($"Repeated Field `{keyValue.Key}` is expecting an IDictionary type instead of {keyValue.Value?.GetType()?.FullName}.");
                }

                var fieldMessageType = fieldDescriptor.MessageType;
                var keyField = fieldMessageType.Fields.InDeclarationOrder()[0];
                var keyTag = GetTagForField(keyField);
                var valueField = fieldMessageType.Fields.InDeclarationOrder()[1];
                var valueTag = GetTagForField(valueField);

                var it = values.GetEnumerator();
                while (it.MoveNext())
                {
                    var keyFromMap = it.Key;
                    var valueFromMap = it.Value;
                    output.WriteTag(tag);
                    
                    var length = 0;
                    var isKeyDefaultValue = DefaultValueHelper.IsDefaultValue(keyField.FieldType, keyFromMap);
                    if (!isKeyDefaultValue)
                    {
                        length += CodedOutputStream.ComputeRawVarint32Size(keyTag) + ComputeSimpleFieldSize(keyTag, fieldMessageType, keyField, "key", keyFromMap, context);
                    }
                    var isValueDefaultValue = DefaultValueHelper.IsDefaultValue(valueField.FieldType, valueFromMap);
                    if (!isValueDefaultValue)
                    {
                        Debug.Assert(valueFromMap is not null);
                        length += CodedOutputStream.ComputeRawVarint32Size(valueTag) + ComputeSimpleFieldSize(valueTag, fieldMessageType, valueField, "value", valueFromMap!, context);
                    }
                    output.WriteLength(length);

                    if (!isKeyDefaultValue)
                    {
                        WriteFieldValue(keyTag, fieldMessageType, keyField, "key", keyFromMap, ref output, context);
                    }

                    if (!isValueDefaultValue)
                    {
                        Debug.Assert(valueFromMap is not null);
                        WriteFieldValue(valueTag, fieldMessageType, valueField, "value", valueFromMap!, ref output, context);
                    }
                }
            }
            else if (fieldDescriptor.IsRepeated)
            {
                if (keyValue.Value is not IEnumerable values)
                {
                    throw new DynamicGrpcClientException($"Repeated Field `{keyValue.Key}` is expecting an IEnumerable type instead of {keyValue.Value?.GetType()?.FullName}.");
                }

                // TODO: inefficient copy here to RepeatedField. We should have access to FieldCodec internals
                // https://github.com/protocolbuffers/protobuf/issues/9432
                switch (fieldDescriptor.FieldType)
                {
                    case FieldType.Double:
                        {
                            var repeated = new RepeatedField<double>();
                            foreach(var item in values) repeated.Add(Convert.ToDouble(item));
                            repeated.WriteTo(ref output, FieldCodec.ForDouble(tag));
                        }
                        break;
                    case FieldType.Float:
                        {
                            var repeated = new RepeatedField<float>();
                            foreach (var item in values) repeated.Add(Convert.ToSingle(item));
                            repeated.WriteTo(ref output, FieldCodec.ForFloat(tag));
                        }
                        break;
                    case FieldType.Int64:
                        {
                            var repeated = new RepeatedField<long>();
                            foreach (var item in values) repeated.Add(Convert.ToInt64(item));
                            repeated.WriteTo(ref output, FieldCodec.ForInt64(tag));
                        }
                        break;
                    case FieldType.UInt64:
                        {
                            var repeated = new RepeatedField<ulong>();
                            foreach (var item in values) repeated.Add(Convert.ToUInt64(item));
                            repeated.WriteTo(ref output, FieldCodec.ForUInt64(tag));
                        }
                        break;
                    case FieldType.Int32:
                        {
                            var repeated = new RepeatedField<int>();
                            foreach (var item in values) repeated.Add(Convert.ToInt32(item));
                            repeated.WriteTo(ref output, FieldCodec.ForInt32(tag));
                        }
                        break;
                    case FieldType.Fixed64:
                        {
                            var repeated = new RepeatedField<ulong>();
                            foreach (var item in values) repeated.Add(Convert.ToUInt64(item));
                            repeated.WriteTo(ref output, FieldCodec.ForFixed64(tag));
                        }
                        break;
                    case FieldType.Fixed32:
                        {
                            var repeated = new RepeatedField<uint>();
                            foreach (var item in values) repeated.Add(Convert.ToUInt32(item));
                            repeated.WriteTo(ref output, FieldCodec.ForFixed32(tag));
                        }
                        break;
                    case FieldType.Bool:
                        {
                            var repeated = new RepeatedField<bool>();
                            foreach (var item in values) repeated.Add(Convert.ToBoolean(item));
                            repeated.WriteTo(ref output, FieldCodec.ForBool(tag));
                        }
                        break;
                    case FieldType.String:
                        {
                            var repeated = new RepeatedField<string?>();
                            foreach (var item in values) repeated.Add(Convert.ToString(item));
                            repeated.WriteTo(ref output, FieldCodec.ForString(tag));
                        }
                        break;
                    case FieldType.Group:
                    {
                        var descriptorForRepeatedMessage = GetSafeDescriptor(fieldDescriptor.MessageType.FullName);
                        var parser = new MessageParser<DynamicMessage>(() => new DynamicMessage(descriptorForRepeatedMessage, context.Factory(), context));
                        var repeated = new RepeatedField<DynamicMessage>();
                        foreach (var item in values)
                        {
                            repeated.Add(new DynamicMessage(descriptorForRepeatedMessage, (IDictionary<string, object>)item, context));
                        }

                        repeated.WriteTo(ref output, FieldCodec.ForGroup(tag, GetGroupEndTag(tag), parser));
                    }
                        break;
                    case FieldType.Message:
                        {
                            var descriptorForRepeatedMessage = GetSafeDescriptor(fieldDescriptor.MessageType.FullName);
                            var parser = new MessageParser<DynamicMessage>(() => new DynamicMessage(descriptorForRepeatedMessage, context.Factory(), context));
                            var repeated = new RepeatedField<DynamicMessage>();
                            foreach (var item in values)
                            {
                                repeated.Add(new DynamicMessage(descriptorForRepeatedMessage, (IDictionary<string, object>)item, context));
                            }

                            repeated.WriteTo(ref output, FieldCodec.ForMessage(tag, parser));
                        }
                        break;
                    case FieldType.Bytes:
                        {
                            var repeated = new RepeatedField<ByteString>();
                            foreach (var item in values) repeated.Add(ByteString.CopyFrom((byte[])item));
                            repeated.WriteTo(ref output, FieldCodec.ForBytes(tag));
                        }
                        break;
                    case FieldType.UInt32:
                        {
                            var repeated = new RepeatedField<uint>();
                            foreach (var item in values) repeated.Add(Convert.ToUInt32(item));
                            repeated.WriteTo(ref output, FieldCodec.ForUInt32(tag));
                        }
                        break;
                    case FieldType.SFixed32:
                        {
                            var repeated = new RepeatedField<int>();
                            foreach (var item in values) repeated.Add(Convert.ToInt32(item));
                            repeated.WriteTo(ref output, FieldCodec.ForSFixed32(tag));
                        }
                        break;
                    case FieldType.SFixed64:
                        {
                            var repeated = new RepeatedField<long>();
                            foreach (var item in values) repeated.Add(Convert.ToInt64(item));
                            repeated.WriteTo(ref output, FieldCodec.ForSFixed64(tag));
                        }
                        break;
                    case FieldType.SInt32:
                        {
                            var repeated = new RepeatedField<int>();
                            foreach (var item in values) repeated.Add(Convert.ToInt32(item));
                            repeated.WriteTo(ref output, FieldCodec.ForSInt32(tag));
                        }
                        break;
                    case FieldType.SInt64:
                        {
                            var repeated = new RepeatedField<long>();
                            foreach (var item in values) repeated.Add(Convert.ToInt64(item));
                            repeated.WriteTo(ref output, FieldCodec.ForSInt64(tag));
                        }
                        break;
                    case FieldType.Enum:
                        {
                            var repeated = new RepeatedField<int>();
                            foreach (var item in values)
                            {
                                var enumInputValue = item;
                                if (enumInputValue is Enum realEnum)
                                {
                                    enumInputValue = realEnum.ToString();
                                }

                                var rawEnumValue = enumInputValue is string enumAsText ? fieldDescriptor.EnumType.FindValueByName(enumAsText).Number : Convert.ToInt32(enumInputValue);
                                repeated.Add(rawEnumValue);
                            }
                            repeated.WriteTo(ref output, FieldCodec.ForEnum<int>(tag, o => o, i => i, 0));
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            else
            {
                WriteFieldValue(tag, Descriptor, fieldDescriptor, keyValue.Key, keyValue.Value, ref output, context);
            }
        }
    }

    private void WriteFieldValue(uint tag, MessageDescriptor parentDescriptor, FieldDescriptor fieldDescriptor, string keyName, object value, ref WriteContext output, DynamicGrpcClientContext context)
    {
        output.WriteTag(tag);
        switch (fieldDescriptor.FieldType)
        {
            case FieldType.Double:
                output.WriteDouble(Convert.ToDouble(value));
                break;
            case FieldType.Float:
                output.WriteFloat(Convert.ToSingle(value));
                break;
            case FieldType.Int64:
                output.WriteInt64(Convert.ToInt64(value));
                break;
            case FieldType.UInt64:
                output.WriteUInt64(Convert.ToUInt64(value));
                break;
            case FieldType.Int32:
                output.WriteInt32(Convert.ToInt32(value));
                break;
            case FieldType.Fixed64:
                output.WriteFixed64(Convert.ToUInt64(value));
                break;
            case FieldType.Fixed32:
                output.WriteFixed32(Convert.ToUInt32(value));
                break;
            case FieldType.Bool:
                output.WriteBool(Convert.ToBoolean(value));
                break;
            case FieldType.String:
                output.WriteString(Convert.ToString(value));
                break;
            case FieldType.Group:
            {
                var descriptor = GetSafeDescriptor(fieldDescriptor.MessageType.FullName);
                var valueToSerialize = (IDictionary<string, object>)value;
                var sizeOfValueToSerialize = descriptor.ComputeSize(valueToSerialize, context);
                output.WriteLength(sizeOfValueToSerialize);
                descriptor.WriteTo((IDictionary<string, object>)value, ref output, context);
                output.WriteTag(GetGroupEndTag(tag));
                break;
            }
            case FieldType.Message:
            {
                var descriptor = GetSafeDescriptor(fieldDescriptor.MessageType.FullName);
                var valueToSerialize = (IDictionary<string, object>)value;
                var sizeOfValueToSerialize = descriptor.ComputeSize(valueToSerialize, context);
                output.WriteLength(sizeOfValueToSerialize);
                descriptor.WriteTo((IDictionary<string, object>)value, ref output, context);
                break;
            }
            case FieldType.Bytes:
                output.WriteBytes(ByteString.CopyFrom((byte[])value));
                break;
            case FieldType.UInt32:
                output.WriteUInt32(Convert.ToUInt32(value));
                break;
            case FieldType.SFixed32:
                output.WriteSFixed32(Convert.ToInt32(value));
                break;
            case FieldType.SFixed64:
                output.WriteSFixed64(Convert.ToInt64(value));
                break;
            case FieldType.SInt32:
                output.WriteSInt32(Convert.ToInt32(value));
                break;
            case FieldType.SInt64:
                output.WriteSInt64(Convert.ToInt64(value));
                break;
            case FieldType.Enum:
                var enumInputValue = value;
                if (enumInputValue is Enum realEnum)
                {
                    enumInputValue = realEnum.ToString();
                }

                int enumValue = enumInputValue is string enumAsText ? fieldDescriptor.EnumType.FindValueByName(enumAsText).Number : Convert.ToInt32(enumInputValue);
                output.WriteEnum(enumValue);
                break;
            default:
                throw new ArgumentOutOfRangeException($"Unsupported field type `{fieldDescriptor.FieldType}`= {(int)fieldDescriptor.FieldType} in message type `{parentDescriptor.FullName}`.");
        }
    }

    public int ComputeSize(IDictionary<string, object> value, DynamicGrpcClientContext context)
    {
        int size = 0;
        foreach (var keyValue in value)
        {
            var fieldDescriptor = _nameToField[keyValue.Key];
            var tag = GetTagForField(fieldDescriptor);
            var fieldSize = ComputeFieldSize(tag, Descriptor, fieldDescriptor, keyValue.Key, keyValue.Value, context);
            size += fieldSize;
        }

        return size;
    }

    private int ComputeFieldSize(uint tag, MessageDescriptor parentDescriptor, FieldDescriptor fieldDescriptor, string key, object value, DynamicGrpcClientContext context)
    {
        int fieldSize;

        if (fieldDescriptor.IsMap)
        {
            if (value is not IDictionary values)
            {
                throw new DynamicGrpcClientException($"Repeated Field `{key}` is expecting an IDictionary type instead of {value?.GetType()?.FullName}.");
            }

            var fieldMessageType = fieldDescriptor.MessageType;
            var keyField = fieldMessageType.Fields.InDeclarationOrder()[0];
            var keyTag = GetTagForField(keyField);
            var keyTagSize = CodedOutputStream.ComputeUInt32Size(keyTag);
            var valueField = fieldMessageType.Fields.InDeclarationOrder()[1];
            var valueTag = GetTagForField(valueField);
            var valueTagSize = CodedOutputStream.ComputeUInt32Size(valueTag);
            var tagSize = CodedOutputStream.ComputeUInt32Size(tag);

            var it = values.GetEnumerator();
            fieldSize = 0;
            while (it.MoveNext())
            {
                var keyFromMap = it.Key;
                var valueFromMap = it.Value;
                fieldSize += tagSize;

                var keyValueSize = 0;
                var isKeyDefaultValue = DefaultValueHelper.IsDefaultValue(keyField.FieldType, keyFromMap);
                if (!isKeyDefaultValue)
                {
                    keyValueSize += keyTagSize;
                    keyValueSize += ComputeSimpleFieldSize(keyTag, fieldMessageType, keyField, "key", keyFromMap, context);
                }
                var isValueDefaultValue = DefaultValueHelper.IsDefaultValue(valueField.FieldType, valueFromMap);
                if (!isValueDefaultValue)
                {
                    Debug.Assert(valueFromMap is not null);
                    keyValueSize += valueTagSize;
                    keyValueSize += ComputeSimpleFieldSize(valueTag, fieldMessageType, valueField, "value", valueFromMap!, context);
                }

                fieldSize += CodedOutputStream.ComputeLengthSize(keyValueSize);
                fieldSize += keyValueSize;
            }
        }
        else if (fieldDescriptor.IsRepeated)
        {
            if (value is not IEnumerable values)
            {
                throw new DynamicGrpcClientException($"Repeated Field `{key}` is expecting an IEnumerable type instead of {value?.GetType()?.FullName}.");
            }

            switch (fieldDescriptor.FieldType)
            {
                case FieldType.Double:
                    {
                        var repeated = new RepeatedField<double>();
                        foreach (var item in values) repeated.Add(Convert.ToDouble(item));
                        fieldSize = repeated.CalculateSize(FieldCodec.ForDouble(tag));
                    }
                    break;
                case FieldType.Float:
                    {
                        var repeated = new RepeatedField<float>();
                        foreach (var item in values) repeated.Add(Convert.ToSingle(item));
                        fieldSize = repeated.CalculateSize(FieldCodec.ForFloat(tag));
                    }
                    break;
                case FieldType.Int64:
                    {
                        var repeated = new RepeatedField<long>();
                        foreach (var item in values) repeated.Add(Convert.ToInt64(item));
                        fieldSize = repeated.CalculateSize(FieldCodec.ForInt64(tag));
                    }
                    break;
                case FieldType.UInt64:
                    {
                        var repeated = new RepeatedField<ulong>();
                        foreach (var item in values) repeated.Add(Convert.ToUInt64(item));
                        fieldSize = repeated.CalculateSize(FieldCodec.ForUInt64(tag));
                    }
                    break;
                case FieldType.Int32:
                    {
                        var repeated = new RepeatedField<int>();
                        foreach (var item in values) repeated.Add(Convert.ToInt32(item));
                        fieldSize = repeated.CalculateSize(FieldCodec.ForInt32(tag));
                    }
                    break;
                case FieldType.Fixed64:
                    {
                        var repeated = new RepeatedField<ulong>();
                        foreach (var item in values) repeated.Add(Convert.ToUInt64(item));
                        fieldSize = repeated.CalculateSize(FieldCodec.ForFixed64(tag));
                    }
                    break;
                case FieldType.Fixed32:
                    {
                        var repeated = new RepeatedField<uint>();
                        foreach (var item in values) repeated.Add(Convert.ToUInt32(item));
                        fieldSize = repeated.CalculateSize(FieldCodec.ForFixed32(tag));
                    }
                    break;
                case FieldType.Bool:
                    {
                        var repeated = new RepeatedField<bool>();
                        foreach (var item in values) repeated.Add(Convert.ToBoolean(item));
                        fieldSize = repeated.CalculateSize(FieldCodec.ForBool(tag));
                    }
                    break;
                case FieldType.String:
                    {
                        var repeated = new RepeatedField<string?>();
                        foreach (var item in values) repeated.Add(Convert.ToString(item));
                        fieldSize = repeated.CalculateSize(FieldCodec.ForString(tag));
                    }
                    break;
                case FieldType.Group:
                {
                    var descriptorForRepeatedMessage = GetSafeDescriptor(fieldDescriptor.MessageType.FullName);
                    var parser = new MessageParser<DynamicMessage>(() => new DynamicMessage(descriptorForRepeatedMessage, context.Factory(), context));
                    var repeated = new RepeatedField<DynamicMessage>();
                    foreach (var item in values)
                    {
                        repeated.Add(new DynamicMessage(descriptorForRepeatedMessage, (IDictionary<string, object>)item, context));
                    }
                    fieldSize = repeated.CalculateSize(FieldCodec.ForGroup(tag, GetGroupEndTag(tag), parser));
                }
                    break;
                case FieldType.Message:
                    {
                        var descriptorForRepeatedMessage = GetSafeDescriptor(fieldDescriptor.MessageType.FullName);
                        var parser = new MessageParser<DynamicMessage>(() => new DynamicMessage(descriptorForRepeatedMessage, context.Factory(), context));
                        var repeated = new RepeatedField<DynamicMessage>();
                        foreach (var item in values)
                        {
                            repeated.Add(new DynamicMessage(descriptorForRepeatedMessage, (IDictionary<string, object>)item, context));
                        }
                        fieldSize = repeated.CalculateSize(FieldCodec.ForMessage(tag, parser));
                    }
                    break;
                case FieldType.Bytes:
                    {
                        var repeated = new RepeatedField<ByteString>();
                        foreach (var item in values) repeated.Add(ByteString.CopyFrom((byte[])item));
                        fieldSize = repeated.CalculateSize(FieldCodec.ForBytes(tag));
                    }
                    break;
                case FieldType.UInt32:
                    {
                        var repeated = new RepeatedField<uint>();
                        foreach (var item in values) repeated.Add(Convert.ToUInt32(item));
                        fieldSize = repeated.CalculateSize(FieldCodec.ForUInt32(tag));
                    }
                    break;
                case FieldType.SFixed32:
                    {
                        var repeated = new RepeatedField<int>();
                        foreach (var item in values) repeated.Add(Convert.ToInt32(item));
                        fieldSize = repeated.CalculateSize(FieldCodec.ForSFixed32(tag));
                    }
                    break;
                case FieldType.SFixed64:
                    {
                        var repeated = new RepeatedField<long>();
                        foreach (var item in values) repeated.Add(Convert.ToInt64(item));
                        fieldSize = repeated.CalculateSize(FieldCodec.ForSFixed64(tag));
                    }
                    break;
                case FieldType.SInt32:
                    {
                        var repeated = new RepeatedField<int>();
                        foreach (var item in values) repeated.Add(Convert.ToInt32(item));
                        fieldSize = repeated.CalculateSize(FieldCodec.ForSInt32(tag));
                    }
                    break;
                case FieldType.SInt64:
                    {
                        var repeated = new RepeatedField<long>();
                        foreach (var item in values) repeated.Add(Convert.ToInt64(item));
                        fieldSize = repeated.CalculateSize(FieldCodec.ForSInt64(tag));
                    }
                    break;
                case FieldType.Enum:
                    {
                        var repeated = new RepeatedField<int>();
                        foreach (var item in values)
                        {
                            var enumInputValue = item;
                            if (enumInputValue is Enum realEnum)
                            {
                                enumInputValue = realEnum.ToString();
                            }

                            var rawEnumValue = enumInputValue is string enumAsText ? fieldDescriptor.EnumType.FindValueByName(enumAsText).Number : Convert.ToInt32(enumInputValue);
                            repeated.Add(rawEnumValue);
                        }
                        fieldSize = repeated.CalculateSize(FieldCodec.ForEnum<int>(tag, o => o, i => i, 0));
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            fieldSize += CodedOutputStream.ComputeRawVarint32Size(tag);
        }
        else
        {
            fieldSize = ComputeSimpleFieldSize(tag, parentDescriptor, fieldDescriptor, key, value, context);
            fieldSize += CodedOutputStream.ComputeRawVarint32Size(tag);
        }

        return fieldSize;
    }

    private int ComputeSimpleFieldSize(uint tag, MessageDescriptor parentDescriptor, FieldDescriptor fieldDescriptor, string key, object value, DynamicGrpcClientContext context)
    {
        int fieldSize;
        switch (fieldDescriptor.FieldType)
        {
            case FieldType.Double:
                fieldSize = CodedOutputStream.ComputeDoubleSize(Convert.ToDouble(value));
                break;
            case FieldType.Float:
                fieldSize = CodedOutputStream.ComputeFloatSize(Convert.ToSingle(value));
                break;
            case FieldType.Int64:
                fieldSize = CodedOutputStream.ComputeInt64Size(Convert.ToInt64(value));
                break;
            case FieldType.UInt64:
                fieldSize = CodedOutputStream.ComputeUInt64Size(Convert.ToUInt64(value));
                break;
            case FieldType.Int32:
                fieldSize = CodedOutputStream.ComputeInt32Size(Convert.ToInt32(value));
                break;
            case FieldType.Fixed64:
                fieldSize = CodedOutputStream.ComputeFixed64Size(Convert.ToUInt64(value));
                break;
            case FieldType.Fixed32:
                fieldSize = CodedOutputStream.ComputeFixed32Size(Convert.ToUInt32(value));
                break;
            case FieldType.Bool:
                fieldSize = CodedOutputStream.ComputeBoolSize(Convert.ToBoolean(value));
                break;
            case FieldType.String:
                fieldSize = CodedOutputStream.ComputeStringSize(Convert.ToString(value));
                break;
            case FieldType.Group:
            {
                var descriptor = GetSafeDescriptor(fieldDescriptor.MessageType.FullName);
                var messageSize = descriptor.ComputeSize((IDictionary<string, object>)value, context);
                fieldSize = CodedOutputStream.ComputeLengthSize(messageSize) + messageSize;
                fieldSize += CodedOutputStream.ComputeRawVarint32Size(GetGroupEndTag(tag));
            }
                break;
            case FieldType.Message:
            {
                var descriptor = GetSafeDescriptor(fieldDescriptor.MessageType.FullName);
                var messageSize = descriptor.ComputeSize((IDictionary<string, object>)value, context);
                fieldSize = CodedOutputStream.ComputeLengthSize(messageSize) + messageSize;
            }
                break;
            case FieldType.Bytes:
                var bytes = (byte[])value;
                fieldSize = CodedOutputStream.ComputeLengthSize(bytes.Length) + bytes.Length;
                break;
            case FieldType.UInt32:
                fieldSize = CodedOutputStream.ComputeUInt32Size(Convert.ToUInt32(value));
                break;
            case FieldType.SFixed32:
                fieldSize = CodedOutputStream.ComputeSFixed32Size(Convert.ToInt32(value));
                break;
            case FieldType.SFixed64:
                fieldSize = CodedOutputStream.ComputeSFixed64Size(Convert.ToInt64(value));
                break;
            case FieldType.SInt32:
                fieldSize = CodedOutputStream.ComputeSInt32Size(Convert.ToInt32(value));
                break;
            case FieldType.SInt64:
                fieldSize = CodedOutputStream.ComputeSInt64Size(Convert.ToInt64(value));
                break;
            case FieldType.Enum:
                fieldSize = CodedOutputStream.ComputeEnumSize(Convert.ToInt32(value));
                break;
            default:
                throw new ArgumentOutOfRangeException($"Unsupported field type `{fieldDescriptor.FieldType}`= {(int)fieldDescriptor.FieldType} in message type `{parentDescriptor.FullName}`.");
        }

        return fieldSize;
    }

    private static WireFormat.WireType GetWireType(FieldType type, bool isPackedRepeated)
    {
        if (isPackedRepeated) return WireFormat.WireType.LengthDelimited;

        switch (type)
        {
            case FieldType.SFixed64:
            case FieldType.Fixed64:
            case FieldType.Double:
                return WireFormat.WireType.Fixed64;
            case FieldType.SFixed32:
            case FieldType.Fixed32:
            case FieldType.Float:
                return WireFormat.WireType.Fixed32;
            case FieldType.Enum:
            case FieldType.Bool:
            case FieldType.SInt32:
            case FieldType.UInt32:
            case FieldType.Int32:
            case FieldType.Int64:
            case FieldType.SInt64:
            case FieldType.UInt64:
                return WireFormat.WireType.Varint;
            case FieldType.String:
            case FieldType.Message:
            case FieldType.Bytes:
                return WireFormat.WireType.LengthDelimited;
            case FieldType.Group:
                return WireFormat.WireType.StartGroup;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    
    private DynamicMessageSerializer GetSafeDescriptor(string typeName)
    {
        if (!DescriptorSet.TryFindMessageDescriptorProto(typeName, out var descriptor))
        {
            throw new InvalidOperationException($"Cannot find type {typeName}");
        }

        return descriptor;
    }

    private class DynamicMessage : IMessage<DynamicMessage>, IBufferMessage
    {
        public DynamicMessage(DynamicMessageSerializer dynamicMessageSerializer, IDictionary<string, object> value, DynamicGrpcClientContext context)
        {
            DynamicMessageSerializer = dynamicMessageSerializer;
            Value = value;
            Context = context;
        }

        private DynamicMessageSerializer DynamicMessageSerializer { get; }

        public IDictionary<string, object> Value { get; private set; }

        private DynamicGrpcClientContext Context { get; }

        public void InternalMergeFrom(ref ParseContext ctx)
        {
            Value = DynamicMessageSerializer.ReadFrom(ref ctx, Context);
        }

        public void InternalWriteTo(ref WriteContext ctx)
        {
            DynamicMessageSerializer.WriteTo(Value, ref ctx, Context);
        }

        public void MergeFrom(DynamicMessage message)
        {
            foreach (var keyPair in message.Value)
            {
                Value[keyPair.Key] = keyPair.Value;
            }
        }

        public void MergeFrom(CodedInputStream input)
        {
            throw new NotImplementedException();
        }

        public void WriteTo(CodedOutputStream output)
        {
            throw new NotImplementedException();
        }

        public int CalculateSize()
        {
            return DynamicMessageSerializer.ComputeSize(Value, Context);
        }

        public MessageDescriptor Descriptor => DynamicMessageSerializer.Descriptor;

        public bool Equals(DynamicMessage? other)
        {
            return false;
        }

        public DynamicMessage Clone()
        {
            var newValue = Context.Factory();
            foreach (var keyPair in Value)
            {
                newValue.Add(keyPair);
            }

            return new DynamicMessage(DynamicMessageSerializer, newValue, Context);
        }
    }

    /// <summary>
    /// https://developers.google.com/protocol-buffers/docs/proto3#default
    /// </summary>
    private static class DefaultValueHelper
    {
        public static object? GetDefaultValue(FieldType type)
        {
            // For strings, the default value is the empty string.
            // For bytes, the default value is empty bytes.
            // For bools, the default value is false.
            // For numeric types, the default value is zero.
            // For enums, the default value is the first defined enum value, which must be 0.
            switch (type)
            {
                case FieldType.Double:
                    return DefaultDouble;
                case FieldType.Float:
                    return DefaultFloat;
                case FieldType.Int64:
                    return DefaultInt64;
                case FieldType.UInt64:
                    return DefaultUInt64;
                case FieldType.Int32:
                    return DefaultInt32;
                case FieldType.Fixed64:
                    return DefaultFixed64;
                case FieldType.Fixed32:
                    return DefaultFixed32;
                case FieldType.Bool:
                    return DefaultBool;
                case FieldType.UInt32:
                    return DefaultUInt32;
                case FieldType.SFixed32:
                    return DefaultSFixed32;
                case FieldType.SFixed64:
                    return DefaultSFixed64;
                case FieldType.SInt32:
                    return DefaultSInt32;
                case FieldType.SInt64:
                    return DefaultSInt64;
                case FieldType.Enum:
                    return DefaultEnum;
                case FieldType.String:
                    return DefaultString;
                case FieldType.Bytes:
                    return DefaultBytes;
                default:
                    return null;
            }
        }

        public static bool IsDefaultValue(FieldType type, object? value)
        {
            var defaultValue = GetDefaultValue(type);
            return defaultValue != null && defaultValue.Equals(value);
        }

        private static readonly object DefaultDouble = 0.0;
        private static readonly object DefaultFloat = 0.0f;
        private static readonly object DefaultInt64 = 0L;
        private static readonly object DefaultUInt64 = 0UL;
        private static readonly object DefaultInt32 = 0;
        private static readonly object DefaultFixed64 = 0UL;
        private static readonly object DefaultFixed32 = 0U;
        private static readonly object DefaultBool = false;
        private static readonly object DefaultUInt32 = 0U;
        private static readonly object DefaultSFixed32 = 0;
        private static readonly object DefaultSFixed64 = 0L;
        private static readonly object DefaultSInt32 = 0;
        private static readonly object DefaultSInt64 = 0L;
        private static readonly object DefaultEnum = 0;
        private static readonly object DefaultString = string.Empty;
        private static readonly object DefaultBytes = Array.Empty<byte>();
    }
}