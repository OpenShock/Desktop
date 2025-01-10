namespace OpenShock.Desktop.Utils;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using OneOf;

// Credit to https://github.com/mcintyre321/OneOf/issues/118#issuecomment-1455110602 for the original implementation

public class OneOfConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeof(IOneOf).IsAssignableFrom(typeToConvert);

    public override JsonConverter CreateConverter(Type? typeToConvert, JsonSerializerOptions options)
    {
        var (oneOfGenericType, converterType) = GetTypes(typeToConvert);
        if (oneOfGenericType is null || converterType is null)
        {
            throw new NotSupportedException($"Cannot convert {typeToConvert}");
        }

        var jsonConverter = (JsonConverter)Activator.CreateInstance(
            converterType.MakeGenericType(oneOfGenericType.GenericTypeArguments),
            BindingFlags.Instance | BindingFlags.Public,
            null,
            [options],
            null)!;

        return jsonConverter;
    }

    private static (Type? oneOfGenericType, Type? converterType) GetTypes(Type? type)
    {
        while (type is not null)
        {
            if (type.IsGenericType)
            {
                var genericTypeDefinition = type.GetGenericTypeDefinition();
                if (genericTypeDefinition == typeof(OneOfBase<,>) ||
                    genericTypeDefinition == typeof(OneOf<,>))
                {
                    return (type, typeof(OneOf2JsonConverter<,>));
                }

                if (genericTypeDefinition == typeof(OneOfBase<,,>) ||
                    genericTypeDefinition == typeof(OneOf<,,>))
                {
                    return (type, typeof(OneOf3JsonConverter<,,>));
                }

                // TODO: Not supported (yet).
                // if (genericTypeDefinition == typeof(OneOfBase<,,,>) ||
                //     genericTypeDefinition == typeof(OneOf<,,,>))
                // {
                //   return (type, typeof(OneOfJson<,,,>));
                // }
                //
                // if (genericTypeDefinition == typeof(OneOfBase<,,,,>) ||
                //     genericTypeDefinition == typeof(OneOf<,,,,>))
                // {
                //   return (type, typeof(OneOfJson<,,,,>));
                // }
                //
                // if (genericTypeDefinition == typeof(OneOfBase<,,,,,>) ||
                //     genericTypeDefinition == typeof(OneOf<,,,,,>))
                // {
                //   return (type, typeof(OneOfJson<,,,,,>));
                // }
                //
                // if (genericTypeDefinition == typeof(OneOfBase<,,,,,,>) ||
                //     genericTypeDefinition == typeof(OneOf<,,,,,,>))
                // {
                //   return (type, typeof(OneOfJson<,,,,,,>));
                // }
                //
                // if (genericTypeDefinition == typeof(OneOfBase<,,,,,,,>) ||
                //     genericTypeDefinition == typeof(OneOf<,,,,,,,>))
                // {
                //   return (type, typeof(OneOfJson<,,,,,,,>));
                // }
                //
                // if (genericTypeDefinition == typeof(OneOfBase<,,,,,,,,>) ||
                //     genericTypeDefinition == typeof(OneOf<,,,,,,,,>))
                // {
                //   return (type, typeof(OneOfJson<,,,,,,,,>));
                // }
            }

            type = type.BaseType;
        }

        return (null, null);
    }

    private static IOneOf CreateOneOf(JsonSerializerOptions options, int index, JsonDocument doc, Type oneOfType, Type[] types)
    {
        var args = new object[types.Length + 1];
        args[0] = index;
        args[index + 1] = doc.Deserialize(types[index], options)!;

        var oneOf = Activator.CreateInstance(
            oneOfType,
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            args,
            null
        );

        return (IOneOf)oneOf!;
    }

    private const string IndexKey = "$index";

    private class OneOf2JsonConverter<T0, T1> : JsonConverter<OneOfBase<T0, T1>>
    {
        private static readonly Type OneOfType = typeof(OneOf<,>).MakeGenericType(typeof(T0), typeof(T1));
        private static readonly Type[] Types = { typeof(T0), typeof(T1) };

        // These are needed
        public OneOf2JsonConverter(JsonSerializerOptions _)
        {
        }

        public override OneOfBase<T0, T1> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            if (!doc.RootElement.TryGetProperty(IndexKey, out var indexElement) ||
                !indexElement.TryGetInt32(out var index) ||
                index is < 0 or > 1)
            {
                throw new JsonException("Cannot not find type index or type index is not a valid number");
            }

            var oneOf = CreateOneOf(options, index, doc, OneOfType, Types);

            return (OneOfBase<T0, T1>)Activator.CreateInstance(typeToConvert, oneOf)!;
        }

        public override void Write(Utf8JsonWriter writer, OneOfBase<T0, T1> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WritePropertyName(IndexKey);
            writer.WriteNumberValue(value.Index);

            using var doc = value.Match(
                t0 => JsonSerializer.SerializeToDocument(t0, options),
                t1 => JsonSerializer.SerializeToDocument(t1, options)
            );

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                prop.WriteTo(writer);
            }

            writer.WriteEndObject();
        }
    }

    private class OneOf3JsonConverter<T0, T1, T2> : JsonConverter<OneOfBase<T0, T1, T2>>
    {
        private static readonly Type OneOfType = typeof(OneOf<,,>).MakeGenericType(typeof(T0), typeof(T1), typeof(T2));
        private static readonly Type[] Types = { typeof(T0), typeof(T1), typeof(T2) };

        // These are needed
        public OneOf3JsonConverter(JsonSerializerOptions _)
        {
        }

        public override OneOfBase<T0, T1, T2> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            if (!doc.RootElement.TryGetProperty(IndexKey, out var indexElement) ||
                !indexElement.TryGetInt32(out var index) ||
                index is < 0 or > 2)
            {
                throw new JsonException("Cannot not find type index or type index is not a valid number");
            }

            var oneOfBase = CreateOneOf(options, index, doc, OneOfType, Types);

            return (OneOfBase<T0, T1, T2>)Activator.CreateInstance(typeToConvert, oneOfBase)!;
        }

        public override void Write(Utf8JsonWriter writer, OneOfBase<T0, T1, T2> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WritePropertyName(IndexKey);
            writer.WriteNumberValue(value.Index);

            using var doc = value.Match(
                t0 => JsonSerializer.SerializeToDocument(t0, options),
                t1 => JsonSerializer.SerializeToDocument(t1, options),
                t2 => JsonSerializer.SerializeToDocument(t2, options)
            );

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                prop.WriteTo(writer);
            }

            writer.WriteEndObject();
        }
    }
}