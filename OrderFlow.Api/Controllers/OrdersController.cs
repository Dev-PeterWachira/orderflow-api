using Microsoft.AspNetCore.Mvc;
using OrderFlow.Api.DTOs.Orders;
using OrderFlow.Api.Services;

namespace OrderFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(
        CreateOrderRequest request)
    {
        try
        {
            var order = await _orderService.CreateAsync(request);

            if (order is null)
                return NotFound("Customer was not found.");

            return CreatedAtAction(
                nameof(GetById),
                new { id = order.Id },
                order);
        }
        catch (InvalidOrderException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderResponse>> GetById(int id)
    {
        var order = await _orderService.GetByIdAsync(id);

        if (order is null)
            return NotFound();

        return Ok(order);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(
                "Page must be positive and pageSize must be between 1 and 100.");

        var result = await _orderService.GetAllAsync(page, pageSize);

        return Ok(new
        {
            result.Items,
            result.TotalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(
                result.TotalCount / (double)pageSize)
        });
    }
}