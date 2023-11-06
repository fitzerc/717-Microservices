using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Microservices.ManageUsers.Identity;

public class MicroservicesAuthDbContext : IdentityDbContext<IdentityUser>
{
    public MicroservicesAuthDbContext(DbContextOptions<MicroservicesAuthDbContext> options)
        : base(options)
    {
        Database.EnsureCreated();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<IdentityUser>().ToTable("AspNetUsers");
        base.OnModelCreating(builder);
    }
}
