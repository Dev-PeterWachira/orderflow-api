using OrderFlow.Api.DTOs.Reports;
using OrderFlow.Api.DTOs.Common;

namespace OrderFlow.Api.Services;


public interface IReportService
{
Task<SalesSummaryResponse> GetSalesAsync(DateTime startDate,DateTime endDate,CancellationToken cancellationToken = default);

Task<IReadOnlyList<DailySalesResponse>> GetDailySalesAsync(DateTime startDate,DateTime endDate,CancellationToken cancellationToken = default);

Task<IReadOnlyList<ProductSalesResponse>> GetProductSalesAsync(DateTime startDate,DateTime endDate,CancellationToken cancellationToken = default);

Task<IReadOnlyList<ProductSalesResponse>> GetTopProductsAsync(DateTime startDate,DateTime endDate,int count = 10,CancellationToken cancellationToken = default);

Task<PagedResponse<CustomerOrderHistoryResponse>> GetCustomerOrderHistoryAsync(int customerId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default); 

}
