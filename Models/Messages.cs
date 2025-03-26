using Microsoft.EntityFrameworkCore;

namespace ASimpleForum.Models
{
    public sealed class DirectMessage
    {
        public required Guid Recipient { get; set; }
        public required Guid Sender { get; set; }

        public required string Body { get; set; }
        public required DateTime TimeStamp { get; set; }
    }

    public sealed class MessageContext : DbContext
    {
        public DbSet<DirectMessage> DirectMessages { get; set; }

        public string DatabasePath => Path.Combine(Environment.CurrentDirectory, "messages.db");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DirectMessage>()
                .HasKey(e => e.Recipient);

            modelBuilder.Entity<DirectMessage>()
                .HasAlternateKey(e => e.Sender);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseSqlite($"Filename={DatabasePath}");
    }
}
