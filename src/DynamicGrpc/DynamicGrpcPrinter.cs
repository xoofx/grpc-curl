using System.Text;
using Google.Protobuf.Reflection;

namespace DynamicGrpc;

public static class DynamicGrpcPrinter
{
    public static void ToProtoString(this FileDescriptor file, TextWriter writer, DynamicGrpcPrinterOptions? options = null)
    {
        ToProtoString(file, 0, writer, options);
    }

    public static string ToProtoString(this FileDescriptor file, DynamicGrpcPrinterOptions? options = null)
    {
        var writer = new StringWriter();
        ToProtoString(file, writer, options);
        return writer.ToString();
    }

    public static void ToProtoString(this ServiceDescriptor service, TextWriter writer, DynamicGrpcPrinterOptions? options = null)
    {
        ToProtoString(service, 0, writer, options);
    }

    public static string ToProtoString(this ServiceDescriptor service, DynamicGrpcPrinterOptions? options = null)
    {
        var writer = new StringWriter();
        ToProtoString(service, writer, options);
        return writer.ToString();
    }

    public static void ToProtoString(this MessageDescriptor message, TextWriter writer, DynamicGrpcPrinterOptions? options = null)
    {
        ToProtoString(message, 0, writer, options);
    }

    public static string ToProtoString(this MessageDescriptor message, DynamicGrpcPrinterOptions? options = null)
    {
        var writer = new StringWriter();
        ToProtoString(message, writer, options);
        return writer.ToString();
    }

    public static void ToProtoString(this EnumDescriptor enumDesc, TextWriter writer, DynamicGrpcPrinterOptions? options = null)
    {
        ToProtoString(enumDesc, 0, writer, options);
    }

    public static string ToProtoString(this EnumDescriptor enumDesc, DynamicGrpcPrinterOptions? options = null)
    {
        var writer = new StringWriter();
        ToProtoString(enumDesc, writer, options);
        return writer.ToString();
    }

    private static void ToProtoString(this FileDescriptor file, int level, TextWriter writer, DynamicGrpcPrinterOptions? options = null)
    {
        options ??= DynamicGrpcPrinterOptions.Default;

        if (options.AddMetaComments)
        {
            WriteLine(writer, $"// {file.Name} is a proto file.", options.Indent, level);
        }

        bool newNewLine = false;
        // Write syntax
        switch (file.Syntax)
        {
            case Syntax.Proto2:
                WriteLine(writer, "syntax = \"proto2\";", options.Indent, level);
                newNewLine = true;
                break;
            case Syntax.Proto3:
                WriteLine(writer, "syntax = \"proto3\";", options.Indent, level);
                newNewLine = true;
                break;
        }

        // Dump package
        if (newNewLine) writer.WriteLine();
        newNewLine = false;
        if (!string.IsNullOrWhiteSpace(file.Package))
        {
            WriteLine(writer, $"package {file.Package};", options.Indent, level);
            newNewLine = true;
        }

        // Dump imports
        if (newNewLine) writer.WriteLine();
        newNewLine = false;
        foreach (var import in file.Dependencies)
        {
            WriteLine(writer, $"import \"{import.Name}\"", options.Indent, level);
            newNewLine = true;
        }

        // Dump services
        if (newNewLine) writer.WriteLine();
        newNewLine = false;
        foreach (var serviceDescriptor in file.Services)
        {
            ToProtoString(serviceDescriptor, level, writer, options);
            writer.WriteLine();
        }

        // Dump message types
        foreach (var messageDescriptor in file.MessageTypes)
        {
            ToProtoString(messageDescriptor, level, writer, options);
            writer.WriteLine();
        }

        // Dump message types
        foreach (var enumDescriptor in file.EnumTypes)
        {
            ToProtoString(enumDescriptor, level, writer, options);
            writer.WriteLine();
        }
    }

    private static void ToProtoString(this ServiceDescriptor service, int level, TextWriter writer, DynamicGrpcPrinterOptions? options = null)
    {
        options ??= DynamicGrpcPrinterOptions.Default;
        if (options.AddMetaComments)
        {
            WriteLine(writer, $"// {service.FullName} is a service:", options.Indent, level);
        }
        WriteLine(writer, $"service {service.Name} {{", options.Indent, level);
        level++;
        foreach (var method in service.Methods)
        {
            WriteLine(writer, $"{options.Indent}rpc {method.Name} ( .{method.InputType.FullName} ) returns ( .{method.OutputType.FullName} );", options.Indent, level);
        }
        level--;
        WriteLine(writer, "}", options.Indent, level);
    }

    private static void ToProtoString(this MessageDescriptor message, int level, TextWriter writer, DynamicGrpcPrinterOptions? options = null)
    {
        options ??= DynamicGrpcPrinterOptions.Default;
        if (options.AddMetaComments)
        {
            WriteLine(writer, $"// {message.FullName} is a message:", options.Indent, level);
        }
        WriteLine(writer, $"message {message.Name} {{", options.Indent, level);
        level++;
        bool needNewLine = false;
        foreach (var field in message.Fields.InDeclarationOrder())
        {
            WriteLine(writer, $"{options.Indent}{GetTypeName(field)} {field.Name} = {field.FieldNumber};", options.Indent, level);
            needNewLine = true;
        }

        if (message.NestedTypes.Count > 0)
        {
            if (needNewLine) writer.WriteLine();
            needNewLine = false;
            foreach (var nestedMessageType in message.NestedTypes)
            {
                ToProtoString(nestedMessageType, level, writer, options);
                writer.WriteLine();
            }
        }

        if (message.EnumTypes.Count > 0)
        {
            if (needNewLine) writer.WriteLine();
            needNewLine = false;
            foreach (var enumDescriptor in message.EnumTypes)
            {
                ToProtoString(enumDescriptor, level, writer, options);
            }
        }

        level--;
        WriteLine(writer, "}", options.Indent, level);
    }

    private static void ToProtoString(this EnumDescriptor enumDescriptor, int level, TextWriter writer, DynamicGrpcPrinterOptions? options = null)
    {
        options ??= DynamicGrpcPrinterOptions.Default;
        if (options.AddMetaComments)
        {
            WriteLine(writer, $"// {enumDescriptor.FullName} is an enum:", options.Indent, level);
        }
        WriteLine(writer, $"enum {enumDescriptor.Name} {{", options.Indent, level);
        level++;
        foreach (var item in enumDescriptor.Values)
        {
            WriteLine(writer, $"{item.Name} = {item.Number};", options.Indent, level);
        }
        level--;
        WriteLine(writer, "}", options.Indent, level);
    }

    private static void WriteLine(TextWriter writer, string text, string indent, int level)
    {
        WriteIndent(writer, indent, level);
        writer.WriteLine(text);
    }

    private static void WriteIndent(TextWriter writer, string indent, int level)
    {
        for (int i = 0; i < level; i++)
        {
            writer.Write(indent);
        }
    }
    
    private static string GetTypeName(FieldDescriptor field)
    {
        if (field.IsMap)
        {
            var subFields = field.MessageType.Fields.InFieldNumberOrder();
            return $"map<{GetTypeName(subFields[0])}, {GetTypeName(subFields[1])}>";
        }

        var builder = new StringBuilder();
        if (field.IsRequired) builder.Append("required ");
        var options = field.GetOptions();
        if (options == null)
        {
            if (field.File.Syntax == Syntax.Proto3)
            {
                if (field.IsRepeated) builder.Append("repeated ");
            }
        }
        else
        {
            if (field.File.Syntax != Syntax.Proto3 && field.IsPacked) builder.Append("packed ");
            if (field.IsRepeated) builder.Append("repeated ");
        }

        switch (field.FieldType)
        {
            case FieldType.Double:
                builder.Append("double");
                break;
            case FieldType.Float:
                builder.Append( "float");
                break;
            case FieldType.Int64:
                builder.Append( "int64");
                break;
            case FieldType.UInt64:
                builder.Append( "uint64");
                break;
            case FieldType.Int32:
                builder.Append( "int32");
                break;
            case FieldType.Fixed64:
                builder.Append( "fixed64");
                break;
            case FieldType.Fixed32:
                builder.Append( "fixed32");
                break;
            case FieldType.Bool:
                builder.Append( "bool");
                break;
            case FieldType.String:
                builder.Append( "string");
                break;
            case FieldType.Group:
                break;
            case FieldType.Message:
                builder.Append( $".{field.MessageType.FullName}");
                break;
            case FieldType.Bytes:
                builder.Append( "bytes");
                break;
            case FieldType.UInt32:
                builder.Append( "uint32");
                break;
            case FieldType.SFixed32:
                builder.Append( "sfixed32");
                break;
            case FieldType.SFixed64:
                builder.Append( "sfixed64");
                break;
            case FieldType.SInt32:
                builder.Append( "sint32");
                break;
            case FieldType.SInt64:
                builder.Append( "sint64");
                break;
            case FieldType.Enum:
                builder.Append( $".{field.EnumType.FullName}");
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return builder.ToString();
    }
}