using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace ASimpleForum.Models
{
    public sealed class User
    {
        public required Guid Id { get; set; }

        public required string Username { get; set; }
        public required string Email { get; set; }

        public required byte[] PasswordHash { get; set; }

        public required bool IsEmailConfirmed { get; set; }
        public required DateTime LastLogin { get; set; }
        public required DateTime CreationTimeStamp { get; set; }

        public static byte[] HashPassword(string password)
        {
            using (HashAlgorithm algorithm = SHA256.Create())
                return algorithm.ComputeHash(Encoding.UTF8.GetBytes(password));
        }

        public bool PasswordMatches(string password) => PasswordHash.SequenceEqual(HashPassword(password));
    }

    public sealed class UserContext : DbContext
    {
        public DbSet<User> Users { get; set; }

        public string DatabasePath => Path.Combine(Environment.CurrentDirectory, "users.db");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasKey(x => x.Id)
                .HasName("ID");
            modelBuilder.Entity<User>()
                .HasAlternateKey(x => x.Username)
                .HasName("UserID");
            modelBuilder.Entity<User>()
                .HasAlternateKey(x => x.Email)
                .HasName("Email");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseSqlite($"Filename={DatabasePath}");
    }
}
