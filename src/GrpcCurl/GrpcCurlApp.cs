using System.Diagnostics;
using System.Reflection;
using DynamicGrpc;
using Grpc.Core;
using Grpc.Net.Client;
using Mono.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GrpcCurl;

public class GrpcCurlApp
{
    public static async Task<int> Run(string[] args)
    {
        var exeName = Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly()?.Location)?.ToLowerInvariant();
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
            $"Usage: {exeName} [options] host:port service/method",
            _,
            "## Options",
            _,
            { "d=", "Data for string content.", v => options.Data = ParseJson(v) },
            { "json", "Use JSON naming for input and output.", v => options.UseJsonNaming = true },
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

            if (arguments.Count != 2)
            {
                throw new GrpcCurlException("Expecting a path to a solution file");
            }

            options.Address = arguments[0];
            var serviceMethod = arguments[1];

            var indexOfSlash = serviceMethod.IndexOf('/');
            if (indexOfSlash < 0) throw new GrpcCurlException("Invalid symbol. The symbol must contain a slash (/) to separate the service from the method (serviceName/methodName)");

            options.Service = serviceMethod.Substring(0, indexOfSlash);
            options.Method = serviceMethod.Substring(indexOfSlash + 1);

            return await RunInternal(options);
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

    private static async Task<int> RunInternal(GrpcCurlOptions options)
    {
        var httpAddress = options.Address.StartsWith("http") ? options.Address : $"http://{options.Address}";
        var channel = GrpcChannel.ForAddress(httpAddress);

        var client = await DynamicGrpcClient.FromServerReflection(channel, new DynamicGrpcClientOptions()
        {
            UseJsonNaming = options.UseJsonNaming
        });

        Debug.Assert(options.Data != null);


        if (!client.TryFindMethod(options.Service, options.Method, out var methodDescriptor))
        {
            throw new GrpcCurlException($"Unable to find the method `{options.Service}/{options.Method}`");
        }

        // Parse Input
        var input = new List<IDictionary<string, object>>();

        if (options.Data is IEnumerable<object> it)
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
        else if (options.Data is IDictionary<string, object> dict)
        {
            input.Add(dict);
        }
        else
        {
            throw new GrpcCurlException($"Invalid type `{options.Data?.GetType()?.FullName}` from the input. Expecting an object.");
        }

        // Perform the async call
        await foreach (var result in client.AsyncDynamicCall(options.Service, options.Method, ToAsync(input)))
        {
            WriteResultToConsole(result);
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

    private static void WriteResultToConsole(IDictionary<string, object> result)
    {
        // Serialize the result back to the output
        var serializer = new JsonSerializer();
        var strWriter = new StringWriter();
        var writer = new JsonTextWriter(strWriter)
        {
            Formatting = Formatting.Indented
        };
        serializer.Serialize(writer, result);
        Console.WriteLine(strWriter.ToString());
    }


    private static object? ParseJson(string data)
    {
        try
        {
            return ToApiRequest(JsonConvert.DeserializeObject(data));
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

    private static object? ToApiRequest(object? requestObject)
    {
        switch (requestObject)
        {
            case JObject jObject: // objects become Dictionary<string,object>
                return ((IEnumerable<KeyValuePair<string, JToken>>)jObject).ToDictionary(j => j.Key, j => ToApiRequest(j.Value));
            case JArray jArray: // arrays become List<object>
                return jArray.Select(ToApiRequest).ToList();
            case JValue jValue: // values just become the value
                return jValue.Value;
            case null:
                return null;
            default: // don't know what to do here
                throw new Exception($"Unsupported type: {requestObject.GetType()}");
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