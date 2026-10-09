using System.Runtime.Versioning;
using Microsoft.AspNetCore;
using OrderFlow.Api.DTOs.Customers;
using OrderFlow.Api.Services;

namespace OrderFlow.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]

    public class CustomersController : ControllerBase
    {
        private readonly ICustomerService _customerService;

        public CustomersController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        [HttpPost]
        public async Task<ActionResult<CustomerResponse>> Create(CustomerCreateRequest request)

        {
            var customer = await _customerService.CreateAsnc(request);

            return CreatedAtAction(
                nameof(GetById),
                new {id = customer.Id},
                customer
            );
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<CustomerResponse>> GetById(int id)
        {
            var customer = await _customerService.GetByIdAsync(id);

            if(customer is null)
               return NotFound();

           return Ok(customer);
        }
    }
}