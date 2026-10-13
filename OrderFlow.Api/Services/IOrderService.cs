using OrderFlow.Api.DTOs.Orders;

namespace OrderFlow.Api.Services;

public interface IOrderService
{
    Task<OrderResponse?> CreateAsync(CreateOrderRequest request, int customerId,CancellationToken cancellationToken);

    Task<OrderResponse?> GetByIdAsync(int id, int? customerId, CancellationToken cancellationToken = default);

    Task<(IEnumerable<OrderResponse> Items, int TotalCount)> GetAllAsync(int page,int pageSize, int? customerId, CancellationToken cancellationToken = default);

    Task<OrderResponse?> ProcessAsync(int orderID, CancellationToken cancellationToken = default);

    Task<OrderResponse?> CancelAsync(int orderId, int customerId, CancellationToken cancellationToken = default);
}