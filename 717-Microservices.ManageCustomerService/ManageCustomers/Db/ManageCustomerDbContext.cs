using ManageCustomers.Db.Entities;
using Microsoft.EntityFrameworkCore;

namespace ManageCustomers.Db;

public class ManageCustomerDbContext : DbContext
{
    internal DbSet<Customer> Customers { get; set; }
    public ManageCustomerDbContext(DbContextOptions<ManageCustomerDbContext> options)
        : base(options)
    {
        Database.EnsureCreated();
    }
}
