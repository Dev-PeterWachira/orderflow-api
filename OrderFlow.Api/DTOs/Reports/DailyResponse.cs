
namespace OrderFlow.Api.DTOs.Reports;

public class DailySalesResponse
{
    public DateTime Date { get; set; }
    public int OrderCount { get; set; }
    public int ItemsSold { get; set; }
    public decimal Revenue { get; set; }
}