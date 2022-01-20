using System.Threading.Tasks;
using Greet;
using Grpc.Core;

namespace GrpcCurl.Tests.Proto;

public class GreeterProto2Impl : Greet.GreeterProto2.GreeterProto2Base
{
    public override Task<TryGroup> SayGroup(TryGroup request, ServerCallContext context)
    {
        var response = new TryGroup()
        {
            Result = new TryGroup.Types.Result()
            {
                Url = request.Result.Url + " - yes",
                Snippets = request.Result.Snippets + " - yes",
                Title = request.Result.Title + " - yes",
            }
        };
        return Task.FromResult(response);
    }
}