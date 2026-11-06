using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DocAgent.Infrastructure;

/// <summary>
/// Lets `dotnet ef migrations add` build the DbContext without Aspire running. It never connects:
/// generating a migration only needs the model.
/// </summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<DocAgentDbContext>
{
    public DocAgentDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<DocAgentDbContext>()
            .UseNpgsql("Host=localhost;Database=docagentdb;Username=postgres;Password=design-time-only")
            .Options);
}
