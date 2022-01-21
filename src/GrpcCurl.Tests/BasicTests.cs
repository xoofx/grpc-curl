using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using DynamicGrpc;
using Grpc.Net.Client;
using GrpcCurl.Tests.Proto;
using NUnit.Framework;

namespace GrpcCurl.Tests
{
    public class BasicTests : GrpcTestBase
    {
        [Test]
        [Ignore("Local only")]
        public async Task TestStarlink()
        {
            using var channel = GrpcChannel.ForAddress("http://192.168.100.1:9200");
            var client = await DynamicGrpcClient.FromServerReflection(channel);

            var result = await client.AsyncUnaryCall("SpaceX.API.Device.Device", "Handle", new Dictionary<string, object>()
            {
                { "get_status", new Dictionary<string, object>() }
            });

            var text= JsonSerializer.Serialize(result, new JsonSerializerOptions() { WriteIndented = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals});
            Console.WriteLine(text);
        }
        
        [Test]
        public async Task TestBasicUnaryCall()
        {
            var client = await DynamicGrpcClient.FromServerReflection(TestGrpcChannel);

            var result = await client.AsyncUnaryCall("greet.Greeter", "SayHello", new Dictionary<string, object>()
            {
                { "name", "Hello GrpcCurl!" }
            });

            Assert.That(result, Contains.Key("message"));
            Assert.AreEqual("Hello from server with input name: Hello GrpcCurl!.", result["message"]);

            dynamic dynamicResult = result;

            Assert.AreEqual(result["message"], dynamicResult.message);
        }

        [Test]
        [Ignore("Group is not correctly mapped by server reflection. Instead it is backed into Message.")]
        public async Task TestGroup()
        {
            // TODO: log an issue on server reflection
            var client = await DynamicGrpcClient.FromServerReflection(TestGrpcChannel);

            var input = new Dictionary<string, object>()
            {
                { "url", "http://localhost" },
                { "title", "This is a title" },
                { "snippets", "This is a snippet" }
            };
            var result = await client.AsyncUnaryCall("greet.GreeterProto2", "SayGroup", new Dictionary<string, object>()
            {
                { "result", input }
            });

            Assert.That(result, Contains.Key("result"));
            dynamic dynamicResult = result;
            Assert.AreEqual($"{input["url"]} - yes", dynamicResult.result.url);
            Assert.AreEqual($"{input["title"]} - yes", dynamicResult.result.title);
            Assert.AreEqual($"{input["snippets"]} - yes", dynamicResult.result.sessions);
        }

        [Test]
        public async Task TestServerStreamingCall()
        {
            var client = await DynamicGrpcClient.FromServerReflection(TestGrpcChannel);

            var call = client.AsyncServerStreamingCall("greet.Greeter", "SayHellos", new Dictionary<string, object>()
            {
                { "name", "Hello GrpcCurl!" }
            });

            var results = new List<IDictionary<string, object>>();
            while (await call.ResponseStream.MoveNext(CancellationToken.None))
            {
                results.Add(call.ResponseStream.Current);
            }

            Assert.AreEqual(GreeterServiceImpl.StreamingCount, results.Count);

            for(int i = 0; i < results.Count; i++)
            {
                var result = results[i];

                Assert.That(result, Contains.Key("message"));
                Assert.AreEqual($"Streaming Hello {i}/{GreeterServiceImpl.StreamingCount} from server with input name: Hello GrpcCurl!.", result["message"]);

                dynamic dynamicResult = result;

                Assert.AreEqual(result["message"], dynamicResult.message);
            }
        }

        [TestCase("double", 1.0, 2.0)]
        [TestCase("float", 1.0f, 2.0f)]
        [TestCase("int32", -1, 0)] // testing default value
        [TestCase("int32", 1, 2)]
        [TestCase("int64", 1L, 2L)]
        [TestCase("uint32", 1U, 2U)]
        [TestCase("uint64", 1UL, 2UL)]
        [TestCase("sint32", 3, 4)]
        [TestCase("sint64", 5L, 6L)]
        [TestCase("fixed32", 3U, 4U)]
        [TestCase("fixed64", 5UL, 6UL)]
        [TestCase("sfixed32", 1, 2)]
        [TestCase("sfixed64", 1L, 2L)]
        [TestCase("bool", true, false)]
        [TestCase("string", "hello", "hello1")]
        [TestCase("bytes", null, null)]
        [TestCase("enum_type", 0, "WEB")]
        [TestCase("enum_type", -1, "UNIVERSAL")] // testing default value
        [TestCase("enum_type", "IMAGES", "LOCAL")]
        public async Task TestPrimitives(string name, object input, object expectedOutput)
        {
            var client = await DynamicGrpcClient.FromServerReflection(TestGrpcChannel);

            if (name == "bytes")
            {
                input = new byte[] { 1, 2, 3 };
                expectedOutput = input;
            }

            // Request with a simple field
            var result = await client.AsyncUnaryCall("Primitives.PrimitiveService", $"Request_{name}", new Dictionary<string, object>()
            {
                { "value", input }
            });
            ValidateResult(name, expectedOutput, result);

            // Request with repeated fields
            var inputList = new List<object>
            {
                input,
                input,
                input,
                input
            };
            result = await client.AsyncUnaryCall("Primitives.PrimitiveService", $"Request_with_repeated_{name}", new Dictionary<string, object>()
            {
                { "values", inputList }
            });
            Assert.AreEqual(1, result.Count);
            Assert.That(result, Contains.Key("values"));
            Assert.IsInstanceOf<IEnumerable>(result["values"]);
            int count = 0;
            foreach (var itemResult in (IEnumerable)result["values"])
            {
                Assert.AreEqual(expectedOutput, itemResult);
                count++;
            }
            Assert.AreEqual(inputList.Count, count);

            static void ValidateResult(string name, object expectedOutput, IDictionary<string, object> result)
            {
                Assert.That(result, Contains.Key("value"));
                var output = result["value"];
                Assert.AreEqual(expectedOutput, output);
            }
        }


        [TestCase("int32", 1)]
        [TestCase("int64", 1L)]
        [TestCase("uint32", 1U)]
        [TestCase("uint64", 1UL)]
        [TestCase("sint32", 3)]
        [TestCase("sint64", 5L)]
        [TestCase("fixed32", 3U)]
        [TestCase("fixed64", 5UL)]
        [TestCase("sfixed32", 1)]
        [TestCase("sfixed64", 1L)]
        [TestCase("bool", true)]
        [TestCase("string", "hello")]
        public async Task TestMaps(string name, object input)
        {
            var client = await DynamicGrpcClient.FromServerReflection(TestGrpcChannel);

            var map_key = $"map_key_{name}_values";
            var result = await client.AsyncUnaryCall("Primitives.PrimitiveService", $"Request_map_type", new Dictionary<string, object>()
            {
                { "value", new Dictionary<string, object>()
                    {
                        {map_key, new Dictionary<object, string>()
                            {
                                {input, "test0"} // Creates a default value for the key
                            }
                        }
                    }
                }
            });

            Assert.That(result, Contains.Key("value"));
            var subValue = result["value"];
            Assert.IsInstanceOf<IDictionary<string, object>>(subValue);
            var dict = (IDictionary<string, object>)subValue;
            Assert.That(dict, Contains.Key(map_key));
            subValue = dict[map_key];
            Assert.IsInstanceOf<IDictionary<object, object>>(subValue);
            var subDict = (IDictionary<object, object>)subValue;
            Assert.AreEqual(2, subDict.Count);
            Assert.That(subDict, Contains.Key(input));
            Assert.That(subDict, Contains.Value("test0"));
            Assert.That(subDict, Contains.Value("test10"));
        }

        [Test]
        public async Task TestAny()
        {
            var client = await DynamicGrpcClient.FromServerReflection(TestGrpcChannel);

            var result = await client.AsyncUnaryCall("Primitives.PrimitiveService", $"Request_any_type", new Dictionary<string, object>()
            {
                { "value", new Dictionary<string, object>()
                    {
                        {"instrument", new Dictionary<string, object>()
                            {
                                {"currency_message", "this is a currency"} 
                            }.WithAny("Primitives.Currency")
                        }
                    }
                }
            });

            Assert.That(result, Contains.Key("value"));
            var subValue = result["value"];
            Assert.IsInstanceOf<IDictionary<string, object>>(subValue);
            var dict = (IDictionary<string, object>)subValue;
            Assert.That(dict, Contains.Key("instrument"));
            subValue = dict["instrument"];
            Assert.IsInstanceOf<IDictionary<string, object>>(subValue);
            var subDict = (IDictionary<string, object>)subValue;
            Assert.AreEqual(2, subDict.Count, $"Invalid number of key/values. {string.Join(", ", subDict.Keys)}");
            Assert.That(subDict, Contains.Key("stock_message"));
            Assert.That(subDict, Contains.Key("@type"));
            Assert.AreEqual("From currency: this is a currency", subDict["stock_message"]);
            Assert.AreEqual("type.googleapis.com/Primitives.Stock", subDict["@type"]);
        }

        [Test]
        public async Task TestDefaults()
        {
            var client = await DynamicGrpcClient.FromServerReflection(TestGrpcChannel);

            var result = await client.AsyncUnaryCall("Primitives.PrimitiveService", $"Request_defaults_type", new Dictionary<string, object>());

            Assert.That(result, Contains.Key("value"));
            var subValue = result["value"];
            Assert.IsInstanceOf<IDictionary<string, object>>(subValue);
            dynamic dict = subValue;

            Assert.AreEqual(0, dict.field_int32);
            Assert.AreEqual(0L, dict.field_int64);
            Assert.AreEqual(0U, dict.field_uint32);
            Assert.AreEqual(0UL, dict.field_uint64);
            Assert.AreEqual(0, dict.field_sint32);
            Assert.AreEqual(0L, dict.field_sint64);
            Assert.AreEqual(0U, dict.field_fixed32);
            Assert.AreEqual(0UL, dict.field_fixed64);
            Assert.AreEqual(0, dict.field_sfixed32);
            Assert.AreEqual(0L, dict.field_sfixed64);
            Assert.AreEqual(false, dict.field_bool);
            Assert.AreEqual("", dict.field_string);
            Assert.AreEqual(Array.Empty<byte>(), dict.field_bytes);
            Assert.AreEqual("UNIVERSAL", dict.field_enum_type);
        }
    }
}