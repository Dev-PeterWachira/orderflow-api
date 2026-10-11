namespace OrderFlow.Api.Models.Reporting;

public class DailySalesRow
{
public DateTime Date { get; set; }
public int OrderCount { get; set; }
public int ItemsSold { get; set; }
public decimal Revenue { get; set; }
}
