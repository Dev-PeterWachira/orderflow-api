

namespace OrderFlow.Api.Models
{
    public class Order
    {
        public int Id {get; set;}

        public int CustomerId {get; set;}

        public OrderStatus Status {get; set;} = OrderStatus.Pending;

        public DateTime CreatedAt {get; set;} = DateTime.UtcNow;

        public Customer Customer {get; set;} = null!;

        public ICollection<OrderItem> OrderItems {get; set;} = new List<OrderItem>();
    }
}