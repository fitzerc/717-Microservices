using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManageOrdersService.Db.Entities
{
    internal class Order
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [Required]
        public string UserId { get; set; }
        [Required]
        public int CustomerId { get; set; }
        public List<OrderLineItem> LineItems { get; set; } = new();
        [Required]
        public DateTime DatePlaced { get; set; } = DateTime.Now;
    }

    internal class OrderLineItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [ForeignKey(nameof(Order))]
        public int OrderId { get; set; }
        public int ProductId { get; set; }
    }
}
