using Microsoft.EntityFrameworkCore;

namespace ASimpleForum.Models
{
    public sealed class Forum
    {
        public required Guid Id { get; set; }

        public required string Name { get; set; }
        public required string Description { get; set; }

        public required List<Guid> Moderators { get; set; }
        public required List<Guid> Blacklist { get; set; }
        public required List<Guid> Whitelist { get; set; }

        public bool IsPublic => Whitelist.Count > 0;

        public bool IsAuthorized(User? user) => IsPublic
            || (user != null && (
            Whitelist.Contains(user.Id) 
            || user.Permissions >= PermissionType.Administrator
        ));
    }

    public sealed class Post
    {
        public required Guid Id { get; set; }
        public required Guid ForumId { get; set; }

        public required Guid Author { get; set; }

        public required string Title { get; set; }
        public required string Body { get; set; }
        public required bool Removed { get; set; }

        public required DateTime TimeStamp { get; set; }
    }

    public sealed class PostReply
    {
        public required Guid Id { get; set; }
        public required Guid ParentReplyId { get; set; }

        public required Guid Author { get; set; }
        public required string Body { get; set; }

        public required DateTime TimeStamp { get; set; }
    }

    public sealed class ForumContext : DbContext
    {
        public DbSet<Forum> Forums { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<PostReply> Replies { get; set; }

        public string DatabasePath => Path.Combine(Environment.CurrentDirectory, "forums.db");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Forum>()
                .HasKey(x => x.Id)
                .HasName("ID");

            modelBuilder.Entity<Post>()
                .HasKey(x => x.Id)
                .HasName("ID");

            modelBuilder.Entity<PostReply>()
                .HasKey(x => x.Id)
                .HasName("ID");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseSqlite($"Filename={DatabasePath}");
    }
}
