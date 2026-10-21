using FluentAssertions;
using SureBudgetRequest.Domain.Common;
using Xunit;

namespace SureBudgetRequest.Domain.Tests.Common;

public class ResultTests
{
    // =========================================================================
    // 1. Error Record Tests
    // =========================================================================

    [Theory]
    [InlineData("User.NotFound", "The user was not found.", ErrorType.NotFound)]
    [InlineData("Budget.Invalid", "The budget is negative.", ErrorType.Validation)]
    [InlineData("Request.Conflict", "Concurrency collision.", ErrorType.Conflict)]
    [InlineData("Auth.Unauthorized", "Login required.", ErrorType.Unauthorized)]
    [InlineData("Auth.Forbidden", "Access denied.", ErrorType.Forbidden)]
    [InlineData("System.Failure", "Unexpected error.", ErrorType.Failure)]
    public void Error_FactoryMethods_ShouldInitializeCorrectProperties(string code, string message, ErrorType type)
    {
        // Act
        Error error = type switch
        {
            ErrorType.NotFound => Error.NotFound(code, message),
            ErrorType.Validation => Error.Validation(code, message),
            ErrorType.Conflict => Error.Conflict(code, message),
            ErrorType.Unauthorized => Error.Unauthorized(code, message),
            ErrorType.Forbidden => Error.Forbidden(code, message),
            ErrorType.Failure => Error.Failure(code, message),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        // Assert
        error.Code.Should().Be(code);
        error.Message.Should().Be(message);
        error.Type.Should().Be(type);
    }

    [Fact]
    public void Error_None_ShouldHaveEmptyCodeAndMessage_WithFailureType()
    {
        // Assert
        Error.None.Code.Should().BeEmpty();
        Error.None.Message.Should().BeEmpty();
        Error.None.Type.Should().Be(ErrorType.Failure);
    }

    [Fact]
    public void Error_NullValue_ShouldHavePredefinedProperties()
    {
        // Assert
        Error.NullValue.Code.Should().Be("Error.NullValue");
        Error.NullValue.Message.Should().Be("The specified result value is null.");
        Error.NullValue.Type.Should().Be(ErrorType.Failure);
    }

    [Fact]
    public void Error_Equality_ShouldRespectValueSemantics()
    {
        // Arrange
        var error1 = Error.Validation("Code1", "Message1");
        var error2 = Error.Validation("Code1", "Message1");
        var errorDifferentCode = Error.Validation("Code2", "Message1");
        var errorDifferentType = Error.Conflict("Code1", "Message1");

        // Assert
        error1.Should().Be(error2);
        (error1 == error2).Should().BeTrue();
        (error1 != errorDifferentCode).Should().BeTrue();
        (error1 != errorDifferentType).Should().BeTrue();
        error1.GetHashCode().Should().Be(error2.GetHashCode());
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public void Error_EdgeCases_WithEmptyOrWhitespaceStrings_ShouldNotThrow(string code, string message)
    {
        // Act
        var error = Error.Failure(code, message);

        // Assert
        error.Code.Should().Be(code);
        error.Message.Should().Be(message);
        error.Type.Should().Be(ErrorType.Failure);
    }

    // =========================================================================
    // 2. Result (Non-Generic) Tests
    // =========================================================================

    [Fact]
    public void Result_Success_ShouldHaveIsSuccessTrue_IsFailureFalse_AndErrorNone()
    {
        // Act
        var result = Result.Success();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Result_Failure_WithError_ShouldHaveIsSuccessFalse_IsFailureTrue_AndPreserveError()
    {
        // Arrange
        var expectedError = Error.NotFound("Item.NotFound", "The item does not exist.");

        // Act
        var result = Result.Failure(expectedError);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(expectedError);
    }

    [Theory]
    [InlineData("Database connection timed out.")]
    [InlineData("")]
    [InlineData("   ")]
    public void Result_Failure_WithStringMessage_ShouldUseGeneralErrorCode(string errorMessage)
    {
        // Act
        var result = Result.Failure(errorMessage);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("General.Error");
        result.Error.Message.Should().Be(errorMessage);
        result.Error.Type.Should().Be(ErrorType.Failure);
    }

    private sealed class SubclassedResult : Result
    {
        public SubclassedResult(bool isSuccess, Error error) : base(isSuccess, error) { }
    }

    [Fact]
    public void Result_ConstructorInvariant_SuccessWithNonNoneError_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var customError = Error.Validation("Val.Error", "Validation failed.");

        // Act
        Action act = () => new SubclassedResult(true, customError);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Successful result cannot have an error.");
    }

    [Fact]
    public void Result_ConstructorInvariant_FailureWithErrorNone_ShouldThrowInvalidOperationException()
    {
        // Act
        Action act = () => new SubclassedResult(false, Error.None);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Failed result must have an error.");
    }

    // =========================================================================
    // 3. Result<T> (Generic) Tests
    // =========================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(42)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void ResultT_Success_WithIntegerValues_ShouldPreserveValue(int value)
    {
        // Act
        var result = Result.Success(value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(value);
        result.Error.Should().Be(Error.None);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResultT_Success_WithBooleans_ShouldPreserveValue(bool value)
    {
        // Act
        var result = Result.Success(value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(value);
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void ResultT_Success_WithDecimals_ShouldPreserveValueAndPrecision()
    {
        // Arrange
        const decimal value = 1234567.8901m;

        // Act
        var result = Result.Success(value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(value);
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void ResultT_Success_WithGuid_ShouldPreserveGuid()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var result = Result.Success(guid);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(guid);
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void ResultT_Success_WithGuidEmpty_ShouldPreserveEmptyGuid()
    {
        // Act
        var result = Result.Success(Guid.Empty);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Guid.Empty);
    }

    [Theory]
    [InlineData("Valid test string")]
    [InlineData("")]
    [InlineData("    ")]
    public void ResultT_Success_WithStringValues_ShouldPreserveValue(string value)
    {
        // Act
        var result = Result.Success(value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(value);
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void ResultT_Success_WithNullReference_ShouldSucceedWithNullValue()
    {
        // Act
        var result = Result.Success<string?>(null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void ResultT_Failure_WithError_ShouldHaveDefaultValue_AndPreserveError()
    {
        // Arrange
        var error = Error.Validation("User.InvalidAge", "Age must be greater than zero.");

        // Act
        var result = Result.Failure<int>(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Value.Should().Be(0); // default(int)
        result.Error.Should().Be(error);
    }

    [Fact]
    public void ResultT_Failure_WithReferenceType_ShouldHaveNullValue()
    {
        // Arrange
        var error = Error.NotFound("Order.NotFound", "Order does not exist.");

        // Act
        var result = Result.Failure<string>(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Value.Should().BeNull();
        result.Error.Should().Be(error);
    }

    [Theory]
    [InlineData("Something went wrong.")]
    [InlineData("")]
    public void ResultT_Failure_WithStringMessage_ShouldUseGeneralErrorCode(string message)
    {
        // Act
        var result = Result.Failure<double>(message);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Value.Should().Be(0.0);
        result.Error.Code.Should().Be("General.Error");
        result.Error.Message.Should().Be(message);
    }

    [Fact]
    public void ResultT_ImplicitConversion_FromValue_ShouldReturnSuccessfulResult()
    {
        // Arrange
        const string expectedValue = "Implicit conversion payload";

        // Act
        Result<string> result = expectedValue;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(expectedValue);
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void ResultT_ImplicitConversion_FromValueType_ShouldReturnSuccessfulResult()
    {
        // Arrange
        const int expectedValue = 999;

        // Act
        Result<int> result = expectedValue;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedValue);
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void ResultT_ImplicitConversion_FromNull_ShouldReturnSuccessfulResultWithNullValue()
    {
        // Act
        Result<string?> result = (string?)null;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        result.Error.Should().Be(Error.None);
    }
}
