using Microsoft.EntityFrameworkCore;
using PressingManager.Core.Entities;

namespace PressingManager.Infrastructure.Data;

/// <summary>
/// Contexte unique de l'application. Les configurations sont chargees automatiquement depuis
/// le dossier Configurations : chaque membre ajoute ses DbSet ici et sa configuration dans son propre fichier.
/// </summary>
public class PressingDbContext : DbContext
{
    public PressingDbContext(DbContextOptions<PressingDbContext> options) : base(options)
    {
    }

    // Module authentification (Personne 5)
    public DbSet<User> Users => Set<User>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Module clients et commandes (Personne 2)
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PressingDbContext).Assembly);
    }
}
