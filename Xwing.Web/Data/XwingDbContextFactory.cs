using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Xwing.Web.Data;
public sealed class XwingDbContextFactory : IDesignTimeDbContextFactory<XwingDbContext>
{
    public XwingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<XwingDbContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=XwingDesignTime;Trusted_Connection=True;TrustServerCertificate=True");
        return new XwingDbContext(optionsBuilder.Options, TimeProvider.System);
    }
}
