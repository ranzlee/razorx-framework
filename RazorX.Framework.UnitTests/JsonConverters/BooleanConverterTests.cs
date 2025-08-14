namespace RazorX.Framework.UnitTests.JsonConverters;

[TestClass]
public sealed class BooleanConverterTests : JsonConverterTestBase {
    [TestMethod]
    public void BooleanJsonConverter_WithRxRequest_TrueString_ReturnsTrue() {
        // Arrange
        SetupRxRequest();
        var converter = new BooleanJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<bool>("\"true\"", converter);

        // Assert
        Assert.IsTrue(result);
        AssertLogContains("overriding Deserialize");
    }

    [TestMethod]
    public void BooleanJsonConverter_WithRxRequest_FalseString_ReturnsFalse() {
        // Arrange
        SetupRxRequest();
        var converter = new BooleanJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<bool>("\"false\"", converter);

        // Assert
        Assert.IsFalse(result);
        AssertLogContains("overriding Deserialize");
    }

    [TestMethod]
    public void BooleanJsonConverter_WithRxRequest_OnString_ReturnsTrue() {
        // Arrange
        SetupRxRequest();
        var converter = new BooleanJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<bool>("\"on\"", converter);

        // Assert
        Assert.IsTrue(result);
        AssertLogContains("overriding Deserialize");
    }

    [TestMethod]
    public void BooleanJsonConverter_WithRxRequest_CaseInsensitive_HandledCorrectly() {
        // Arrange
        SetupRxRequest();
        var converter = new BooleanJsonConverter(MockHttpContextAccessor, TestLogger);

        // Test various cases
        var testCases = new[]
        {
            ("\"TRUE\"", true),
            ("\"False\"", false),
            ("\"ON\"", true),
            ("\"On\"", true)
        };

        foreach (var (input, expected) in testCases) {
            TestLogger.Clear();

            // Act
            var result = DeserializeFromJson<bool>(input, converter);

            // Assert
            Assert.AreEqual(expected, result, $"Input {input} should return {expected}");
        }
    }

    [TestMethod]
    public void BooleanJsonConverter_WithRxRequest_EmptyString_ReturnsFalse() {
        // Arrange
        SetupRxRequest();
        var converter = new BooleanJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<bool>("\"\"", converter);

        // Assert
        Assert.IsFalse(result);
        AssertLogContains("returned false for null/empty input");
    }

    [TestMethod]
    public void BooleanJsonConverter_WithRxRequest_WhitespaceString_ReturnsFalse() {
        // Arrange
        SetupRxRequest();
        var converter = new BooleanJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<bool>("\"   \"", converter);

        // Assert
        Assert.IsFalse(result);
        AssertLogContains("returned false for null/empty input");
    }

    [TestMethod]
    public void BooleanJsonConverter_WithRxRequest_InvalidString_ReturnsFalse() {
        // Arrange
        SetupRxRequest();
        var converter = new BooleanJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<bool>("\"invalid\"", converter);

        // Assert
        Assert.IsFalse(result);
        AssertLogContains("TryParse() failed");
    }

    [TestMethod]
    public void BooleanJsonConverter_WithoutRxRequest_UsesDefaultDeserializer() {
        // Arrange
        SetupNonRxRequest();
        var converter = new BooleanJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<bool>("\"true\"", converter);

        // Assert
        Assert.IsTrue(result);
        AssertLogContains("called default JsonSerializer.Deserialize");
    }

    [TestMethod]
    public void BooleanJsonConverter_NoHttpContext_UsesDefaultDeserializer() {
        // Arrange
        SetupNoHttpContext();
        var converter = new BooleanJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<bool>("\"true\"", converter);

        // Assert
        Assert.IsTrue(result);
        AssertLogContains("No HttpContext");
    }

    [TestMethod]
    public void BooleanJsonConverter_Write_TrueValue_WritesTrue() {
        // Arrange
        var converter = new BooleanJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var json = SerializeToJson(true, converter);

        // Assert
        Assert.AreEqual("true", json);
        AssertLogContains("writing True");
    }

    [TestMethod]
    public void BooleanJsonConverter_Write_FalseValue_WritesFalse() {
        // Arrange
        var converter = new BooleanJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var json = SerializeToJson(false, converter);

        // Assert
        Assert.AreEqual("false", json);
        AssertLogContains("writing False");
    }

    [TestMethod]
    public void BooleanJsonConverter_CheckboxScenario_OnValueHandledAsTrue() {
        // This tests the specific scenario where HTML checkboxes send "on" when checked

        // Arrange
        SetupRxRequest();
        var converter = new BooleanJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act - simulating a checked checkbox
        var checkedResult = DeserializeFromJson<bool>("\"on\"", converter);

        // Act - simulating an unchecked checkbox (empty or missing value)
        TestLogger.Clear();
        var uncheckedResult = DeserializeFromJson<bool>("\"\"", converter);

        // Assert
        Assert.IsTrue(checkedResult, "Checked checkbox should return true");
        Assert.IsFalse(uncheckedResult, "Unchecked checkbox should return false");
    }

    [TestMethod]
    public void BooleanJsonConverter_NonNullableBehavior_AlwaysReturnsValue() {
        // Unlike other converters which return nullable types, 
        // BooleanJsonConverter returns non-nullable bool

        // Arrange
        SetupRxRequest();
        var converter = new BooleanJsonConverter(MockHttpContextAccessor, TestLogger);

        var testCases = new[]
        {
            "\"true\"",
            "\"false\"",
            "\"on\"",
            "\"\"",
            "\"invalid\""
        };

        foreach (var testCase in testCases) {
            TestLogger.Clear();

            // Act
            var result = DeserializeFromJson<bool>(testCase, converter);

            // Assert - should never throw, always return a bool value
            Assert.IsTrue(result == true || result == false,
                $"Test case {testCase} should return a valid boolean value");
        }
    }
}