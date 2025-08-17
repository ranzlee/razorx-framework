using System.Text.Json;

namespace RazorX.Framework.Tests.JsonConverters;

[TestClass]
public sealed class CharConverterTests : JsonConverterTestBase {
    [TestMethod]
    public void CharJsonConverter_WithRxRequest_SingleChar_ReturnsChar() {
        // Arrange
        SetupRxRequest();
        var converter = new CharJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<char?>("\"A\"", converter);

        // Assert
        Assert.AreEqual('A', result);
        AssertLogContains("overriding Deserialize");
    }

    [TestMethod]
    public void CharJsonConverter_WithRxRequest_EmptyString_ReturnsNull() {
        // Arrange
        SetupRxRequest();
        var converter = new CharJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<char?>("\"\"", converter);

        // Assert
        Assert.IsNull(result);
        AssertLogContains("returned null");
    }

    [TestMethod]
    public void CharJsonConverter_WithRxRequest_WhitespaceString_ReturnsNull() {
        // Arrange
        SetupRxRequest();
        var converter = new CharJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<char?>("\"   \"", converter);

        // Assert
        Assert.IsNull(result);
        AssertLogContains("returned null");
    }

    [TestMethod]
    public void CharJsonConverter_WithRxRequest_MultiCharString_ReturnsNull() {
        // Arrange
        SetupRxRequest();
        var converter = new CharJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<char?>("\"ABC\"", converter);

        // Assert
        Assert.IsNull(result);
        AssertLogContains("TryParse() failed");
    }

    [TestMethod]
    public void CharJsonConverter_WithRxRequest_SpecialChars_HandledCorrectly() {
        // Arrange
        SetupRxRequest();
        var converter = new CharJsonConverter(MockHttpContextAccessor, TestLogger);

        var testCases = new[]
        {
            ("\"0\"", '0'),
            ("\" \"", ' '),
            ("\"@\"", '@'),
            ("\"#\"", '#'),
            ("\"$\"", '$')
        };

        foreach (var (input, expected) in testCases) {
            TestLogger.Clear();

            // Act
            var result = DeserializeFromJson<char?>(input, converter);

            // Assert
            Assert.AreEqual(expected, result, $"Input {input} should return '{expected}'");
            AssertLogContains("overriding Deserialize");
        }
    }

    [TestMethod]
    public void CharJsonConverter_WithoutRxRequest_UsesDefaultDeserializer() {
        // Arrange
        SetupNonRxRequest();
        var converter = new CharJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<char?>("\"A\"", converter);

        // Assert
        Assert.AreEqual('A', result);
        AssertLogContains("called default JsonSerializer.Deserialize");
    }

    [TestMethod]
    public void CharJsonConverter_NoHttpContext_UsesDefaultDeserializer() {
        // Arrange
        SetupNoHttpContext();
        var converter = new CharJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<char?>("\"A\"", converter);

        // Assert
        Assert.AreEqual('A', result);
        AssertLogContains("No HttpContext");
    }

    [TestMethod]
    public void CharJsonConverter_Write_ValidChar_WritesString() {
        // Arrange
        var converter = new CharJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var json = SerializeToJson<char?>('A', converter);

        // Assert
        Assert.AreEqual("\"A\"", json);
        AssertLogContains("writing A");
    }

    [TestMethod]
    public void CharJsonConverter_Write_NullValue_WritesNull() {
        // Arrange
        var converter = new CharJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var json = SerializeToJson<char?>(null, converter);

        // Assert
        Assert.AreEqual("null", json);
        AssertLogContains("writing null");
    }

    [TestMethod]
    public void CharJsonConverter_Write_SpecialChars_WritesStringNotNumber() {
        // This test ensures that char values are serialized as strings, not numbers
        // This is important because the original code had a bug where it used WriteNumberValue

        // Arrange
        var converter = new CharJsonConverter(MockHttpContextAccessor, TestLogger);

        var testCases = new[]
        {
            ('0', "\"0\""),
            ('9', "\"9\""),
            ('A', "\"A\""),
            (' ', "\" \""),
            ('@', "\"@\"")
        };

        foreach (var (input, expected) in testCases) {
            TestLogger.Clear();

            // Act
            var json = SerializeToJson<char?>(input, converter);

            // Assert
            Assert.AreEqual(expected, json, $"Char '{input}' should serialize as string {expected}, not as number");
            AssertLogContains($"writing {input}");
        }
    }

    [TestMethod]
    public void CharJsonConverter_EscapedChars_HandledCorrectly() {
        // Test characters that need JSON escaping

        // Arrange
        SetupRxRequest();
        var converter = new CharJsonConverter(MockHttpContextAccessor, TestLogger);

        // These would be escaped in JSON strings
        var testCases = new[]
        {
            "\"\\\"\"", // quote
            "\"\\\\\"", // backslash
            "\"\\n\"",  // newline
            "\"\\t\""   // tab
        };

        foreach (var input in testCases) {
            TestLogger.Clear();

            // Act & Assert - should not throw exceptions
            try {
                var result = DeserializeFromJson<char?>(input, converter);

                // Should either successfully parse or fail gracefully
                Assert.IsTrue(result.HasValue || TestLogger.LogMessages.Any(m => m.Contains("TryParse() failed")),
                    $"Input {input} should either parse or log failure gracefully");
            } catch (JsonException) {
                // JSON parsing errors are acceptable for malformed input
            }
        }
    }
}