using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RazorX.Framework.Tests.Mocks;

namespace RazorX.Framework.Tests.JsonConverters;

[TestClass]
public abstract class JsonConverterTestBase {
    protected MockHttpContextAccessor MockHttpContextAccessor { get; private set; } = null!;
    protected TestLogger<object> TestLogger { get; private set; } = null!;

    [TestInitialize]
    public void SetUp() {
        MockHttpContextAccessor = new MockHttpContextAccessor();
        TestLogger = new TestLogger<object>();
    }

    protected void SetupRxRequest() {
        MockHttpContextAccessor = MockHttpContextAccessor.WithRxRequest();
    }

    protected void SetupNonRxRequest() {
        MockHttpContextAccessor = MockHttpContextAccessor.WithoutRxRequest();
    }

    protected void SetupNoHttpContext() {
        MockHttpContextAccessor = MockHttpContextAccessor.WithoutHttpContext();
    }

    protected T? DeserializeFromJson<T>(string json, JsonConverter converter) {
        var options = new JsonSerializerOptions();
        options.Converters.Add(converter);
        return JsonSerializer.Deserialize<T>(json, options);
    }

    protected string SerializeToJson<T>(T value, JsonConverter converter) {
        var options = new JsonSerializerOptions();
        options.Converters.Add(converter);
        return JsonSerializer.Serialize(value, options);
    }

    protected void AssertLogContains(string expectedSubstring) {
        Assert.IsTrue(TestLogger.LogMessages.Any(msg => msg.Contains(expectedSubstring)),
            $"Expected log message containing '{expectedSubstring}' but found: {string.Join(", ", TestLogger.LogMessages)}");
    }

    protected void AssertLogLevel(LogLevel expectedLevel) {
        Assert.AreEqual(expectedLevel, TestLogger.LastLogLevel);
    }
}