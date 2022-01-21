namespace GrpcCurl;

public class GrpcCurlOptions
{
    public GrpcCurlOptions()
    {
        Address = string.Empty;
        Service = string.Empty;
        Method = string.Empty;
        Writer = Console.Out;
    }

    public string Address { get; set; }

    public string Service { get; set; }

    public string Method { get; set; }

    public bool UseJsonNaming { get; set; }

    public bool ForceHttp { get; set; }

    public object? Data { get; set; }

    public bool Verbose { get; set; }

    public TextWriter Writer { get; set; }
}