using BookLibrary.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BookLibrary.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Book> Books { get; set; }
        public DbSet<Works> Works { get; set; }

        //public DbSet<Author> Authors => Set<Author>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Parsed only after retrieval
            //modelBuilder.Ignore<WorksJsonParsedInfo>();
            //modelBuilder.Ignore<Works_Text>();

            modelBuilder.Entity<Works>(entity =>
            {
                entity.OwnsOne(b => b.RawJson, jsonMetadataBuilder =>
                {
                    jsonMetadataBuilder.ToJson();

                    // Nested item have to map it
                    jsonMetadataBuilder.OwnsOne(c => c.Description, descriptionBuilder =>
                    {
                        descriptionBuilder.HasJsonPropertyName("description");
                        descriptionBuilder.Property(d => d.Value).HasJsonPropertyName("value");
                    });
                });
            });
        }
    }
}
