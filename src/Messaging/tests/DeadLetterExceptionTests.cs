using Femur.Messaging;

namespace Femur.Messaging.Tests;

public class DeadLetterExceptionTests
{
    [Fact]
    public void Constructor_ReasonOnly_SetsReason()
    {
        // Arrange & Act
        var ex = new DeadLetterException("InvalidFormat");

        // Assert
        Assert.Equal("InvalidFormat", ex.Reason);
        Assert.Equal("InvalidFormat", ex.Message);
        Assert.Null(ex.Description);
        Assert.Null(ex.PropertiesToModify);
    }

    [Fact]
    public void Constructor_ReasonAndDescription_SetsBoth()
    {
        // Arrange & Act
        var ex = new DeadLetterException("InvalidFormat", "The order amount is negative");

        // Assert
        Assert.Equal("InvalidFormat", ex.Reason);
        Assert.Equal("The order amount is negative", ex.Description);
        Assert.Equal("The order amount is negative", ex.Message);
        Assert.Null(ex.PropertiesToModify);
    }

    [Fact]
    public void Constructor_ReasonDescriptionAndProperties_SetsAll()
    {
        // Arrange
        var properties = new Dictionary<string, object>
        {
            ["Severity"] = "High",
            ["ErrorCode"] = 1001
        };

        // Act
        var ex = new DeadLetterException("ValidationFailed", "Failed validation rules", properties);

        // Assert
        Assert.Equal("ValidationFailed", ex.Reason);
        Assert.Equal("Failed validation rules", ex.Description);
        Assert.NotNull(ex.PropertiesToModify);
        Assert.Equal(2, ex.PropertiesToModify!.Count);
        Assert.Equal("High", ex.PropertiesToModify["Severity"]);
        Assert.Equal(1001, ex.PropertiesToModify["ErrorCode"]);
    }

    [Fact]
    public void Constructor_ReasonAndInnerException_SetsReasonAndDescription()
    {
        // Arrange
        var innerException = new ArgumentException("Invalid argument provided");

        // Act
        var ex = new DeadLetterException("ProcessingFailed", innerException);

        // Assert
        Assert.Equal("ProcessingFailed", ex.Reason);
        Assert.Equal("Invalid argument provided", ex.Description);
        Assert.Same(innerException, ex.InnerException);
    }

    [Fact]
    public void Constructor_ReasonWithNullDescription_UsesReasonAsMessage()
    {
        // Arrange & Act
        var ex = new DeadLetterException("InvalidData", (string?)null);

        // Assert
        Assert.Equal("InvalidData", ex.Reason);
        Assert.Null(ex.Description);
        Assert.Equal("InvalidData", ex.Message);
    }

    [Fact]
    public void Constructor_WithProperties_PropertiesAreMutable()
    {
        // Arrange
        var properties = new Dictionary<string, object>
        {
            ["Key1"] = "Value1"
        };

        var ex = new DeadLetterException("Test", "Description", properties);

        // Act
        ex.PropertiesToModify!["Key2"] = "Value2";

        // Assert
        Assert.Equal(2, ex.PropertiesToModify.Count);
        Assert.Equal("Value2", ex.PropertiesToModify["Key2"]);
    }
}
