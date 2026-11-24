using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PressingManager.Infrastructure.Data;

/// <summary>
/// Utilise uniquement par les outils EF (dotnet ef migrations add / database update).
/// La chaine de connexion vient de la variable d'environnement ConnectionStrings__Pressing ;
/// a defaut, on vise un PostgreSQL local (valeurs du docker-compose.yml).
/// </summary>
public class PressingDbContextFactory
    : IDesignTimeDbContextFactory<PressingDbContext>
{
    public PressingDbContext CreateDbContext(string[] args)
    {
        NpgsqlSetup.Configure();

        string connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Pressing")
            ?? "Host=localhost;Port=5432;Database=pressingmanager;Username=pressing;Password=pressing_dev";

        var optionsBuilder = new DbContextOptionsBuilder<PressingDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new PressingDbContext(optionsBuilder.Options);
    }
}
