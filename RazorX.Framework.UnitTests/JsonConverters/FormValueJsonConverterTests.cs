using System.Text.Json.Serialization;

namespace RazorX.Framework.UnitTests.JsonConverters;

/// <summary>
/// Tests the generic FormValueJsonConverter base class behavior
/// through concrete implementations
/// </summary>
[TestClass]
public sealed class FormValueJsonConverterTests : JsonConverterTestBase {
    [TestMethod]
    public void FormValueJsonConverter_LoggingBehavior_CorrectConverterName() {
        // Arrange
        SetupRxRequest();
        var converter = new IntJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        DeserializeFromJson<int?>("\"42\"", converter);

        // Assert
        AssertLogContains("IntJsonConverter");
        AssertLogContains("Read");
    }

    [TestMethod]
    public void FormValueJsonConverter_ErrorScenario_LogsFailure() {
        // Arrange
        SetupRxRequest();
        var converter = new IntJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<int?>("\"not-a-number\"", converter);

        // Assert
        Assert.IsNull(result);
        AssertLogContains("TryParse() failed");
        AssertLogContains("IntJsonConverter");
    }

    [TestMethod]
    public void FormValueJsonConverter_InputTrimming_WhitespaceRemoved() {
        // Test that input is properly trimmed before processing

        // Arrange
        SetupRxRequest();
        var converter = new IntJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<int?>("\"  42  \"", converter);

        // Assert
        Assert.AreEqual(42, result);
        AssertLogContains("overriding Deserialize for \"42\" results: 42");
    }

    [TestMethod]
    public void FormValueJsonConverter_NonRxRequest_FallsBackToDefaultDeserializer() {
        // Arrange
        SetupNonRxRequest();
        var converter = new IntJsonConverter(MockHttpContextAccessor, TestLogger);

        // Act
        var result = DeserializeFromJson<int?>("\"42\"", converter);

        // Assert
        Assert.AreEqual(42, result);
        AssertLogContains("called default JsonSerializer.Deserialize");
        AssertLogContains("IntJsonConverter");
    }




    [TestMethod]
    public void FormValueJsonConverter_TypeSafety_CorrectTypeReturned() {
        // Test that each converter returns the correct type

        SetupRxRequest();

        // Act & Assert
        var intResult = DeserializeFromJson<int?>("\"42\"", new IntJsonConverter(MockHttpContextAccessor, TestLogger));
        Assert.IsInstanceOfType(intResult, typeof(int?));

        TestLogger.Clear();
        var longResult = DeserializeFromJson<long?>("\"42\"", new LongJsonConverter(MockHttpContextAccessor, TestLogger));
        Assert.IsInstanceOfType(longResult, typeof(long?));

        TestLogger.Clear();
        var charResult = DeserializeFromJson<char?>("\"A\"", new CharJsonConverter(MockHttpContextAccessor, TestLogger));
        Assert.IsInstanceOfType(charResult, typeof(char?));

        TestLogger.Clear();
        var dateResult = DeserializeFromJson<DateOnly?>("\"2024-01-15\"", new DateOnlyJsonConverter(MockHttpContextAccessor, TestLogger));
        Assert.IsInstanceOfType(dateResult, typeof(DateOnly?));
    }

}