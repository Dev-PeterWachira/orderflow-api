using Microsoft.EntityFrameworkCore;
using OrderFlow.Api.Data;
using OrderFlow.Api.DTOs.Customers;
using OrderFlow.Api.Models;

namespace OrderFlow.Api.Services;

public class CustomerService : ICustomerService
{
    private readonly OrderFlowDbContext _context;

    public CustomerService(OrderFlowDbContext context)
    {
        _context = context;
    }

    public async Task<CustomerResponse> CreateAsync(CustomerCreateRequest request)
    {
        var customer = new Customer
        {
            Name = request.Name,
            Email = request.Email
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        return MapToResponse(customer);
    }

    public async Task<CustomerResponse?> GetByIdAsync(int id)
    {
        var customer = await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        return customer is null ? null : MapToResponse(customer);
    }

    private static CustomerResponse MapToResponse(Customer customer)
    {
        return new CustomerResponse
        {
            Id = customer.Id,
            Name = customer.Name,
            Email = customer.Email
        };
    }
}