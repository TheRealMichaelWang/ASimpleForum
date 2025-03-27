using Microsoft.EntityFrameworkCore;

namespace ASimpleForum.Models
{
    public sealed class MailMessage
    {
        public required Guid Id { get; set; }

        public required Guid Recipient { get; set; }
        public required Guid Sender { get; set; }

        public required string Subject { get; set; }
        public required string Body { get; set; }
        public required DateTime TimeStamp { get; set; }

        public required bool MarkedAsRead { get; set; }
        public required bool MarkedAsFlagged { get; set; }
    }

    public sealed class MessageContext : DbContext
    {
        public DbSet<MailMessage> MailMessages { get; set; }

        public string DatabasePath => Path.Combine(Environment.CurrentDirectory, "messages.db");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MailMessage>()
                .HasKey(e => e.Id)
                .HasName("ID");

            modelBuilder.Entity<MailMessage>()
                .HasIndex(e => e.Recipient);

            modelBuilder.Entity<MailMessage>()
                .HasIndex(e => e.Sender);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseSqlite($"Filename={DatabasePath}");

        public async Task SendMail(Guid senderId, Guid recipientId, string subject, string body)
        {
            MailMessage directMessage = new MailMessage()
            {
                Id = Guid.NewGuid(),
                Sender = senderId,
                Recipient = recipientId,
                Subject = subject,
                Body = body,
                TimeStamp = DateTime.UtcNow,
                MarkedAsRead = false,
                MarkedAsFlagged = false
            };

            await MailMessages.AddAsync(directMessage);
            await SaveChangesAsync();
        }
    }
}
