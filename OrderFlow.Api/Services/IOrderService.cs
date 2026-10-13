using OrderFlow.Api.DTOs.Orders;

namespace OrderFlow.Api.Services;

public interface IOrderService
{
    Task<OrderResponse?> CreateAsync(CreateOrderRequest request, int customerId);

    Task<OrderResponse?> GetByIdAsync(int id, int? customerId);

    Task<(IEnumerable<OrderResponse> Items, int TotalCount)> GetAllAsync(int page,int pageSize, int? customerId);

    Task<OrderResponse?> ProcessAsync(int orderID, CancellationToken cancellationToken = default);
}