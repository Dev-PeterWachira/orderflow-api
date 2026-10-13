using Microsoft.AspNetCore.Mvc;
using OrderFlow.Api.DTOs.Reports;
using OrderFlow.Api.Services;
using OrderFlow.Api.DTOs.Common;

namespace OrderFlow.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController(IReportService reportService) : ControllerBase
{
[HttpGet("sales")]
[ProducesResponseType(typeof(SalesSummaryResponse), StatusCodes.Status200OK)]
public async Task<ActionResult<SalesSummaryResponse>> GetSales(
[FromQuery] DateTime startDate,
[FromQuery] DateTime endDate,
CancellationToken cancellationToken)
{
if (startDate >= endDate)
{
return BadRequest(new
{
message = "startDate must be earlier than endDate."
});
}

    var result = await reportService.GetSalesAsync(
        startDate, endDate, cancellationToken);

    return Ok(result);
}

[HttpGet("sales/daily")]
public async Task<ActionResult<IReadOnlyList<DailySalesResponse>>> GetDailySales(
    [FromQuery] DateTime startDate,
    [FromQuery] DateTime endDate,
    CancellationToken cancellationToken)
{
    if (startDate >= endDate)
    {
        return BadRequest(new
        {
            message = "startDate must be earlier than endDate."
        });
    }

    var result = await reportService.GetDailySalesAsync(
        startDate, endDate, cancellationToken);

    return Ok(result);
}

[HttpGet("sales/products")]
public async Task<ActionResult<IReadOnlyList<ProductSalesResponse>>> GetProductSales(
    [FromQuery] DateTime startDate,
    [FromQuery] DateTime endDate,
    CancellationToken cancellationToken)
{
    if (startDate >= endDate)
    {
        return BadRequest(new
        {
            message = "startDate must be earlier than endDate."
        });
    }

    var result = await reportService.GetProductSalesAsync(
        startDate, endDate, cancellationToken);

    return Ok(result);
}

[HttpGet("products/top")]
public async Task<ActionResult<IReadOnlyList<ProductSalesResponse>>> GetTopProducts(
    [FromQuery] DateTime startDate,
    [FromQuery] DateTime endDate,
    [FromQuery] int count = 10,
    CancellationToken cancellationToken = default)
{
    if (startDate >= endDate)
    {
        return BadRequest(new
        {
            message = "startDate must be earlier than endDate."
        });
    }

    if (count is < 1 or > 100)
    {
        return BadRequest(new
        {
            message = "count must be between 1 and 100."
        });
    }

    var result = await reportService.GetTopProductsAsync(
        startDate, endDate, count, cancellationToken);

    return Ok(result);
}


[HttpGet("customers/{customerId:int}/orders")]
public async Task<ActionResult<PagedResponse<CustomerOrderHistoryResponse>>>
    GetCustomerOrderHistory(
        int customerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
{
    if (customerId <= 0)
    {
        return BadRequest(new
        {
            message = "customerId must be greater than zero."
        });
    }

    if (page < 1)
    {
        return BadRequest(new
        {
            message = "page must be at least 1."
        });
    }

    if (pageSize is < 1 or > 100)
    {
        return BadRequest(new
        {
            message = "pageSize must be between 1 and 100."
        });
    }

    var result = await reportService.GetCustomerOrderHistoryAsync(
        customerId, page, pageSize, cancellationToken);

    return Ok(result);
}

}
