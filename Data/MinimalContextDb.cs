using DemoMinimalApi.Models;
using Microsoft.EntityFrameworkCore;

namespace DemoMinimalApi.Data
{
    public class MinimalContextDb : DbContext
    {
        public MinimalContextDb(DbContextOptions<MinimalContextDb> options) : base(options) { }

        public DbSet<Provider> Providers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Provider>()
                .HasKey(p => p.Id);

            modelBuilder.Entity<Provider>()
                .Property(p => p.Name)
                .IsRequired()
                .HasColumnType("nvarchar(200)");

            modelBuilder.Entity<Provider>()
                .Property(p => p.Document)
                .IsRequired()
                .HasColumnType("nvarchar(14)");

            modelBuilder.Entity<Provider>()
                .ToTable("Provider");

            base.OnModelCreating(modelBuilder);
        }
    }
}
