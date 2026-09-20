using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolarOptimiser.Providers.FoxESS.Contracts
{
    /// <summary>
    /// FoxESS's own field description says a "requested variable with no data is omitted" from the response, but
    /// says nothing about what happens when a variable IS present with an unusable value (a string placeholder,
    /// out-of-range number, or similar). A single such value must not fail the whole response's deserialization -
    /// every other variable's reading is still real, independently useful evidence. This converter maps anything
    /// it cannot parse as a number to <c>null</c> instead of throwing, so <see cref="FoxESSDeviceRealQueryDatum.Value"/>
    /// simply becomes an absent value (Collection then classifies that single reading as <c>Invalid</c>, not the
    /// whole capture as <c>ParseFailure</c>).
    /// </summary>
    public sealed class LenientDecimalConverter : JsonConverter<decimal?>
    {
        public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.Number && reader.TryGetDecimal(out decimal numericValue))
            {
                return numericValue;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                string? text = reader.GetString();
                if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsedFromString))
                {
                    return parsedFromString;
                }

                return null;
            }

            // An unexpected token shape (object, array, bool, an out-of-range number) - skip it and treat the
            // value as unusable rather than propagating a JsonException that would fail every other reading too.
            reader.Skip();
            return null;
        }

        public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteNumberValue(value.Value);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
