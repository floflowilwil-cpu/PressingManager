
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PressingManager.Infrastructure.Data;

public class PressingDbContextFactory
    : IDesignTimeDbContextFactory<PressingDbContext>
{
    public PressingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder =
            new DbContextOptionsBuilder<PressingDbContext>();

        string databasePath = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "PressingManager",
            "pressingmanager.db");

        optionsBuilder.UseSqlite(
            $"Data Source={databasePath}");

        return new PressingDbContext(optionsBuilder.Options);
    }
}
