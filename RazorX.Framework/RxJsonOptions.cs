using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RazorX.Framework;

/// <summary>
/// Cached JsonSerializerOptions instances to avoid repeated allocations
/// </summary>
file static class JsonOptionsCache {
    public static readonly JsonSerializerOptions CleanOptions = new();
}

/// <summary>
/// Adds the JSON converters necessary for converting the request JSON payload created from FORM data.
/// ASP.NET Minimal APIs have much better support for JSON binding compared to FORM data. Form data values are always
/// strings. The custom converters coerce the string values into the correct data types for model binding.
/// </summary>
/// <param name="httpContextAccessor">IHttpContextAccessor</param>
/// <param name="logger">ILogger</param>
public class RxJsonOptions(IHttpContextAccessor httpContextAccessor, ILogger<RxJsonOptions> logger) : IConfigureOptions<JsonOptions> {

    public void Configure(JsonOptions options) {
        // form values
        options.SerializerOptions.Converters.Add(new CharJsonConverter(httpContextAccessor, logger));
        options.SerializerOptions.Converters.Add(new DateOnlyJsonConverter(httpContextAccessor, logger));
        options.SerializerOptions.Converters.Add(new DateTimeJsonConverter(httpContextAccessor, logger));
        options.SerializerOptions.Converters.Add(new BooleanJsonConverter(httpContextAccessor, logger));
        options.SerializerOptions.Converters.Add(new IntJsonConverter(httpContextAccessor, logger));
        options.SerializerOptions.Converters.Add(new LongJsonConverter(httpContextAccessor, logger));
        options.SerializerOptions.Converters.Add(new ShortJsonConverter(httpContextAccessor, logger));
        options.SerializerOptions.Converters.Add(new DecimalJsonConverter(httpContextAccessor, logger));
        options.SerializerOptions.Converters.Add(new DoubleJsonConverter(httpContextAccessor, logger));
        options.SerializerOptions.Converters.Add(new FloatJsonConverter(httpContextAccessor, logger));
        // form value collections (arrays)
        options.SerializerOptions.Converters.Add(new SingleOrArrayConverter<string>(httpContextAccessor, logger));
        options.SerializerOptions.Converters.Add(new SingleOrArrayConverter<char>(httpContextAccessor, logger));
        options.SerializerOptions.Converters.Add(new SingleOrArrayConverter<DateOnly>(httpContextAccessor, logger));
        options.SerializerOptions.Converters.Add(new SingleOrArrayConverter<DateTime>(httpContextAccessor, logger));
        options.SerializerOptions.Converters.Add(new SingleOrArrayConverter<bool>(httpContextAccessor, logger));
        options.SerializerOptions.Converters.Add(new SingleOrArrayConverter<int>(httpContextAccessor, logger));
        options.SerializerOptions.Converters.Add(new SingleOrArrayConverter<long>(httpContextAccessor, logger));
        options.SerializerOptions.Converters.Add(new SingleOrArrayConverter<short>(httpContextAccessor, logger));
        options.SerializerOptions.Converters.Add(new SingleOrArrayConverter<decimal>(httpContextAccessor, logger));
        options.SerializerOptions.Converters.Add(new SingleOrArrayConverter<double>(httpContextAccessor, logger));
        options.SerializerOptions.Converters.Add(new SingleOrArrayConverter<float>(httpContextAccessor, logger));
        // enum converter
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }
}

// Generic base converter that handles all common form value conversion logic
public abstract class FormValueJsonConverter<T>(
    IHttpContextAccessor httpContextAccessor,
    ILogger logger) : JsonConverter<T?> where T : struct {
    protected abstract string ConverterName { get; }
    protected abstract bool TryParseValue(string input, out T result);
    protected abstract void WriteValue(Utf8JsonWriter writer, T value);
    // Virtual method to allow subclasses to customize input preparation
    protected virtual string? PrepareInput(string? originalString) => originalString?.Trim();
    // Virtual method to allow subclasses to customize early null detection
    protected virtual bool ShouldReturnNullForInput(string? originalString, string? preparedString) {
        return string.IsNullOrEmpty(preparedString);
    }
    // Handle null values so our Write method gets called
    public override bool HandleNull => true;

    public override T? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) {
        var originalString = reader.GetString();
        var s = PrepareInput(originalString);
        if (ShouldReturnNullForInput(originalString, s)) {
            if (logger.IsEnabled(LogLevel.Trace)) {
                logger.LogTrace("{converter}.{method} returned null.",
                    ConverterName,
                    nameof(Read));
            }
            return null;
        }
        if (httpContextAccessor.HttpContext is null ||
            !httpContextAccessor.HttpContext.Request.IsRxRequest()) {
            if (logger.IsEnabled(LogLevel.Trace)) {
                logger.LogTrace("No HttpContext or is not rx-request - {converter}.{method} called default JsonSerializer.Deserialize<{type}>() for {val}.",
                    ConverterName,
                    nameof(Read),
                    typeof(T),
                    s);
            }
            // For non-RxRequest, still parse the string value but without special form processing
            if (s != null && TryParseValue(s, out var fallbackResult)) {
                return fallbackResult;
            }
            return null;
        }
        if (s == null) {
            if (logger.IsEnabled(LogLevel.Trace)) {
                logger.LogTrace("{converter}.{method} overriding Deserialize for \"{val}\" results: {result}.",
                    ConverterName,
                    nameof(Read),
                    "null",
                    "null - TryParse() failed");
            }
            return null;
        }
        var isValid = TryParseValue(s, out var result);
        if (logger.IsEnabled(LogLevel.Trace)) {
            logger.LogTrace("{converter}.{method} overriding Deserialize for \"{val}\" results: {result}.",
                ConverterName,
                nameof(Read),
                s,
                isValid ? result : "null - TryParse() failed");
        }
        return isValid ? result : null;
    }

    public override void Write(
        Utf8JsonWriter writer,
        T? value,
        JsonSerializerOptions options) {
        if (value.HasValue) {
            if (logger.IsEnabled(LogLevel.Trace)) {
                logger.LogTrace("{converter}.{method} writing {result}.",
                    ConverterName,
                    nameof(Write),
                    value.Value);
            }
            WriteValue(writer, value.Value);
            return;
        }
        if (logger.IsEnabled(LogLevel.Trace)) {
            logger.LogTrace("{converter}.{method} writing null.",
                ConverterName,
                nameof(Write));
        }
        writer.WriteNullValue();
    }
}

/// <summary>
/// JSON converter that handles both single values and arrays for form data binding.
/// </summary>
/// <typeparam name="T">The element type of the collection.</typeparam>
/// <remarks>
/// This converter is automatically registered by the framework when AddJsonConverters is enabled.
/// It allows form fields to be submitted as either single values or arrays.
/// </remarks>
public sealed class SingleOrArrayConverter<T>(IHttpContextAccessor httpContextAccessor, ILogger logger) : JsonConverter<IEnumerable<T>> {
    // Handle null values so our Write method gets called
    public override bool HandleNull => true;

    public override IEnumerable<T>? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) {
        // Handle null tokens first
        if (reader.TokenType == JsonTokenType.Null) {
            if (logger.IsEnabled(LogLevel.Trace)) {
                logger.LogTrace("{converter}.{method} returned null.",
                    nameof(SingleOrArrayConverter<>),
                    nameof(Read));
            }
            return null;
        }
        // For non-RxRequest, use simpler fallback behavior
        if (httpContextAccessor.HttpContext is null || !httpContextAccessor.HttpContext.Request.IsRxRequest()) {
            if (logger.IsEnabled(LogLevel.Trace)) {
                logger.LogTrace("No HttpContext or is not rx-request - {converter}.{method} called default JsonSerializer.Deserialize<{type}>() for input.",
                    nameof(SingleOrArrayConverter<>),
                    nameof(Read),
                    typeof(IEnumerable<T>));
            }
            return DeserializeWithFallback(ref reader);
        }
        // Handle RxRequest scenarios
        return reader.TokenType switch {
            JsonTokenType.StartArray => DeserializeArray(ref reader),
            JsonTokenType.String => DeserializeSingleString(reader.GetString()),
            _ => DeserializeSingleValue(ref reader)
        };
    }

    private static IEnumerable<T>? DeserializeWithFallback(ref Utf8JsonReader reader) {
        if (reader.TokenType == JsonTokenType.String) {
            var s = reader.GetString();
            if (string.IsNullOrWhiteSpace(s)) return null;
            return JsonSerializer.Deserialize<IEnumerable<T>>(s, JsonOptionsCache.CleanOptions);
        }
        return JsonSerializer.Deserialize<IEnumerable<T>>(ref reader, JsonOptionsCache.CleanOptions);
    }

    private static List<T> DeserializeArray(ref Utf8JsonReader reader) {
        var list = new List<T>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) {
            list.Add(JsonSerializer.Deserialize<T>(ref reader, JsonOptionsCache.CleanOptions)!);
        }
        return list;
    }

    private IEnumerable<T>? DeserializeSingleString(string? stringValue) {
        if (string.IsNullOrWhiteSpace(stringValue)) {
            if (logger.IsEnabled(LogLevel.Trace)) {
                logger.LogTrace("{converter}.{method} returned null.",
                    nameof(SingleOrArrayConverter<>),
                    nameof(Read));
            }
            return null;
        }
        // Convert the string to T and return it as a single-item array
        var item = JsonSerializer.Deserialize<T>($"\"{stringValue}\"", JsonOptionsCache.CleanOptions)!;
        return [item];
    }

    private static List<T> DeserializeSingleValue(ref Utf8JsonReader reader) {
        var item = JsonSerializer.Deserialize<T>(ref reader, JsonOptionsCache.CleanOptions)!;
        return [item];
    }

    public override void Write(
        Utf8JsonWriter writer,
        IEnumerable<T>? objectToWrite,
        JsonSerializerOptions options) {
        if (objectToWrite is null) {
            if (logger.IsEnabled(LogLevel.Trace)) {
                logger.LogTrace("{converter}.{method} writing null.",
                    nameof(SingleOrArrayConverter<>),
                    nameof(Write));
            }
            writer.WriteNullValue();
            return;
        }
        if (logger.IsEnabled(LogLevel.Trace)) {
            logger.LogTrace("{converter}.{method} writing {type}.",
                nameof(SingleOrArrayConverter<>),
                nameof(Write),
                objectToWrite.GetType());
        }
        JsonSerializer.Serialize(writer, objectToWrite, objectToWrite.GetType(), options);
    }

}

/// <summary>
/// JSON converter for DateOnly values from form data.
/// </summary>
/// <remarks>
/// Automatically registered by the framework when AddJsonConverters is enabled.
/// Handles string-to-DateOnly conversion for form submissions.
/// </remarks>
public sealed class DateOnlyJsonConverter(IHttpContextAccessor hca, ILogger logger)
    : FormValueJsonConverter<DateOnly>(hca, logger) {
    protected override string ConverterName => nameof(DateOnlyJsonConverter);
    protected override bool TryParseValue(string input, out DateOnly result)
        => DateOnly.TryParse(input, out result);
    protected override void WriteValue(Utf8JsonWriter writer, DateOnly value)
        => writer.WriteStringValue(value.ToDateTime(TimeOnly.MinValue));
}

/// <summary>
/// JSON converter for DateTime values from form data.
/// </summary>
/// <remarks>
/// Automatically registered by the framework when AddJsonConverters is enabled.
/// Handles string-to-DateTime conversion for form submissions.
/// </remarks>
public sealed class DateTimeJsonConverter(IHttpContextAccessor hca, ILogger logger)
    : FormValueJsonConverter<DateTime>(hca, logger) {
    protected override string ConverterName => nameof(DateTimeJsonConverter);
    protected override bool TryParseValue(string input, out DateTime result)
        => DateTime.TryParse(input, out result);
    protected override void WriteValue(Utf8JsonWriter writer, DateTime value)
        => writer.WriteStringValue(value);
}

/// <summary>
/// JSON converter for boolean values from form data.
/// </summary>
/// <remarks>
/// Automatically registered by the framework when AddJsonConverters is enabled.
/// Handles string-to-bool conversion including "true", "false", "on", "off" values.
/// </remarks>
public sealed class BooleanJsonConverter(IHttpContextAccessor httpContextAccessor, ILogger logger) : JsonConverter<bool> {
    // Handle null values so our Write method gets called
    public override bool HandleNull => true;

    public override bool Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) {
        var s = reader.GetString()?.Trim().ToLower();
        // Return false for null/empty strings (consistent with default bool behavior)
        if (string.IsNullOrEmpty(s)) {
            if (logger.IsEnabled(LogLevel.Trace)) {
                logger.LogTrace("{converter}.{method} returned false for null/empty input.",
                    nameof(BooleanJsonConverter),
                    nameof(Read));
            }
            return false;
        }
        if (httpContextAccessor.HttpContext is null || !httpContextAccessor.HttpContext.Request.IsRxRequest()) {
            if (logger.IsEnabled(LogLevel.Trace)) {
                logger.LogTrace("No HttpContext or is not rx-request - {converter}.{method} called default JsonSerializer.Deserialize<{type}>() for {val}.",
                    nameof(BooleanJsonConverter),
                    nameof(Read),
                    typeof(bool),
                    s);
            }
            // For boolean fallback, parse directly
            return bool.TryParse(s, out var fallbackResult) && fallbackResult;
        }
        // Try standard boolean parsing first
        var isValid = bool.TryParse(s, out var b);
        // Handle HTML checkbox "on" value
        if (!isValid && s == "on") {
            isValid = true;
            b = true;
        }
        if (logger.IsEnabled(LogLevel.Trace)) {
            logger.LogTrace("{converter}.{method} overriding Deserialize for \"{val}\" results: {result}.",
                nameof(BooleanJsonConverter),
                nameof(Read),
                s,
                isValid ? b : "false - TryParse() failed");
        }
        return isValid && b;
    }

    public override void Write(
        Utf8JsonWriter writer,
        bool boolValue,
        JsonSerializerOptions options) {
        if (logger.IsEnabled(LogLevel.Trace)) {
            logger.LogTrace("{converter}.{method} writing {result}.",
                nameof(BooleanJsonConverter),
                nameof(Write),
                boolValue);
        }
        writer.WriteBooleanValue(boolValue);
    }
}

/// <summary>
/// JSON converter for integer values from form data.
/// </summary>
/// <remarks>
/// Automatically registered by the framework when AddJsonConverters is enabled.
/// Handles string-to-int conversion for form submissions.
/// </remarks>
public sealed class IntJsonConverter(IHttpContextAccessor hca, ILogger logger)
    : FormValueJsonConverter<int>(hca, logger) {
    protected override string ConverterName => nameof(IntJsonConverter);
    protected override bool TryParseValue(string input, out int result)
        => int.TryParse(input, out result);
    protected override void WriteValue(Utf8JsonWriter writer, int value)
        => writer.WriteNumberValue(value);
}

/// <summary>
/// JSON converter for long integer values from form data.
/// </summary>
/// <remarks>
/// Automatically registered by the framework when AddJsonConverters is enabled.
/// Handles string-to-long conversion for form submissions.
/// </remarks>
public sealed class LongJsonConverter(IHttpContextAccessor hca, ILogger logger)
    : FormValueJsonConverter<long>(hca, logger) {
    protected override string ConverterName => nameof(LongJsonConverter);
    protected override bool TryParseValue(string input, out long result)
        => long.TryParse(input, out result);
    protected override void WriteValue(Utf8JsonWriter writer, long value)
        => writer.WriteNumberValue(value);
}

/// <summary>
/// JSON converter for short integer values from form data.
/// </summary>
/// <remarks>
/// Automatically registered by the framework when AddJsonConverters is enabled.
/// Handles string-to-short conversion for form submissions.
/// </remarks>
public sealed class ShortJsonConverter(IHttpContextAccessor hca, ILogger logger)
    : FormValueJsonConverter<short>(hca, logger) {
    protected override string ConverterName => nameof(ShortJsonConverter);
    protected override bool TryParseValue(string input, out short result)
        => short.TryParse(input, out result);
    protected override void WriteValue(Utf8JsonWriter writer, short value)
        => writer.WriteNumberValue(value);
}

/// <summary>
/// JSON converter for decimal values from form data.
/// </summary>
/// <remarks>
/// Automatically registered by the framework when AddJsonConverters is enabled.
/// Handles string-to-decimal conversion for form submissions.
/// </remarks>
public sealed class DecimalJsonConverter(IHttpContextAccessor hca, ILogger logger)
    : FormValueJsonConverter<decimal>(hca, logger) {
    protected override string ConverterName => nameof(DecimalJsonConverter);
    protected override bool TryParseValue(string input, out decimal result)
        => decimal.TryParse(input, out result);
    protected override void WriteValue(Utf8JsonWriter writer, decimal value)
        => writer.WriteNumberValue(value);
}

/// <summary>
/// JSON converter for double-precision floating-point values from form data.
/// </summary>
/// <remarks>
/// Automatically registered by the framework when AddJsonConverters is enabled.
/// Handles string-to-double conversion for form submissions.
/// </remarks>
public sealed class DoubleJsonConverter(IHttpContextAccessor hca, ILogger logger)
    : FormValueJsonConverter<double>(hca, logger) {
    protected override string ConverterName => nameof(DoubleJsonConverter);
    protected override bool TryParseValue(string input, out double result)
        => double.TryParse(input, out result);
    protected override void WriteValue(Utf8JsonWriter writer, double value)
        => writer.WriteNumberValue(value);
}

/// <summary>
/// JSON converter for single-precision floating-point values from form data.
/// </summary>
/// <remarks>
/// Automatically registered by the framework when AddJsonConverters is enabled.
/// Handles string-to-float conversion for form submissions.
/// </remarks>
public sealed class FloatJsonConverter(IHttpContextAccessor hca, ILogger logger)
    : FormValueJsonConverter<float>(hca, logger) {
    protected override string ConverterName => nameof(FloatJsonConverter);
    protected override bool TryParseValue(string input, out float result)
        => float.TryParse(input, out result);
    protected override void WriteValue(Utf8JsonWriter writer, float value)
        => writer.WriteNumberValue(value);
}

/// <summary>
/// JSON converter for character values from form data.
/// </summary>
/// <remarks>
/// Automatically registered by the framework when AddJsonConverters is enabled.
/// Handles string-to-char conversion for form submissions.
/// </remarks>
public sealed class CharJsonConverter(IHttpContextAccessor httpContextAccessor, ILogger logger)
    : FormValueJsonConverter<char>(httpContextAccessor, logger) {
    protected override string ConverterName => nameof(CharJsonConverter);
    protected override bool TryParseValue(string input, out char result)
        => char.TryParse(input, out result);
    protected override void WriteValue(Utf8JsonWriter writer, char value)
        => writer.WriteStringValue(value.ToString());

    // Don't trim spaces for char conversion - spaces are valid characters
    protected override string? PrepareInput(string? originalString) => originalString;

    // For char conversion, only return null for empty strings or multi-character whitespace-only strings
    protected override bool ShouldReturnNullForInput(string? originalString, string? preparedString) {
        return string.IsNullOrEmpty(originalString) ||
               (originalString?.Length > 1 && string.IsNullOrWhiteSpace(originalString));
    }
}