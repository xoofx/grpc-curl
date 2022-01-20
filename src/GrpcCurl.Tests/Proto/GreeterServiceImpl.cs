using System.Threading.Tasks;
using Greet;
using Grpc.Core;

namespace GrpcCurl.Tests.Proto;

public class GreeterServiceImpl : Greet.Greeter.GreeterBase
{
    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
    {
        return Task.FromResult(new HelloReply() { Message = $"Hello from server with input name: {request.Name}." });
    }

    public const int StreamingCount = 10;

    public override async Task SayHellos(HelloRequest request, IServerStreamWriter<HelloReply> responseStream, ServerCallContext context)
    {
        for (int i = 0; i < StreamingCount; i++)
        {
            await responseStream.WriteAsync(new HelloReply()
            {
                Message = $"Streaming Hello {i}/{StreamingCount} from server with input name: {request.Name}."
            });
        }
    }
}