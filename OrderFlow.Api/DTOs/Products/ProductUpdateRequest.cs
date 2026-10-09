using System.ComponentModel.DataAnnotations;

namespace OrderFlow.Api.DTOs.Products;

public class ProductUpdateRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }
}
