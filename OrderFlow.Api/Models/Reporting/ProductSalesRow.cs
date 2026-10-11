namespace OrderFlow.Api.Models.Reporting;

public class ProductSalesRow
{
public int ProductId { get; set; }
public string ProductName { get; set; } = string.Empty;
public int ItemsSold { get; set; }
public decimal Revenue { get; set; }
}
