using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using DynamicGrpc;
using Grpc.Net.Client;
using Mono.Options;
namespace GrpcCurl;

public class GrpcCurlApp
{
    public static async Task<int> Run(string[] args)
    {
        var exeName = "grpc-curl";
        bool showHelp = false;

        var assemblyInfoVersion = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var version = assemblyInfoVersion?.InformationalVersion;
        if (version is null)
        {
            var asmVersion = typeof(Program).Assembly.GetName().Version ?? new Version();
            version = $"{asmVersion.Major}.{asmVersion.Minor}.{asmVersion.Build}";
        }

        var options = new GrpcCurlOptions();

        var _ = string.Empty;
        var optionSet = new OptionSet
        {
            $"Copyright (C) {DateTime.Now.Year} Alexandre Mutel. All Rights Reserved",
            $"{exeName} - Version: {version}",
            _,
            $"Usage: {exeName} [options] address service/method",
            _,
            $"  address: A http/https URL or a simple host:address.",
            $"           If only host:address is used, HTTPS is used by default",
            $"           unless the options --http is passed.",
            _,
            "## Options",
            _,
            { "d|data=", "Data for string content.", v => options.Data = ParseJson(v) },
            { "http", "Use HTTP instead of HTTPS unless the protocol is specified directly on the address.", v => options.ForceHttp = true },
            { "json", "Use JSON naming for input and output.", v => options.UseJsonNaming = true },
            { "describe", "Describe the service or dump all services available.", v => options.Describe = true },
            { "v|verbosity:", "Set verbosity.", v => options.Verbose = true },
            { "h|help", "Show this help.", v => showHelp = true },
            _,
        };

        try
        {
            var arguments = optionSet.Parse(args);

            if (showHelp)
            {
                optionSet.WriteOptionDescriptions(Console.Error);
                return 0;
            }

            if (options.Describe && arguments.Count < 1 && arguments.Count > 2 || !options.Describe && arguments.Count != 2)
            {
                throw new GrpcCurlException(arguments.Count == 0 ? "Missing arguments." :  "Invalid number of arguments.");
            }

            options.Address = arguments[0];
            var serviceMethod = arguments.Count == 2 ? arguments[1] : null;

            if (serviceMethod != null)
            {
                var indexOfSlash = serviceMethod.IndexOf('/');
                if (!options.Describe && indexOfSlash < 0) throw new GrpcCurlException("Invalid symbol. The symbol must contain a slash (/) to separate the service from the method (serviceName/methodName)");

                options.Service = indexOfSlash < 0 ? serviceMethod : serviceMethod.Substring(0, indexOfSlash);
                options.Method = indexOfSlash < 0 ? null : serviceMethod.Substring(indexOfSlash + 1);
            }

            return await Run(options);
        }
        catch (Exception exception)
        {
            var backColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(exception.Message);
            Console.ForegroundColor = backColor;
            if (exception is GrpcCurlException rocketException && rocketException.AdditionalText != null)
            {
                Console.Error.WriteLine(rocketException.AdditionalText);
            }

            Console.Error.WriteLine("See --help for usage");
            return 1;
        }
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

    private static TEnum TryParseEnum<TEnum>(string value, string optionName) where TEnum : struct
    {
        if (!Enum.TryParse<TEnum>(value, true, out var result))
        {
            throw new OptionException($"Invalid value `{value}` for option `{optionName}`. Valid values are: {string.Join(", ", Enum.GetNames(typeof(TEnum)).Select(x => x.ToLowerInvariant()))}", optionName);
        }

        return result;
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