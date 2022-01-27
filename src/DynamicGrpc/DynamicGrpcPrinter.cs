using System.Reflection;
using System.Text;
using Google.Protobuf.Reflection;
using FileOptions = Google.Protobuf.Reflection.FileOptions;

namespace DynamicGrpc;

/// <summary>
/// Extension methods for printing descriptors back to proto language.
/// </summary>
public static class DynamicGrpcPrinter
{
    /// <summary>
    /// Prints the proto description of the specified <see cref="FileDescriptor"/> to a writer.
    /// </summary>
    /// <param name="file">The descriptor to print.</param>
    /// <param name="writer">The text writer.</param>
    /// <param name="options">The printing options.</param>
    public static void ToProtoString(this FileDescriptor file, TextWriter writer, DynamicGrpcPrinterOptions? options = null)
    {
        ToProtoString(file, new DynamicGrpcPrinterContext(writer, options ?? new DynamicGrpcPrinterOptions()));
    }

    /// <summary>
    /// Prints the proto description of the specified <see cref="FileDescriptor"/> to a string.
    /// </summary>
    /// <param name="file">The descriptor to print.</param>
    /// <param name="options">The printing options.</param>
    /// <returns>A proto description of the specified descriptor.</returns>
    public static string ToProtoString(this FileDescriptor file, DynamicGrpcPrinterOptions? options = null)
    {
        var writer = new StringWriter();
        ToProtoString(file, writer, options);
        return writer.ToString();
    }

    /// <summary>
    /// Prints the proto description of the specified <see cref="MessageDescriptor"/> to a writer.
    /// </summary>
    /// <param name="service">The descriptor to print.</param>
    /// <param name="writer">The text writer.</param>
    /// <param name="options">The printing options.</param>
    public static void ToProtoString(this ServiceDescriptor service, TextWriter writer, DynamicGrpcPrinterOptions? options = null)
    {
        ToProtoString(service, new DynamicGrpcPrinterContext(writer, options ?? new DynamicGrpcPrinterOptions()));
    }

    /// <summary>
    /// Prints the proto description of the specified <see cref="MessageDescriptor"/> to a string.
    /// </summary>
    /// <param name="service">The descriptor to print.</param>
    /// <param name="options">The printing options.</param>
    /// <returns>A proto description of the specified descriptor.</returns>
    public static string ToProtoString(this ServiceDescriptor service, DynamicGrpcPrinterOptions? options = null)
    {
        var writer = new StringWriter();
        ToProtoString(service, writer, options);
        return writer.ToString();
    }

    /// <summary>
    /// Prints the proto description of the specified <see cref="MessageDescriptor"/> to a writer.
    /// </summary>
    /// <param name="message">The descriptor to print.</param>
    /// <param name="writer">The text writer.</param>
    /// <param name="options">The printing options.</param>
    public static void ToProtoString(this MessageDescriptor message, TextWriter writer, DynamicGrpcPrinterOptions? options = null)
    {
        ToProtoString(message, new DynamicGrpcPrinterContext(writer, options ?? new DynamicGrpcPrinterOptions()));
    }

    /// <summary>
    /// Prints the proto description of the specified <see cref="MessageDescriptor"/> to a string.
    /// </summary>
    /// <param name="message">The descriptor to print.</param>
    /// <param name="options">The printing options.</param>
    /// <returns>A proto description of the specified descriptor.</returns>
    public static string ToProtoString(this MessageDescriptor message, DynamicGrpcPrinterOptions? options = null)
    {
        var writer = new StringWriter();
        ToProtoString(message, writer, options);
        return writer.ToString();
    }

    /// <summary>
    /// Prints the proto description of the specified <see cref="EnumDescriptor"/> to a writer.
    /// </summary>
    /// <param name="enumDesc">The descriptor to print.</param>
    /// <param name="writer">The text writer.</param>
    /// <param name="options">The printing options.</param>
    public static void ToProtoString(this EnumDescriptor enumDesc, TextWriter writer, DynamicGrpcPrinterOptions? options = null)
    {
        ToProtoString(enumDesc, new DynamicGrpcPrinterContext(writer, options ?? new DynamicGrpcPrinterOptions()));
    }

    /// <summary>
    /// Prints the proto description of the specified <see cref="EnumDescriptor"/> to a string.
    /// </summary>
    /// <param name="enumDesc">The descriptor to print.</param>
    /// <param name="options">The printing options.</param>
    /// <returns>A proto description of the specified descriptor.</returns>
    public static string ToProtoString(this EnumDescriptor enumDesc, DynamicGrpcPrinterOptions? options = null)
    {
        var writer = new StringWriter();
        ToProtoString(enumDesc, writer, options);
        return writer.ToString();
    }

    private static string GetEnumName(Enum enumValue)
    {
        var enumType = enumValue.GetType();
        foreach (var field in enumType.GetFields(BindingFlags.Static | BindingFlags.Public))
        {
            if (field.GetValue(null) == enumValue)
            {
                var originalNameAttribute = field.GetCustomAttribute<OriginalNameAttribute>();
                if (originalNameAttribute != null)
                {
                    return originalNameAttribute.Name;
                }
            }
        }

        return enumType.ToString();
    }

    private static void ToProtoString(this FileDescriptor file, DynamicGrpcPrinterContext context)
    {
        if (context.Options.AddMetaComments)
        {
            context.WriteLine($"// {file.Name} is a proto file.");
        }

        bool requiresNewLine = false;
        // Write syntax
        switch (file.Syntax)
        {
            case Syntax.Proto2:
                context.WriteLine("syntax = \"proto2\";");
                requiresNewLine = true;
                break;
            case Syntax.Proto3:
                context.WriteLine("syntax = \"proto3\";");
                requiresNewLine = true;
                break;
        }

        var options = file.GetOptions();
        if (options != null)
        {
            ToProtoString(options, context);
        }

        // Dump package
        if (requiresNewLine) context.WriteLine();
        requiresNewLine = false;
        if (!string.IsNullOrWhiteSpace(file.Package))
        {
            context.WriteLine($"package {file.Package};");
            requiresNewLine = true;
        }
        context.PushContextName(file.Package);

        // Dump imports
        if (requiresNewLine) context.WriteLine();
        requiresNewLine = false;
        foreach (var import in file.Dependencies)
        {
            context.WriteLine($"import \"{import.Name}\"");
            requiresNewLine = true;
        }

        // Dump services
        if (requiresNewLine) context.WriteLine();
        requiresNewLine = false;
        foreach (var serviceDescriptor in file.Services)
        {
            ToProtoString(serviceDescriptor, context);
            context.WriteLine();
        }

        // Dump message types
        foreach (var messageDescriptor in file.MessageTypes)
        {
            ToProtoString(messageDescriptor, context);
            context.WriteLine();
        }

        // Dump message types
        foreach (var enumDescriptor in file.EnumTypes)
        {
            ToProtoString(enumDescriptor, context);
            context.WriteLine();
        }

        context.PopContextName(file.Package);
    }

    private static void ToProtoString(this ServiceDescriptor service, DynamicGrpcPrinterContext context)
    {
        if (context.Options.AddMetaComments)
        {
            context.WriteLine($"// {service.FullName} is a service:");
        }
        context.WriteLine($"service {service.Name} {{");
        context.PushContextName(service.Name);
        context.Indent();
        foreach (var method in service.Methods)
        {
            context.WriteLine($"rpc {method.Name} ( {context.GetTypeName(method.InputType)} ) returns ( {context.GetTypeName(method.OutputType)} );");
        }
        context.UnIndent();
        context.PopContextName(service.Name);
        context.WriteLine("}");
    }

    private static void ToProtoString(this MessageDescriptor message, DynamicGrpcPrinterContext context)
    {
        bool isEmpty = message.Fields.InDeclarationOrder().Count == 0 && message.NestedTypes.Count == 0 && message.EnumTypes.Count == 0 && message.GetOptions() == null;

        if (context.Options.AddMetaComments)
        {
            context.WriteLine($"// {message.FullName} is {(isEmpty?"an empty":"a")} message:");
        }

        // Compact form, if a message is empty, output a single line
        if (isEmpty)
        {
            context.WriteLine($"message {message.Name} {{}}");
            return;
        }

        context.WriteLine($"message {message.Name} {{");
        context.PushContextName(message.Name);
        context.Indent();

        // handle options
        var options = message.GetOptions();
        if (options != null)
        {
            // message_set_wire_format
            if (options.HasMessageSetWireFormat) context.WriteLine($"option message_set_wire_format = {options.MessageSetWireFormat.Bool()};");
            // no_standard_descriptor_accessor
            if (options.HasNoStandardDescriptorAccessor) context.WriteLine($"option no_standard_descriptor_accessor = {options.NoStandardDescriptorAccessor.Bool()};");
            // deprecated
            if (options.HasDeprecated) context.WriteLine($"option deprecated = {options.Deprecated.Bool()};");
            // map_entry
            if (options.HasMapEntry) context.WriteLine($"option map_entry = {options.MapEntry.Bool()};");
        }

        bool requiresNewLine = false;
        OneofDescriptor? currentOneOf = null;
        foreach (var field in message.Fields.InDeclarationOrder())
        {
            var oneof = field.RealContainingOneof;
            if (currentOneOf != oneof)
            {
                if (currentOneOf is not null)
                {
                    context.UnIndent();
                    context.WriteLine("}");
                }

                if (oneof is not null)
                {
                    context.WriteLine($"oneof {oneof.Name} {{");
                    context.Indent();
                }
            }
            currentOneOf = oneof;

            // handle options
            var fieldOptions = field.GetOptions();
            var fieldOptionsAsText = string.Empty;
            if (fieldOptions != null)
            {
                var fieldOptionList = new List<string>();

                // ctype
                if (fieldOptions.HasCtype) fieldOptionList.Add($"ctype = {GetEnumName(fieldOptions.Ctype)}");
                // packed
                if (fieldOptions.HasPacked) fieldOptionList.Add($"packed = {fieldOptions.Packed.Bool()}");
                // jstype
                if (fieldOptions.HasJstype) fieldOptionList.Add($"jstype = {GetEnumName(fieldOptions.Jstype)}");
                // lazy
                if (fieldOptions.HasLazy) fieldOptionList.Add($"lazy = {fieldOptions.Lazy.Bool()}");
                // deprecated
                if (fieldOptions.Deprecated) fieldOptionList.Add($"deprecated = {fieldOptions.Deprecated.Bool()}");
                // weak
                if (fieldOptions.HasWeak) context.WriteLine($"weak = {fieldOptions.Weak.Bool()}");

                if (fieldOptionList.Count > 0)
                {
                    fieldOptionsAsText = $" [ {string.Join(", ", fieldOptionList)} ]";
                }
            }

            if (fieldOptions is { Deprecated: true })
            {
                fieldOptionsAsText = " [ deprecated = true ]";
            }

            context.WriteLine($"{context.GetTypeName(field)} {field.Name} = {field.FieldNumber}{fieldOptionsAsText};");
            requiresNewLine = true;
        }

        if (currentOneOf is not null)
        {
            context.UnIndent();
            context.WriteLine("}");
        }

        if (message.NestedTypes.Count > 0)
        {
            if (requiresNewLine) context.WriteLine();
            requiresNewLine = false;
            for (var index = 0; index < message.NestedTypes.Count; index++)
            {
                var nestedMessageType = message.NestedTypes[index];
                ToProtoString(nestedMessageType, context);

                // Don't output a trailing \n for the last entry
                if (message.EnumTypes.Count > 0 || index + 1 < message.EnumTypes.Count)
                {
                    context.WriteLine();
                }
            }
        }

        if (message.EnumTypes.Count > 0)
        {
            if (requiresNewLine) context.WriteLine();
            for (var index = 0; index < message.EnumTypes.Count; index++)
            {
                var enumDescriptor = message.EnumTypes[index];
                ToProtoString(enumDescriptor, context);

                // Don't output a trailing \n for the last entry
                if (index + 1 < message.EnumTypes.Count)
                {
                    context.WriteLine();
                }
            }
        }

        context.UnIndent();
        context.PopContextName(message.Name);
        context.WriteLine("}");
    }

    private static string Bool(this bool value) => value ? "true" : "false";

    private static void ToProtoString(this EnumDescriptor enumDescriptor, DynamicGrpcPrinterContext context)
    {
        if (context.Options.AddMetaComments)
        {
            context.WriteLine($"// {enumDescriptor.FullName} is an enum:");
        }
        context.WriteLine($"enum {enumDescriptor.Name} {{");
        context.Indent();
        foreach (var item in enumDescriptor.Values)
        {
            context.WriteLine($"{item.Name} = {item.Number};");
        }
        context.UnIndent();
        context.WriteLine("}");
    }

    private static void ToProtoString(FileOptions options, DynamicGrpcPrinterContext context)
    {
        // java_package
        if (options.HasJavaPackage) context.WriteLine($"option java_package = \"{options.JavaPackage}\";");
        // java_outer_classname
        if (options.HasJavaOuterClassname) context.WriteLine($"option java_outer_classname = \"{options.JavaOuterClassname}\";");
        // java_multiple_files
        if (options.HasJavaMultipleFiles) context.WriteLine($"option java_multiple_files = {options.JavaMultipleFiles.Bool()};");
        // java_generate_equals_and_hash
#pragma warning disable CS0612 // Type or member is obsolete
        if (options.HasJavaGenerateEqualsAndHash) context.WriteLine($"option java_generate_equals_and_hash = {options.JavaGenerateEqualsAndHash.Bool()};");
#pragma warning restore CS0612 // Type or member is obsolete
                              // java_string_check_utf8
        if (options.HasJavaStringCheckUtf8) context.WriteLine($"option java_string_check_utf8 = {options.JavaStringCheckUtf8.Bool()};");
        // optimize_for
        if (options.HasOptimizeFor) context.WriteLine($"option optimize_for = {GetEnumName(options.OptimizeFor)};");
        // go_package
        if (options.HasJavaMultipleFiles) context.WriteLine($"option go_package = {options.JavaMultipleFiles.Bool()};");
        // cc_generic_services
        if (options.HasCcGenericServices) context.WriteLine($"option cc_generic_services = {options.CcGenericServices.Bool()};");
        // java_generic_services
        if (options.HasJavaGenericServices) context.WriteLine($"option java_generic_services = {options.JavaGenericServices.Bool()};");
        // py_generic_services
        if (options.HasPyGenericServices) context.WriteLine($"option py_generic_services = {options.PyGenericServices.Bool()};");
        // php_generic_services
        if (options.HasPhpGenericServices) context.WriteLine($"option php_generic_services = {options.PhpGenericServices.Bool()};");
        // deprecated
        if (options.HasDeprecated) context.WriteLine($"option deprecated = {options.Deprecated.Bool()};");
        // cc_enable_arenas
        if (options.HasCcEnableArenas) context.WriteLine($"option cc_enable_arenas = {options.CcEnableArenas.Bool()};");
        // objc_class_prefix
        if (options.HasObjcClassPrefix) context.WriteLine($"option objc_class_prefix = \"{options.ObjcClassPrefix}\";");
        // csharp_namespace
        if (options.HasCsharpNamespace) context.WriteLine($"option csharp_namespace = \"{options.CsharpNamespace}\";");
        // swift_prefix
        if (options.HasSwiftPrefix) context.WriteLine($"option swift_prefix = \"{options.SwiftPrefix}\";");
        // php_class_prefix
        if (options.HasPhpClassPrefix) context.WriteLine($"option php_class_prefix = \"{options.PhpClassPrefix}\";");
        // php_namespace
        if (options.HasPhpNamespace) context.WriteLine($"option php_namespace = \"{options.PhpNamespace}\";");
        // php_metadata_namespace
        if (options.HasPhpMetadataNamespace) context.WriteLine($"option php_metadata_namespace = \"{options.PhpMetadataNamespace}\";");
        // ruby_package
        if (options.HasRubyPackage) context.WriteLine($"option ruby_package = \"{options.RubyPackage}\";");
    }

    private class DynamicGrpcPrinterContext
    {
        private readonly List<string> _contextNames;

        public DynamicGrpcPrinterContext(TextWriter writer, DynamicGrpcPrinterOptions options)
        {
            _contextNames = new List<string>();
            Writer = writer;
            Options = options;
        }

        public DynamicGrpcPrinterOptions Options { get; }

        public int Level { get; set; }

        public void Indent() => Level++;

        public void UnIndent() => Level--;

        public TextWriter Writer { get; }

        public void WriteLine()
        {
            Writer.WriteLine();
        }

        public void WriteLine(string text)
        {
            WriteIndent();
            Writer.WriteLine(text);
        }

        private void WriteIndent()
        {
            var indent = Options.Indent;
            for (int i = 0; i < Level; i++)
            {
                Writer.Write(indent);
            }
        }

        public void PushContextName(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            foreach (var partName in name.Split('.'))
            {
                _contextNames.Add($"{partName}.");
            }
        }

        public void PopContextName(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            foreach (var _ in name.Split('.'))
            {
                _contextNames.RemoveAt(_contextNames.Count - 1);
            }
        }

        public string GetTypeName(MessageDescriptor descriptor)
        {
            return GetContextualTypeName(descriptor.FullName);
        }

        public string GetTypeName(FieldDescriptor field)
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
                    builder.Append("float");
                    break;
                case FieldType.Int64:
                    builder.Append("int64");
                    break;
                case FieldType.UInt64:
                    builder.Append("uint64");
                    break;
                case FieldType.Int32:
                    builder.Append("int32");
                    break;
                case FieldType.Fixed64:
                    builder.Append("fixed64");
                    break;
                case FieldType.Fixed32:
                    builder.Append("fixed32");
                    break;
                case FieldType.Bool:
                    builder.Append("bool");
                    break;
                case FieldType.String:
                    builder.Append("string");
                    break;
                case FieldType.Group:
                    break;
                case FieldType.Message:
                    builder.Append(GetContextualTypeName(field.MessageType.FullName));
                    break;
                case FieldType.Bytes:
                    builder.Append("bytes");
                    break;
                case FieldType.UInt32:
                    builder.Append("uint32");
                    break;
                case FieldType.SFixed32:
                    builder.Append("sfixed32");
                    break;
                case FieldType.SFixed64:
                    builder.Append("sfixed64");
                    break;
                case FieldType.SInt32:
                    builder.Append("sint32");
                    break;
                case FieldType.SInt64:
                    builder.Append("sint64");
                    break;
                case FieldType.Enum:
                    builder.Append(GetContextualTypeName(field.EnumType.FullName));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return builder.ToString();
        }

        private string GetContextualTypeName(string fullTypeName)
        {
            if (Options.FullyQualified) return $".{fullTypeName}";

            int nextIndex = 0;
            foreach (var partName in _contextNames)
            {
                var currentIndex = fullTypeName.IndexOf(partName, nextIndex, StringComparison.OrdinalIgnoreCase);
                if (currentIndex != nextIndex)
                {
                    break;
                }

                nextIndex = currentIndex + partName.Length;
            }

            return nextIndex > 0 ? fullTypeName.Substring(nextIndex) : $".{fullTypeName}";
        }
    }
}