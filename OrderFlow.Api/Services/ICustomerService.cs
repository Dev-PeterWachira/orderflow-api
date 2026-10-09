using OrderFlow.Api.DTOs.Customers;

namespace OrderFlow.Api.Services;

public interface ICustomerService
{
    Task<CustomerResponse> CreateAsync(CustomerCreateRequest request);
    Task<CustomerResponse?> GetByIdAsync(int id);
}