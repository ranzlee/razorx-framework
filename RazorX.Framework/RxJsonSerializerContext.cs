using System.Text.Json;
using System.Text.Json.Serialization;

namespace RazorX.Framework;

[JsonSerializable(typeof(CloseDialogTrigger))]
[JsonSerializable(typeof(FocusElementTrigger))]
[JsonSerializable(typeof(SetStateTrigger))]
[JsonSerializable(typeof(ToastTrigger))]
[JsonSerializable(typeof(List<MergeStrategy>))]
[JsonSerializable(typeof(MergeStrategy))]
[JsonSerializable(typeof(SseEventPayload))]
// Form converter types - primitives
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(char))]
[JsonSerializable(typeof(char?))]
[JsonSerializable(typeof(DateOnly))]
[JsonSerializable(typeof(DateOnly?))]
[JsonSerializable(typeof(DateTime))]
[JsonSerializable(typeof(DateTime?))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(bool?))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(int?))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(long?))]
[JsonSerializable(typeof(short))]
[JsonSerializable(typeof(short?))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(decimal?))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(double?))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(float?))]
// Form converter types - collections
[JsonSerializable(typeof(IEnumerable<string>))]
[JsonSerializable(typeof(IEnumerable<char>))]
[JsonSerializable(typeof(IEnumerable<DateOnly>))]
[JsonSerializable(typeof(IEnumerable<DateTime>))]
[JsonSerializable(typeof(IEnumerable<bool>))]
[JsonSerializable(typeof(IEnumerable<int>))]
[JsonSerializable(typeof(IEnumerable<long>))]
[JsonSerializable(typeof(IEnumerable<short>))]
[JsonSerializable(typeof(IEnumerable<decimal>))]
[JsonSerializable(typeof(IEnumerable<double>))]
[JsonSerializable(typeof(IEnumerable<float>))]
// Lists (concrete types for deserialization)
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<char>))]
[JsonSerializable(typeof(List<DateOnly>))]
[JsonSerializable(typeof(List<DateTime>))]
[JsonSerializable(typeof(List<bool>))]
[JsonSerializable(typeof(List<int>))]
[JsonSerializable(typeof(List<long>))]
[JsonSerializable(typeof(List<short>))]
[JsonSerializable(typeof(List<decimal>))]
[JsonSerializable(typeof(List<double>))]
[JsonSerializable(typeof(List<float>))]
// Arrays (for serialization in tests and real usage)
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(char[]))]
[JsonSerializable(typeof(DateOnly[]))]
[JsonSerializable(typeof(DateTime[]))]
[JsonSerializable(typeof(bool[]))]
[JsonSerializable(typeof(int[]))]
[JsonSerializable(typeof(long[]))]
[JsonSerializable(typeof(short[]))]
[JsonSerializable(typeof(decimal[]))]
[JsonSerializable(typeof(double[]))]
[JsonSerializable(typeof(float[]))]
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
internal partial class RxJsonSerializerContext : JsonSerializerContext { }

internal static class RxJsonSerializer {
    private static readonly RxJsonSerializerContext Context = new(new JsonSerializerOptions {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    });

    public static string Serialize<T>(T value) where T : class {
        return value switch {
            CloseDialogTrigger trigger => JsonSerializer.Serialize(trigger, Context.CloseDialogTrigger),
            FocusElementTrigger trigger => JsonSerializer.Serialize(trigger, Context.FocusElementTrigger),
            SetStateTrigger trigger => JsonSerializer.Serialize(trigger, Context.SetStateTrigger),
            ToastTrigger trigger => JsonSerializer.Serialize(trigger, Context.ToastTrigger),
            List<MergeStrategy> strategies => JsonSerializer.Serialize(strategies, Context.ListMergeStrategy),
            SseEventPayload payload => JsonSerializer.Serialize(payload, Context.SseEventPayload),
            _ => JsonSerializer.Serialize(value, value.GetType(), Context)
        };
    }
}