namespace OrderFlow.Api.DTOS.Reports;

public class SalesSummaryResponse
{
    public int OrderCount {get; set;}

    public int ItemsSold {get; set;}

    public decimal Revenue {get; set;}
}