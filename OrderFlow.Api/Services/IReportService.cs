using OrderFlow.Api.DTOs.Reports;

namespace OrderFlow.Api.Services;

public interface IReportService
{
Task<SalesSummaryResponse> GetSalesAsync(DateTime startDate,DateTime endDate,CancellationToken cancellationToken = default);

Task<IReadOnlyList<DailySalesResponse>> GetDailySalesAsync(DateTime startDate,DateTime endDate,CancellationToken cancellationToken = default);

Task<IReadOnlyList<ProductSalesResponse>> GetProductSalesAsync(DateTime startDate,DateTime endDate,CancellationToken cancellationToken = default);

Task<IReadOnlyList<ProductSalesResponse>> GetTopProductsAsync(DateTime startDate,DateTime endDate,int count = 10,CancellationToken cancellationToken = default);

Task<IReadOnlyList<CustomerOrderHistoryResponse>> GetCustomerOrderHistoryAsync(int customerId,CancellationToken cancellationToken = default);

}
