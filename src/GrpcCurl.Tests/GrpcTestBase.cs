using System;
using System.Threading.Tasks;
using Grpc.Net.Client;
using GrpcCurl.Tests.Proto;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace GrpcCurl.Tests;

public abstract class GrpcTestBase
{
    private WebApplication? _app;
    private GrpcChannel? _testGrpcChannel;

    [OneTimeSetUp]
    public async Task Setup()
    {
        _app = CreateWebApplicationTest();
        await _app.StartAsync();
        var client = _app.GetTestClient();
        _testGrpcChannel = GrpcChannel.ForAddress(client.BaseAddress ?? throw new InvalidOperationException("HttpClient.BaseAddress cannot be null"), new GrpcChannelOptions()
        {
            HttpClient = client
        });
    }

    public GrpcChannel TestGrpcChannel => _testGrpcChannel!;

    [OneTimeTearDown]
    public async Task TearDown()
    {
        if (_testGrpcChannel != null)
        {
            await _testGrpcChannel.ShutdownAsync();
            _testGrpcChannel.Dispose();
        }

        if (_app != null)
        {
            await _app.DisposeAsync();
        }
    }

    private static WebApplication CreateWebApplicationTest()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions()
        {
            EnvironmentName = Environments.Development,
        });
        builder.Services.AddGrpc();
        builder.Services.AddGrpcReflection();
        builder.WebHost.UseTestServer();

        var app = builder.Build();
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGrpcService<GreeterServiceImpl>();
            endpoints.MapGrpcService<GreeterProto2Impl>();
            endpoints.MapGrpcService<PrimitiveServiceImpl>();
            endpoints.MapGrpcReflectionService();
        });

        return app;
    }
}