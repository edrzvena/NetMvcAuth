using Microsoft.EntityFrameworkCore;   //dotnet add package Microsoft.EntityFrameworkCore.SqlServer #ini library ef core/orm nya .NET
using NetMvcAuth.Models; //manggil namespace WebApplication1.Models (yang isinya class Product), biar class itu bisa dipake di file ini

namespace NetMvcAuth.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
    }
}