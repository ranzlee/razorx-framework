namespace RazorX.Framework.UnitTests.JsonConverters;

[TestClass]
public sealed class NumericConverterTests : JsonConverterTestBase {
    [TestMethod]
    public void IntJsonConverter_WithRxRequest_ValidNumber_ReturnsValue() {
        // Arrange
        SetupRxRequest();
        var converter = new IntJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<int?>("\"42\"", converter);

        // Assert
        Assert.AreEqual(42, result);
        AssertLogContains("overriding Deserialize");
    }

    [TestMethod]
    public void IntJsonConverter_WithRxRequest_InvalidNumber_ReturnsNull() {
        // Arrange
        SetupRxRequest();
        var converter = new IntJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<int?>("\"invalid\"", converter);

        // Assert
        Assert.IsNull(result);
        AssertLogContains("TryParse() failed");
    }

    [TestMethod]
    public void IntJsonConverter_WithRxRequest_NullValue_ReturnsNull() {
        // Arrange
        SetupRxRequest();
        var converter = new IntJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<int?>("\"\"", converter);

        // Assert
        Assert.IsNull(result);
        AssertLogContains("returned null");
    }

    [TestMethod]
    public void IntJsonConverter_WithRxRequest_WhitespaceValue_ReturnsNull() {
        // Arrange
        SetupRxRequest();
        var converter = new IntJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<int?>("\"   \"", converter);

        // Assert
        Assert.IsNull(result);
        AssertLogContains("returned null");
    }

    [TestMethod]
    public void IntJsonConverter_WithoutRxRequest_UsesDefaultDeserializer() {
        // Arrange
        SetupNonRxRequest();
        var converter = new IntJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<int?>("\"42\"", converter);

        // Assert
        Assert.AreEqual(42, result);
        AssertLogContains("called default JsonSerializer.Deserialize");
    }

    [TestMethod]
    public void IntJsonConverter_NoHttpContext_UsesDefaultDeserializer() {
        // Arrange
        SetupNoHttpContext();
        var converter = new IntJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<int?>("\"42\"", converter);

        // Assert
        Assert.AreEqual(42, result);
        AssertLogContains("No HttpContext");
    }

    [TestMethod]
    public void IntJsonConverter_Write_NonNullValue_WritesNumber() {
        // Arrange
        var converter = new IntJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var json = SerializeToJson<int?>(42, converter);

        // Assert
        Assert.AreEqual("42", json);
        AssertLogContains("writing 42");
    }

    [TestMethod]
    public void IntJsonConverter_Write_NullValue_WritesNull() {
        // Arrange
        var converter = new IntJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var json = SerializeToJson<int?>(null, converter);

        // Assert
        Assert.AreEqual("null", json);
        AssertLogContains("writing null");
    }

    [TestMethod]
    public void IntJsonConverter_BoundaryValues_HandledCorrectly() {
        // Arrange
        SetupRxRequest();
        var converter = new IntJsonConverter(MockHttpContextAccessor, TestLogger);

        // Test max value
        var maxResult = DeserializeFromJson<int?>($"\"{int.MaxValue}\"", converter);
        Assert.AreEqual(int.MaxValue, maxResult);

        // Test min value
        TestLogger.Clear();
        var minResult = DeserializeFromJson<int?>($"\"{int.MinValue}\"", converter);
        Assert.AreEqual(int.MinValue, minResult);
    }

    [TestMethod]
    public void LongJsonConverter_WithRxRequest_ValidNumber_ReturnsValue() {
        // Arrange
        SetupRxRequest();
        var converter = new LongJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<long?>("\"42\"", converter);

        // Assert
        Assert.AreEqual(42L, result);
        AssertLogContains("overriding Deserialize");
    }

    [TestMethod]
    public void DecimalJsonConverter_WithRxRequest_ValidDecimal_ReturnsValue() {
        // Arrange
        SetupRxRequest();
        var converter = new DecimalJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<decimal?>("\"42.50\"", converter);

        // Assert
        Assert.AreEqual(42.50m, result);
        AssertLogContains("overriding Deserialize");
    }

    [TestMethod]
    public void DoubleJsonConverter_WithRxRequest_ValidDouble_ReturnsValue() {
        // Arrange
        SetupRxRequest();
        var converter = new DoubleJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<double?>("\"42.5\"", converter);

        // Assert
        Assert.AreEqual(42.5d, result);
        AssertLogContains("overriding Deserialize");
    }

    [TestMethod]
    public void FloatJsonConverter_WithRxRequest_ValidFloat_ReturnsValue() {
        // Arrange
        SetupRxRequest();
        var converter = new FloatJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<float?>("\"42.5\"", converter);

        // Assert
        Assert.AreEqual(42.5f, result);
        AssertLogContains("overriding Deserialize");
    }

    [TestMethod]
    public void ShortJsonConverter_WithRxRequest_ValidShort_ReturnsValue() {
        // Arrange
        SetupRxRequest();
        var converter = new ShortJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<short?>("\"42\"", converter);

        // Assert
        Assert.AreEqual((short)42, result);
        AssertLogContains("overriding Deserialize");
    }
}