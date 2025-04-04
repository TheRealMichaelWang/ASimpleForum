﻿using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace ASimpleForum.Models
{
    public enum PermissionType
    {
        SuperUser=2,
        Administrator=1,
        RegisteredUser=0
    }

    public sealed class User
    {
        public required Guid Id { get; set; }

        public required string Username { get; set; }
        public required string Email { get; set; }

        public required byte[] PasswordHash { get; set; }

        public required PermissionType Permissions { get; set; }
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

        public async Task<User?> FindUserAsync(string identifier)
        {
            Guid id;
            if (Guid.TryParse(identifier, out id))
            {
                return await Users.FindAsync(id);
            }
            else
            {
                User? user = await Users.FirstOrDefaultAsync(x => x.Username == identifier);
                if (user == null)
                {
                    user = await Users.FirstOrDefaultAsync(x => x.Email == identifier);
                }
                return user;
            }
        }

        public async Task<string> GetIdentifier(Guid userId)
        {
            User? user = await Users.FindAsync(userId);
            return user == null ? $"[deleted user]{userId.ToString()}" : user.Username;
        }
    }
}
