using System.ComponentModel.DataAnnotations;

namespace OrderFlow.Api.DTOs.Customers
{
    public class CustomerCreateRequest
    {
        [Required]
        [MaxLength(200)]
        public string Name {get; set;} = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(320)]
        public string Email {get; set;} = string.Empty;
    }
}