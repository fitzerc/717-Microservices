using ManageOrdersService.Db.Entities;
using Microsoft.EntityFrameworkCore;

namespace ManageOrdersService.Db;

public class ManageOrderDbContext : DbContext
{
    internal DbSet<Order> Orders { get; set; }
    internal DbSet<OrderLineItem> OrderLineItem { get; set; }
    public ManageOrderDbContext(DbContextOptions<ManageOrderDbContext> options)
        : base(options)
    {
        Database.EnsureCreated();
    }
}
