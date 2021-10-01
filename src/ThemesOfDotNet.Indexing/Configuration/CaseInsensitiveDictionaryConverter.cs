using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThemesOfDotNet.Indexing.Configuration;

internal sealed class CaseInsensitiveDictionaryConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        if (typeToConvert.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))
        {
            var arguments = typeToConvert.GetGenericArguments();
            if (arguments.Length == 2 &&
                arguments[0] == typeof(string))
            {
                return true;
            }
        }

        return false;
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetGenericArguments()[1];
        var converterType = typeof(CaseInsensitiveDictionaryConverterOfT<>).MakeGenericType(valueType);
        return (JsonConverter?)Activator.CreateInstance(converterType);
    }

    private sealed class CaseInsensitiveDictionaryConverterOfT<T> : JsonConverter<IReadOnlyDictionary<string, T>>
    {
        public override IReadOnlyDictionary<string, T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var parsed = JsonSerializer.Deserialize<IReadOnlyDictionary<string, T>>(ref reader, options)!;
            var result = new Dictionary<string, T>(parsed, StringComparer.OrdinalIgnoreCase);
            return result;
        }

        public override void Write(Utf8JsonWriter writer, IReadOnlyDictionary<string, T> value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}
