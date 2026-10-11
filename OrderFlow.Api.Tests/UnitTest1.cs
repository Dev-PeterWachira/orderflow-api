
using System.ComponentModel.DataAnnotations;
using OrderFlow.Api.DTOs.Customers;
using OrderFlow.Api.DTOs.Orders;
using OrderFlow.Api.DTOs.Products;

namespace OrderFlow.Api.Tests;

public class DtoValidationTests
{
    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model);

        Validator.TryValidateObject(
            model,
            context,
            results,
            validateAllProperties: true);

        return results;
    }

    [Fact]
    public void Product_WithNegativePrice_IsInvalid()
    {
        var request = new ProductCreateRequest
        {
            Name = "Keyboard",
            Price = -10,
            StockQuantity = 5
        };

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void Product_WithZeroPrice_IsInvalid()
    {
        var request = new ProductCreateRequest
        {
            Name = "Keyboard",
            Price = 0,
            StockQuantity = 5
        };

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void Product_WithNegativeStock_IsInvalid()
    {
        var request = new ProductCreateRequest
        {
            Name = "Keyboard",
            Price = 100,
            StockQuantity = -1
        };

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void Product_WithValidValues_IsValid()
    {
        var request = new ProductCreateRequest
        {
            Name = "Keyboard",
            Price = 4500,
            StockQuantity = 25
        };

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void Customer_WithInvalidEmail_IsInvalid()
    {
        var request = new CustomerCreateRequest
        {
            Name = "Test Customer",
            Email = "not-an-email"
        };

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void Order_WithZeroCustomerId_IsInvalid()
    {
        var request = new CreateOrderRequest
        {
            CustomerId = 0,
            Items =
            [
                new CreateOrderItemRequest
                {
                    ProductId = 1,
                    Quantity = 1
                }
            ]
        };

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void Order_WithNoItems_IsInvalid()
    {
        var request = new CreateOrderRequest
        {
            CustomerId = 1,
            Items = []
        };

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void OrderItem_WithZeroQuantity_IsInvalid()
    {
        var request = new CreateOrderItemRequest
        {
            ProductId = 1,
            Quantity = 0
        };

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void OrderItem_WithZeroProductId_IsInvalid()
    {
        var request = new CreateOrderItemRequest
        {
            ProductId = 0,
            Quantity = 1
        };

        Assert.NotEmpty(Validate(request));
    }
}