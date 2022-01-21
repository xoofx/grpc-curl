using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using GrpcCurl.Tests.Proto;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace GrpcCurl.Tests;

public class CurlTests
{
    [Test]
    public async Task Curl()
    {
        const int port = 9874;
        string host = $"http://localhost:{port}";
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions()
        {
            EnvironmentName = Environments.Development,
        });
        builder.Services.AddGrpc();
        builder.Services.AddGrpcReflection();
        builder.WebHost.UseUrls(host);
        builder.WebHost.ConfigureKestrel((context, options) =>
        {
            options.Listen(IPAddress.Loopback, port, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http2;
            });
        });

        await using var app = builder.Build();
        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGrpcService<GreeterServiceImpl>();
            endpoints.MapGrpcReflectionService();
        });

        await app.StartAsync();

        var savedOutput = Console.Out;
        try
        {
            var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);
            var code = await GrpcCurlApp.Run(new string[] { "-d", "{ \"name\": \"Hello grpc-curl!\"}", host, "greet.Greeter/SayHello" });
            Assert.AreEqual(0, code);
            var result = stringWriter.ToString();
            StringAssert.Contains(@"""message"": ""Hello from server with input name: Hello grpc-curl!.""", result);
        }
        finally
        {
            Console.SetOut(savedOutput);
            await app.StopAsync();
        }
    }
}