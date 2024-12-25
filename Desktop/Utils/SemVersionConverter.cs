namespace OpenShock.Desktop.Utils;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Semver;

public class SemVersionConverter : JsonConverter<SemVersion>
{
    public override SemVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if(reader.TokenType != JsonTokenType.String) throw new JsonException("Expected string value for SemVersion");
        
        return SemVersion.Parse(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, SemVersion value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }

    public override SemVersion ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if(reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("Expected string value for SemVersion");
        
        return SemVersion.Parse(reader.GetString()!);
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, SemVersion value, JsonSerializerOptions options)
    {
        writer.WritePropertyName(value.ToString());
    }
}