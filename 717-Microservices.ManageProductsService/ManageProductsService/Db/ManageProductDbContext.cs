using ManageProductsService.Db.Entities;
using Microsoft.EntityFrameworkCore;

namespace ManageProductsService.Db;

public class ManageProductDbContext : DbContext
{
    internal DbSet<Product> Products { get; set; }
    public ManageProductDbContext(DbContextOptions<ManageProductDbContext> options)
        : base(options)
    {
        Database.EnsureCreated();
    }
}
