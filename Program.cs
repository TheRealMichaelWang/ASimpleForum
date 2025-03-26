using ASimpleForum.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace ASimpleForum
{
    public class Program
    {
        [RequiresDynamicCode("Database migrations/creations require dynamic code.")]
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateSlimBuilder(args);
            builder.WebHost.UseKestrelHttpsConfiguration();

            var app = builder.Build();
            app.UseHttpsRedirection();
            app.UseHsts();

            using (UserContext userContext = new UserContext())
            using (MessageContext messageContext = new MessageContext())
            using (ForumContext forumContext = new ForumContext())
            {
                SessionManager sessionManager = new SessionManager(userContext, app);
                userContext.Database.EnsureCreated();
                messageContext.Database.EnsureCreated();
                forumContext.Database.EnsureCreated();

                app.Run();
            }
        }
    }
}
