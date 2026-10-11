namespace OrderFlow.Api.Models.Reporting;

public class CustomerOrderHistoryRow
{
public int OrderId { get; set; }
public DateTime CreatedAt { get; set; }
public int Status { get; set; }
public int ItemsCount { get; set; }
public decimal Total { get; set; }
}
