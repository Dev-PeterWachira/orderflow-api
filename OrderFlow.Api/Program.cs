using Microsoft.EntityFrameworkCore;
using OrderFlow.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<OrderFlowDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("OrderFlowDb")));



var app = builder.Build();


app.UseHttpsRedirection();

app.MapControllers();

app.Run();