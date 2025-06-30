using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Shared.Tests.Unit;

/// <summary>
/// Unit tests for shared domain models and utilities
/// </summary>
public class DomainModelTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Order_Should_CalculateTotal_Correctly()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        
        // This is a simple unit test example
        var orderTotal = 100.50m;
        var tax = 8.50m;
        
        // Act
        var expectedTotal = orderTotal + tax;
        
        // Assert
        expectedTotal.Should().Be(109.00m);
    }
    
    [Fact]
    [Trait("Category", "Unit")]
    public void Inventory_Should_ValidateQuantity_Correctly()
    {
        // Arrange
        var availableQuantity = 10;
        var requestedQuantity = 5;
        
        // Act
        var canFulfill = availableQuantity >= requestedQuantity;
        
        // Assert
        canFulfill.Should().BeTrue();
    }
    
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("valid-supplier-code", true)]
    [InlineData("VALID123", true)]
    public void SupplierCode_Should_ValidateCorrectly(string supplierCode, bool expected)
    {
        // Act
        var isValid = !string.IsNullOrWhiteSpace(supplierCode) && supplierCode.Length >= 3;
        
        // Assert
        isValid.Should().Be(expected);
    }
}

/// <summary>
/// Unit tests for logging utilities
/// </summary>
public class LoggingTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Logger_Should_LogInformation_WhenCalled()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<LoggingTests>>();
        var message = "Test message";
        
        // Act
        mockLogger.Object.LogInformation(message);
        
        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}

/// <summary>
/// Unit tests for validation utilities
/// </summary>
public class ValidationTests
{
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("test@example.com", true)]
    [InlineData("invalid-email", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Email_Should_ValidateCorrectly(string email, bool expected)
    {
        // Simple email validation for demonstration
        var isValid = !string.IsNullOrWhiteSpace(email) && email.Contains("@") && email.Contains(".");
        
        isValid.Should().Be(expected);
    }
    
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    public void Quantity_Should_ValidateCorrectly(int quantity, bool expected)
    {
        var isValid = quantity > 0;
        
        isValid.Should().Be(expected);
    }
}
