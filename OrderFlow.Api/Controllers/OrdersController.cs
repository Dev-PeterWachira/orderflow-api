using Microsoft.AspNetCore.Mvc;
using OrderFlow.Api.DTOs.Orders;
using OrderFlow.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace OrderFlow.Api.Controllers;
[Authorize]
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
    public async Task<ActionResult<OrderResponse>> Create(CreateOrderRequest request)
    {
        try
        {

            var customerId = GetAuthenticatedCustomerId();

            if(!customerId.HasValue)
               return Forbid();

            var order = await _orderService.CreateAsync(request, customerId.Value);

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

        var customerId = IsEmployee ? null : GetAuthenticatedCustomerId();
        
        if(!IsEmployee && !customerId.HasValue)
           return Forbid();

        var order = await _orderService.GetByIdAsync(id, customerId);

        if (order is null)
            return NotFound();

        return Ok(order);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
          
          var customerId = IsEmployee ? null : GetAuthenticatedCustomerId();

          if(!IsEmployee && !customerId.HasValue)
             return Forbid();


        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(
                "Page must be positive and pageSize must be between 1 and 100.");

        var result = await _orderService.GetAllAsync(page,pageSize,customerId);

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

  [Authorize(Roles = "Employee")]  
 [HttpPost("{id:int}/process")]
 public async Task<ActionResult<OrderResponse>> Process(int id,CancellationToken cancellationToken)
 {
    try
    {
        var order = await _orderService.ProcessAsync(id, cancellationToken);

        if (order is null)
            return NotFound();

        return Ok(order);
    }
    catch (InvalidOrderException ex)
    {
        return Conflict(new { error = ex.Message });
    }
    catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                error = "The order was modified by another request. Please refresh and try again."
            });
        }
 }


   private int? GetAuthenticatedCustomerId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(value, out var customerId) ? customerId : null;
    }

    private bool IsEmployee => User.IsInRole("Employee");
 }