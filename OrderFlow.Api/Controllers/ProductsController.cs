using Microsoft.AspNetCore.Mvc;
using OrderFlow.Api.DTOs.Products;
using OrderFlow.Api.Services;
using Microsoft.AspNetCore.Authorization;

namespace OrderFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }
    [Authorize(Roles = "Employee")]
    [HttpPost]
public async Task<ActionResult<ProductResponse>> Create(ProductCreateRequest request)
{
    var product = await _productService.CreateAsync(request);

    return CreatedAtAction(
        nameof(GetById),
        new { id = product.Id },
        product);
}

[HttpGet("{id:int}")]
public async Task<ActionResult<ProductResponse>> GetById(int id)
{
    var product = await _productService.GetByIdAsync(id);

    if (product is null)
        return NotFound();

    return Ok(product);
}

[HttpGet]
public async Task<IActionResult> GetAll(
    [FromQuery] string? search,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 10)
{
    if (page < 1 || pageSize < 1 || pageSize > 100)
        return BadRequest("Invalid pagination parameters.");

    var result = await _productService.GetAllAsync(
        search, page, pageSize);

    return Ok(new
    {
        result.Items,
        result.TotalCount,
        Page = page,
        PageSize = pageSize
    });
}
[Authorize(Roles = "Employee")]
[HttpPut("{id:int}")]
public async Task<ActionResult<ProductResponse>> Update(int id,ProductUpdateRequest request)
{
    var product = await _productService.UpdateAsync(id, request);

    if (product is null)
        return NotFound();

    return Ok(product);
}
[Authorize(Roles = "Employee")]
[HttpDelete("{id:int}")]
public async Task<IActionResult> Delete(int id)
{
    var deleted = await _productService.DeleteAsync(id);

    if (!deleted)
        return NotFound();

    return NoContent();
}
}