using System;
using System.Threading.Tasks;
using Grpc.Core;
using Primitives;

namespace GrpcCurl.Tests.Proto;

public class PrimitiveServiceImpl : PrimitiveService.PrimitiveServiceBase
{
    public override Task<double_InOut> Request_double(double_InOut request, ServerCallContext context) => Task.FromResult(new double_InOut() { Value = request.Value + 1 });
    public override Task<float_InOut> Request_float(float_InOut request, ServerCallContext context) => Task.FromResult(new float_InOut() { Value = request.Value + 1 });
    public override Task<int32_InOut> Request_int32(int32_InOut request, ServerCallContext context) => Task.FromResult(new int32_InOut() { Value = request.Value + 1 });
    public override Task<int64_InOut> Request_int64(int64_InOut request, ServerCallContext context) => Task.FromResult(new int64_InOut() { Value = request.Value + 1 });
    public override Task<uint32_InOut> Request_uint32(uint32_InOut request, ServerCallContext context) => Task.FromResult(new uint32_InOut() { Value = request.Value + 1 });
    public override Task<uint64_InOut> Request_uint64(uint64_InOut request, ServerCallContext context) => Task.FromResult(new uint64_InOut() { Value = request.Value + 1 });
    public override Task<sint32_InOut> Request_sint32(sint32_InOut request, ServerCallContext context) => Task.FromResult(new sint32_InOut() { Value = request.Value + 1 });
    public override Task<sint64_InOut> Request_sint64(sint64_InOut request, ServerCallContext context) => Task.FromResult(new sint64_InOut() { Value = request.Value + 1 });
    public override Task<fixed32_InOut> Request_fixed32(fixed32_InOut request, ServerCallContext context) => Task.FromResult(new fixed32_InOut() { Value = request.Value + 1 });
    public override Task<fixed64_InOut> Request_fixed64(fixed64_InOut request, ServerCallContext context) => Task.FromResult(new fixed64_InOut() { Value = request.Value + 1 });
    public override Task<sfixed32_InOut> Request_sfixed32(sfixed32_InOut request, ServerCallContext context) => Task.FromResult(new sfixed32_InOut() { Value = request.Value + 1 });
    public override Task<sfixed64_InOut> Request_sfixed64(sfixed64_InOut request, ServerCallContext context) => Task.FromResult(new sfixed64_InOut() { Value = request.Value + 1 });
    public override Task<bool_InOut> Request_bool(bool_InOut request, ServerCallContext context)
    {
        return Task.FromResult(new bool_InOut() { Value = !request.Value });
    }

    public override Task<string_InOut> Request_string(string_InOut request, ServerCallContext context) => Task.FromResult(new string_InOut() { Value = request.Value + 1 });
    public override Task<bytes_InOut> Request_bytes(bytes_InOut request, ServerCallContext context) => Task.FromResult(new bytes_InOut() { Value = request.Value });

    public override Task<enum_type_InOut> Request_enum_type(enum_type_InOut request, ServerCallContext context) => Task.FromResult(new enum_type_InOut() { Value = (enum_type)((int)request.Value + 1) });

    public override Task<double_repeated_InOut> Request_with_repeated_double(double_repeated_InOut request, ServerCallContext context)
    {
        var values = request.Values;
        for (int i = 0; i < values.Count; i++)
        {
            values[i]++;
        }
        return Task.FromResult(request);
    }

    public override Task<float_repeated_InOut> Request_with_repeated_float(float_repeated_InOut request, ServerCallContext context)
    {
        var values = request.Values;
        for (int i = 0; i < values.Count; i++)
        {
            values[i]++;
        }
        return Task.FromResult(request);
    }

    public override Task<int32_repeated_InOut> Request_with_repeated_int32(int32_repeated_InOut request, ServerCallContext context)
    {
        var values = request.Values;
        for (int i = 0; i < values.Count; i++)
        {
            values[i]++;
        }
        return Task.FromResult(request);
    }

    public override Task<int64_repeated_InOut> Request_with_repeated_int64(int64_repeated_InOut request, ServerCallContext context)
    {
        var values = request.Values;
        for (int i = 0; i < values.Count; i++)
        {
            values[i]++;
        }
        return Task.FromResult(request);
    }

    public override Task<uint32_repeated_InOut> Request_with_repeated_uint32(uint32_repeated_InOut request, ServerCallContext context)
    {
        var values = request.Values;
        for (int i = 0; i < values.Count; i++)
        {
            values[i]++;
        }
        return Task.FromResult(request);
    }

    public override Task<uint64_repeated_InOut> Request_with_repeated_uint64(uint64_repeated_InOut request, ServerCallContext context)
    {
        var values = request.Values;
        for (int i = 0; i < values.Count; i++)
        {
            values[i]++;
        }
        return Task.FromResult(request);
    }

    public override Task<sint32_repeated_InOut> Request_with_repeated_sint32(sint32_repeated_InOut request, ServerCallContext context)
    {
        var values = request.Values;
        for (int i = 0; i < values.Count; i++)
        {
            values[i]++;
        }
        return Task.FromResult(request);
    }

    public override Task<sint64_repeated_InOut> Request_with_repeated_sint64(sint64_repeated_InOut request, ServerCallContext context)
    {
        var values = request.Values;
        for (int i = 0; i < values.Count; i++)
        {
            values[i]++;
        }
        return Task.FromResult(request);
    }

    public override Task<fixed32_repeated_InOut> Request_with_repeated_fixed32(fixed32_repeated_InOut request, ServerCallContext context)
    {
        var values = request.Values;
        for (int i = 0; i < values.Count; i++)
        {
            values[i]++;
        }
        return Task.FromResult(request);
    }

    public override Task<fixed64_repeated_InOut> Request_with_repeated_fixed64(fixed64_repeated_InOut request, ServerCallContext context)
    {
        var values = request.Values;
        for (int i = 0; i < values.Count; i++)
        {
            values[i]++;
        }
        return Task.FromResult(request);
    }

    public override Task<sfixed32_repeated_InOut> Request_with_repeated_sfixed32(sfixed32_repeated_InOut request, ServerCallContext context)
    {
        var values = request.Values;
        for (int i = 0; i < values.Count; i++)
        {
            values[i]++;
        }
        return Task.FromResult(request);
    }

    public override Task<sfixed64_repeated_InOut> Request_with_repeated_sfixed64(sfixed64_repeated_InOut request, ServerCallContext context)
    {
        var values = request.Values;
        for (int i = 0; i < values.Count; i++)
        {
            values[i]++;
        }
        return Task.FromResult(request);
    }

    public override Task<bool_repeated_InOut> Request_with_repeated_bool(bool_repeated_InOut request, ServerCallContext context)
    {
        var values = request.Values;
        for (int i = 0; i < values.Count; i++)
        {
            values[i] = !values[i];
        }
        return Task.FromResult(request);
    }

    public override Task<string_repeated_InOut> Request_with_repeated_string(string_repeated_InOut request, ServerCallContext context)
    {
        var values = request.Values;
        for (int i = 0; i < values.Count; i++)
        {
            values[i] = values[i] + 1;
        }
        return Task.FromResult(request);
    }


    public override Task<bytes_repeated_InOut> Request_with_repeated_bytes(bytes_repeated_InOut request, ServerCallContext context)
    {
        var values = request.Values;
        return Task.FromResult(request);
    }

    public override Task<enum_type_repeated_InOut> Request_with_repeated_enum_type(enum_type_repeated_InOut request, ServerCallContext context)
    {
        var values = request.Values;
        for (int i = 0; i < values.Count; i++)
        {
            values[i]++;
        }
        return Task.FromResult(request);
    }

    public override Task<map_type_InOut> Request_map_type(map_type_InOut request, ServerCallContext context)
    {
        request.Value.MapKeyInt32Values.Add(10, "test10");
        request.Value.MapKeyInt64Values.Add(10, "test10");
        request.Value.MapKeyUint32Values.Add(10, "test10");
        request.Value.MapKeyUint64Values.Add(10, "test10");
        request.Value.MapKeySint32Values.Add(10, "test10");
        request.Value.MapKeySint64Values.Add(10, "test10");
        request.Value.MapKeyFixed32Values.Add(10, "test10");
        request.Value.MapKeyFixed64Values.Add(10, "test10");
        request.Value.MapKeySfixed32Values.Add(10, "test10");
        request.Value.MapKeySfixed64Values.Add(10, "test10");
        request.Value.MapKeyBoolValues.Add(false, "test10");
        request.Value.MapKeyStringValues.Add("hello10", "test10");
        return Task.FromResult(request);
    }

    public override Task<map_type_repeated_InOut> Request_with_repeated_map_type(map_type_repeated_InOut request, ServerCallContext context)
    {
        throw new NotImplementedException();
    }

    public override Task<any_type_InOut> Request_any_type(any_type_InOut request, ServerCallContext context)
    {
        if (request.Value.Instrument.Is(Currency.Descriptor))
        {
            var message = request.Value.Instrument.Unpack<Currency>().CurrencyMessage;
            request.Value.Instrument = Google.Protobuf.WellKnownTypes.Any.Pack(new Stock() { StockMessage = $"From currency: {message}" });
        }
        else if (request.Value.Instrument.Is(Stock.Descriptor))
        {
            var message = request.Value.Instrument.Unpack<Stock>().StockMessage;
            request.Value.Instrument = Google.Protobuf.WellKnownTypes.Any.Pack(new Currency() { CurrencyMessage = $"From stock: {message}" });
        }

        return Task.FromResult(request);
    }

    public override Task<any_type_repeated_InOut> Request_with_repeated_any_type(any_type_repeated_InOut request, ServerCallContext context)
    {
        throw new NotImplementedException();
    }
}