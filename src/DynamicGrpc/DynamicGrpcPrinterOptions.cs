namespace DynamicGrpc;

public class DynamicGrpcPrinterOptions
{
    internal static readonly DynamicGrpcPrinterOptions Default = new DynamicGrpcPrinterOptions();
    
    public DynamicGrpcPrinterOptions()
    {
        Indent = "  ";
    }

    public bool AddMetaComments { get; set; }
    
    public string Indent { get; set; }
}