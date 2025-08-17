namespace RazorX.Framework.Tests.JsonConverters;

[TestClass]
public sealed class DateTimeConverterTests : JsonConverterTestBase {
    [TestMethod]
    public void DateOnlyJsonConverter_WithRxRequest_ValidDate_ReturnsValue() {
        // Arrange
        SetupRxRequest();
        var converter = new DateOnlyJsonConverter(MockHttpContextAccessor, TestLogger);
        var expected = new DateOnly(2024, 1, 15);

        // Act
        var result = DeserializeFromJson<DateOnly?>("\"2024-01-15\"", converter);

        // Assert
        Assert.AreEqual(expected, result);
        AssertLogContains("overriding Deserialize");
    }

    [TestMethod]
    public void DateOnlyJsonConverter_WithRxRequest_InvalidDate_ReturnsNull() {
        // Arrange
        SetupRxRequest();
        var converter = new DateOnlyJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<DateOnly?>("\"invalid-date\"", converter);

        // Assert
        Assert.IsNull(result);
        AssertLogContains("TryParse() failed");
    }

    [TestMethod]
    public void DateOnlyJsonConverter_Write_ValidDate_WritesIsoString() {
        // Arrange
        var converter = new DateOnlyJsonConverter(MockHttpContextAccessor, TestLogger);
        var date = new DateOnly(2024, 1, 15);

        // Act
        var json = SerializeToJson<DateOnly?>(date, converter);

        // Assert
        Assert.IsTrue(json.Contains("2024-01-15"));
        AssertLogContains("writing");
    }

    [TestMethod]
    public void DateOnlyJsonConverter_Write_NullValue_WritesNull() {
        // Arrange
        var converter = new DateOnlyJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var json = SerializeToJson<DateOnly?>(null, converter);

        // Assert
        Assert.AreEqual("null", json);
        AssertLogContains("writing null");
    }

    [TestMethod]
    public void DateTimeJsonConverter_WithRxRequest_ValidDateTime_ReturnsValue() {
        // Arrange
        SetupRxRequest();
        var converter = new DateTimeJsonConverter(MockHttpContextAccessor, TestLogger);
        var expected = new DateTime(2024, 1, 15, 14, 30, 0);

        // Act
        var result = DeserializeFromJson<DateTime?>("\"2024-01-15T14:30:00\"", converter);

        // Assert
        Assert.AreEqual(expected, result);
        AssertLogContains("overriding Deserialize");
    }

    [TestMethod]
    public void DateTimeJsonConverter_WithRxRequest_InvalidDateTime_ReturnsNull() {
        // Arrange
        SetupRxRequest();
        var converter = new DateTimeJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<DateTime?>("\"invalid-datetime\"", converter);

        // Assert
        Assert.IsNull(result);
        AssertLogContains("TryParse() failed");
    }

    [TestMethod]
    public void DateTimeJsonConverter_WithRxRequest_EmptyString_ReturnsNull() {
        // Arrange
        SetupRxRequest();
        var converter = new DateTimeJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<DateTime?>("\"\"", converter);

        // Assert
        Assert.IsNull(result);
        AssertLogContains("returned null");
    }

    [TestMethod]
    public void DateTimeJsonConverter_WithoutRxRequest_UsesDefaultDeserializer() {
        // Arrange
        SetupNonRxRequest();
        var converter = new DateTimeJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<DateTime?>("\"2024-01-15T14:30:00\"", converter);

        // Assert
        Assert.IsNotNull(result);
        AssertLogContains("called default JsonSerializer.Deserialize");
    }

    [TestMethod]
    public void DateTimeJsonConverter_Write_ValidDateTime_WritesIsoString() {
        // Arrange
        var converter = new DateTimeJsonConverter(MockHttpContextAccessor, TestLogger);
        var dateTime = new DateTime(2024, 1, 15, 14, 30, 0);

        // Act
        var json = SerializeToJson<DateTime?>(dateTime, converter);

        // Assert
        Assert.IsTrue(json.Contains("2024-01-15"));
        AssertLogContains("writing");
    }

    [TestMethod]
    public void DateTimeJsonConverter_Write_NullValue_WritesNull() {
        // Arrange
        var converter = new DateTimeJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var json = SerializeToJson<DateTime?>(null, converter);

        // Assert
        Assert.AreEqual("null", json);
        AssertLogContains("writing null");
    }

    [TestMethod]
    public void DateOnlyJsonConverter_DifferentFormats_Handled() {
        // Arrange
        SetupRxRequest();
        var converter = new DateOnlyJsonConverter(MockHttpContextAccessor, TestLogger);

        // Test various date formats that DateOnly.TryParse can handle
        var testCases = new[]
        {
            "\"1/15/2024\"",
            "\"15/01/2024\"",
            "\"2024-01-15\"",
            "\"January 15, 2024\""
        };

        foreach (var testCase in testCases) {
            TestLogger.Clear();

            // Act
            var result = DeserializeFromJson<DateOnly?>(testCase, converter);

            // Assert - should either parse successfully or return null
            // We're not testing specific formats, just that the converter handles them gracefully
            Assert.IsTrue(result.HasValue || TestLogger.LogMessages.Any(m => m.Contains("TryParse() failed")),
                $"Test case {testCase} should either parse or log failure");
        }
    }
}