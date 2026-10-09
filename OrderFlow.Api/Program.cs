using Microsoft.EntityFrameworkCore;
using OrderFlow.Api.Data;
using OrderFlow.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<OrderFlowDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("OrderFlowDb")));

builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IOrderService, OrderService>();



var app = builder.Build();


app.UseHttpsRedirection();

app.MapControllers();

app.Run();