
using OrderFlow.Api.DTOs.Products;

namespace OrderFlow.Api.Services;

public interface IProductService
{
    Task<ProductResponse> CreateAsync(ProductCreateRequest request);

    Task<ProductResponse?> GetByIdAsync(int id);

    Task<(IEnumerable<ProductResponse> Items, int TotalCount)> GetAllAsync(
        string? search,
        int page,
        int pageSize);

    Task<ProductResponse?> UpdateAsync(
        int id,
        ProductUpdateRequest request);

    Task<bool> DeleteAsync(int id);
}