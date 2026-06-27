using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NetIAM.Infrastructure.Persistence;

public sealed class NetIamDbContextFactory : IDesignTimeDbContextFactory<NetIamDbContext>
{
    public NetIamDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<NetIamDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("NETIAM_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=netiam_dev;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseOpenIddict();
        return new NetIamDbContext(optionsBuilder.Options);
    }
}
