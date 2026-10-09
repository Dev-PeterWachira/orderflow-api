using OrderFlow.Api.Models;

namespace OrderFlow.Api.DTOs.Orders;

public class OrderResponse
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal Total { get; set; }
    public List<OrderItemResponse> Items { get; set; } = [];
}