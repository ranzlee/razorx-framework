namespace RazorX.Framework.Tests.JsonConverters;

[TestClass]
public sealed class SingleOrArrayConverterTests : JsonConverterTestBase {
    [TestMethod]
    public void SingleOrArrayConverter_WithRxRequest_SingleValue_ReturnsArray() {
        // Arrange
        SetupRxRequest();
        var converter = new SingleOrArrayConverter<string>(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<IEnumerable<string>>("\"single-value\"", converter);

        // Assert
        Assert.IsNotNull(result);
        var array = result.ToArray();
        Assert.AreEqual(1, array.Length);
        Assert.AreEqual("single-value", array[0]);
    }

    [TestMethod]
    public void SingleOrArrayConverter_WithRxRequest_ArrayValue_ReturnsArray() {
        // Arrange
        SetupRxRequest();
        var converter = new SingleOrArrayConverter<string>(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<IEnumerable<string>>("[\"value1\", \"value2\", \"value3\"]", converter);

        // Assert
        Assert.IsNotNull(result);
        var array = result.ToArray();
        Assert.AreEqual(3, array.Length);
        Assert.AreEqual("value1", array[0]);
        Assert.AreEqual("value2", array[1]);
        Assert.AreEqual("value3", array[2]);
    }

    [TestMethod]
    public void SingleOrArrayConverter_WithRxRequest_NullValue_ReturnsNull() {
        // Arrange
        SetupRxRequest();
        var converter = new SingleOrArrayConverter<string>(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<IEnumerable<string>>("null", converter);

        // Assert
        Assert.IsNull(result);
        AssertLogContains("returned null");
    }

    [TestMethod]
    public void SingleOrArrayConverter_WithRxRequest_EmptyString_ReturnsNull() {
        // Arrange
        SetupRxRequest();
        var converter = new SingleOrArrayConverter<string>(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<IEnumerable<string>>("\"\"", converter);

        // Assert
        Assert.IsNull(result);
        AssertLogContains("returned null");
    }

    [TestMethod]
    public void SingleOrArrayConverter_WithRxRequest_WhitespaceString_ReturnsNull() {
        // Arrange
        SetupRxRequest();
        var converter = new SingleOrArrayConverter<string>(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<IEnumerable<string>>("\"   \"", converter);

        // Assert
        Assert.IsNull(result);
        AssertLogContains("returned null");
    }

    [TestMethod]
    public void SingleOrArrayConverter_WithRxRequest_EmptyArray_ReturnsEmptyArray() {
        // Arrange
        SetupRxRequest();
        var converter = new SingleOrArrayConverter<string>(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<IEnumerable<string>>("[]", converter);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count());
    }

    [TestMethod]
    public void SingleOrArrayConverter_WithoutRxRequest_UsesDefaultDeserializer() {
        // Arrange
        SetupNonRxRequest();
        var converter = new SingleOrArrayConverter<string>(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<IEnumerable<string>>("[\"single-value\"]", converter);

        // Assert
        Assert.IsNotNull(result);
        AssertLogContains("called default JsonSerializer.Deserialize");
    }

    [TestMethod]
    public void SingleOrArrayConverter_NoHttpContext_UsesDefaultDeserializer() {
        // Arrange
        SetupNoHttpContext();
        var converter = new SingleOrArrayConverter<string>(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<IEnumerable<string>>("[\"single-value\"]", converter);

        // Assert
        Assert.IsNotNull(result);
        AssertLogContains("No HttpContext");
    }

    [TestMethod]
    public void SingleOrArrayConverter_Write_Array_WritesJsonArray() {
        // Arrange
        var converter = new SingleOrArrayConverter<string>(MockHttpContextAccessor, TestLogger);
        var values = new[] { "value1", "value2", "value3" };

        // Act
        var json = SerializeToJson<IEnumerable<string>>(values, converter);

        // Assert
        StringAssert.Contains(json, "value1");
        StringAssert.Contains(json, "value2");
        StringAssert.Contains(json, "value3");
        AssertLogContains("writing");
    }

    [TestMethod]
    public void SingleOrArrayConverter_Write_NullValue_WritesNull() {
        // Arrange
        var converter = new SingleOrArrayConverter<string>(MockHttpContextAccessor, TestLogger);

        // Act
        var json = SerializeToJson<IEnumerable<string>>(null!, converter);

        // Assert
        Assert.AreEqual("null", json);
        AssertLogContains("writing null");
    }

    [TestMethod]
    public void SingleOrArrayConverter_IntegerType_HandlesCorrectly() {
        // Test with non-string types

        // Arrange
        SetupRxRequest();
        var converter = new SingleOrArrayConverter<int>(MockHttpContextAccessor, TestLogger);

        // Test single integer
        var singleResult = DeserializeFromJson<IEnumerable<int>>("42", converter);
        Assert.IsNotNull(singleResult);
        Assert.AreEqual(1, singleResult.Count());
        Assert.AreEqual(42, singleResult.First());

        // Test array of integers
        TestLogger.Clear();
        var arrayResult = DeserializeFromJson<IEnumerable<int>>("[1, 2, 3]", converter);
        Assert.IsNotNull(arrayResult);
        var intArray = arrayResult.ToArray();
        Assert.AreEqual(3, intArray.Length);
        Assert.AreEqual(1, intArray[0]);
        Assert.AreEqual(2, intArray[1]);
        Assert.AreEqual(3, intArray[2]);
    }

    [TestMethod]
    public void SingleOrArrayConverter_FormSubmission_MultipleValues_HandledCorrectly() {
        // This tests the real-world scenario where HTML forms submit multiple values
        // for the same field name (like multiple checkboxes or select multiple)

        // Arrange
        SetupRxRequest();
        var converter = new SingleOrArrayConverter<string>(MockHttpContextAccessor, TestLogger);

        // Act - Simulate form data with multiple values
        var result = DeserializeFromJson<IEnumerable<string>>("[\"option1\", \"option2\", \"option3\"]", converter);

        // Assert
        Assert.IsNotNull(result);
        var options = result.ToArray();
        Assert.AreEqual(3, options.Length);
        Assert.AreEqual("option1", options[0]);
        Assert.AreEqual("option2", options[1]);
        Assert.AreEqual("option3", options[2]);
    }

    [TestMethod]
    public void SingleOrArrayConverter_FormSubmission_SingleValue_HandledCorrectly() {
        // This tests the scenario where a form field that could have multiple values
        // only has a single value (like a single checkbox checked)

        // Arrange
        SetupRxRequest();
        var converter = new SingleOrArrayConverter<string>(MockHttpContextAccessor, TestLogger);

        // Act - Simulate form data with single value
        var result = DeserializeFromJson<IEnumerable<string>>("\"single-option\"", converter);

        // Assert
        Assert.IsNotNull(result);
        var options = result.ToArray();
        Assert.AreEqual(1, options.Length);
        Assert.AreEqual("single-option", options[0]);
    }

    [TestMethod]
    public void SingleOrArrayConverter_Write_EmptyCollection_WritesEmptyArray() {
        // Arrange
        var converter = new SingleOrArrayConverter<string>(MockHttpContextAccessor, TestLogger);
        var emptyArray = Array.Empty<string>();

        // Act
        var json = SerializeToJson<IEnumerable<string>>(emptyArray, converter);

        // Assert
        Assert.AreEqual("[]", json);
        AssertLogContains("writing");
    }
}