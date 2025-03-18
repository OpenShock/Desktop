using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenShock.Desktop.Utils;

public sealed class ByteArrayHexConverter : JsonConverter<byte[]>
{
    public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected string, but got {reader.TokenType}");
        
        return Convert.FromHexString(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(Convert.ToHexString(value));
    }
}
