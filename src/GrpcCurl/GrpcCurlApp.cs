using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using DynamicGrpc;
using Grpc.Net.Client;
namespace GrpcCurl;

public partial class ProgramArguments { }

public class GrpcCurlApp
{
    public static Task<int> Run(string[] args)
    {
        return ProgramArguments.CreateParser()
                               .WithVersion(ThisAssembly.Info.InformationalVersion)
                               .Parse(args)
                               .Match(Run,
                                      result => Print(Console.Out, FormatHelp(result.Help)),
                                      result => Print(Console.Out, result.Version),
                                      result => Print(Console.Error, FormatHelp(result.Usage), exitCode: 1));

        static string FormatHelp(string text) =>
            text.Replace("{version}", ThisAssembly.Info.InformationalVersion);

        static Task<int> Print(TextWriter writer, string message, int exitCode = 0)
        {
            writer.WriteLine(message);
            return Task.FromResult(exitCode);
        }
    }

    static async Task<int> Run(ProgramArguments args)
    {
        var options = new GrpcCurlOptions();

        int result;
        try
        {
            options.Address = args.ArgAddressColonPort!;
            options.ForceHttp = args.OptHttp;
            options.UseJsonNaming = args.OptJson;
            options.Describe = args.CmdDescribe;
            options.Data = args.OptData is { } data ? ParseJson(data) : null;

            if ((args.CmdDescribe ? args.ArgService : args.ArgServiceSlashMethod) is { } serviceMethod)
            {
                var indexOfSlash = serviceMethod.IndexOf('/');
                if (!options.Describe && indexOfSlash < 0) throw new GrpcCurlException("Invalid symbol. The symbol must contain a slash (/) to separate the service from the method (serviceName/methodName)");

                options.Service = indexOfSlash < 0 ? serviceMethod : serviceMethod.Substring(0, indexOfSlash);
                options.Method = indexOfSlash < 0 ? null : serviceMethod.Substring(indexOfSlash + 1);
            }

            result = await Run(options);
        }
        catch (Exception exception)
        {
            await WriteLineError($"Unexpected error {exception}");
            result = 1;
        }

        return result;
    }

    private static async Task WriteLineError(string text)
    {
        var backColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        await Console.Error.WriteLineAsync(text);
        Console.ForegroundColor = backColor;
    }

    public static async Task<int> Run(GrpcCurlOptions options)
    {
        var httpAddress = options.Address.StartsWith("http") ? options.Address : $"{(options.ForceHttp?"http":"https")}://{options.Address}";
        var channel = GrpcChannel.ForAddress(httpAddress);

        var client = await DynamicGrpcClient.FromServerReflection(channel, new DynamicGrpcClientOptions()
        {
            UseJsonNaming = options.UseJsonNaming
        });

        // Describe
        if (options.Describe)
        {
            if (options.Service is null)
            {
                foreach (var file in client.Files)
                {
                    file.ToProtoString(options.Writer, new DynamicGrpcPrinterOptions() { AddMetaComments = true });
                    await options.Writer.WriteLineAsync();
                }
            }
            else
            {
                foreach (var file in client.Files)
                {
                    var service = file.Services.FirstOrDefault(x => x.FullName == options.Service);
                    if (service is not null)
                    {
                        service.ToProtoString(options.Writer, new DynamicGrpcPrinterOptions() { AddMetaComments = true });
                        await options.Writer.WriteLineAsync();
                    }
                }
            }

            return 0;
        }

        // Parse input from stdin if data was not passed by command line
        var data = options.Data;
        if (data is null)
        {
            if (Console.IsInputRedirected)
            {
                data = ParseJson(await Console.In.ReadToEndAsync());
            }
        }
        data ??= new Dictionary<string, object>();


        Debug.Assert(options.Service is not null);
        Debug.Assert(options.Method is not null);
        if (!client.TryFindMethod(options.Service!, options.Method!, out var methodDescriptor))
        {
            throw new GrpcCurlException($"Unable to find the method `{options.Service}/{options.Method}`");
        }

        // Parse Input
        var input = new List<IDictionary<string, object>>();

        if (data is IEnumerable<object> it)
        {
            int index = 0;
            foreach (var item in it)
            {

                if (item is IDictionary<string, object> dict)
                {
                    input.Add(dict);
                }
                else
                {
                    throw new GrpcCurlException($"Invalid type `{item?.GetType()?.FullName}` from the input array at index [{index}]. Expecting an object.");
                }
                index++;
            }
        }
        else if (data is IDictionary<string, object> dict)
        {
            input.Add(dict);
        }
        else
        {
            throw new GrpcCurlException($"Invalid type `{data?.GetType()?.FullName}` from the input. Expecting an object.");
        }

        // Perform the async call
        await foreach (var result in client.AsyncDynamicCall(options.Service, options.Method, ToAsync(input)))
        {
            OutputResult(options.Writer, result);
        }
        return 0;
    }

    private static async IAsyncEnumerable<IDictionary<string, object>> ToAsync(IEnumerable<IDictionary<string, object>> input)
    {
        foreach (var item in input)
        {
            yield return await ValueTask.FromResult(item);
        }
    }

    private static void OutputResult(TextWriter output, IDictionary<string, object> result)
    {
        // Serialize the result back to the output
        var json = ToJson(result)!;
        var stream = new MemoryStream();
        var utf8Writer = new Utf8JsonWriter(stream, new JsonWriterOptions()
        {
            SkipValidation = true,
            Indented = true
        });
        json.WriteTo(utf8Writer, new JsonSerializerOptions()
        {
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            WriteIndented = true
        });
        utf8Writer.Flush();
        var jsonString = Encoding.UTF8.GetString(stream.ToArray());
        output.WriteLine(jsonString);
    }


    private static object? ParseJson(string data)
    {
        try
        {
            var json = JsonNode.Parse(data);
            return ToApiRequest(json);
        }
        catch (Exception ex)
        {
            throw new GrpcCurlException($"Failing to deserialize JSON data. Reason: {ex.Message}.");
        }
    }

    private static object? ToApiRequest(JsonNode? requestObject)
    {
        switch (requestObject)
        {
            case JsonObject jObject: // objects become Dictionary<string,object>
                return jObject.ToDictionary(j => j.Key, j => ToApiRequest(j.Value));
            case JsonArray jArray: // arrays become List<object>
                return jArray.Select(ToApiRequest).ToList();
            case JsonValue jValue: // values just become the value
                return jValue.GetValue<object>();
            case null:
                return null;
            default: // don't know what to do here
                throw new Exception($"Unsupported type: {requestObject.GetType()}");
        }
    }

    private static JsonNode? ToJson(object? requestObject)
    {
        switch (requestObject)
        {
            case int i32: return JsonValue.Create(i32);
            case uint u32: return JsonValue.Create(u32);
            case long i64: return JsonValue.Create(i64);
            case ulong u64: return JsonValue.Create(u64);
            case float f32: return JsonValue.Create(f32);
            case double f64: return JsonValue.Create(f64);
            case short i16: return JsonValue.Create(i16);
            case ushort u16: return JsonValue.Create(u16);
            case sbyte i8: return JsonValue.Create(i8);
            case byte u8: return JsonValue.Create(u8);
            case bool b: return JsonValue.Create(b);
            case string str:
                return JsonValue.Create(str);
            case IDictionary<string, object> obj: // objects become Dictionary<string,object>
                var jsonObject = new JsonObject();
                foreach (var kp in obj)
                {
                    jsonObject.Add(kp.Key, ToJson(kp.Value));
                }
                return jsonObject;
            case IEnumerable array: // arrays become List<object>
                var jsonArray = new JsonArray();
                foreach (var o in array)
                {
                    jsonArray.Add(ToJson(o));
                }

                return jsonArray;
            default: // don't know what to do here
                return null;
        }
    }

    private class GrpcCurlException : Exception
    {
        public GrpcCurlException(string? message) : base(message)
        {
        }

        public string? AdditionalText { get; set; }
    }
}