using OrderFlow.Api.DTOs.Orders;

namespace OrderFlow.Api.Services;

public interface IOrderService
{
    Task<OrderResponse?> CreateAsync(CreateOrderRequest request);

    Task<OrderResponse?> GetByIdAsync(int id);

    Task<(IEnumerable<OrderResponse> Items, int TotalCount)> GetAllAsync(
        int page,
        int pageSize);
}