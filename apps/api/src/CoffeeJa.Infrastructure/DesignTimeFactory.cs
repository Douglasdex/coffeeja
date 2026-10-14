using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CoffeeJa.Infrastructure;

/// <summary>Permite `dotnet ef` sem subir a API.</summary>
public class DesignTimeFactory : IDesignTimeDbContextFactory<CoffeeJaDbContext>
{
    public CoffeeJaDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                 ?? "Server=(localdb)\\MSSQLLocalDB;Database=CoffeeJa;Trusted_Connection=True;TrustServerCertificate=True";
        var opts = new DbContextOptionsBuilder<CoffeeJaDbContext>().UseSqlServer(cs).Options;
        return new CoffeeJaDbContext(opts);
    }
}
