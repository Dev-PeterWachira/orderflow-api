
using OrderFlow.Api.Models;

namespace OrderFlow.Api.DTOs.Reports;

public class CustomerOrderHistoryResponse
{
    public int OrderId { get; set; }
    public DateTime CreatedAt { get; set; }
    public OrderStatus Status { get; set; }
    public int ItemsCount { get; set; }
    public decimal Total { get; set; }
}