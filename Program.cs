using ASimpleForum.Models;

namespace ASimpleForum
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("ASimpleForum");
            Console.WriteLine($"Current Working Directory: {Environment.CurrentDirectory}.");

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
                PostOffice postOffice = new PostOffice(messageContext, userContext, sessionManager, app);

                userContext.Database.EnsureCreated();
                messageContext.Database.EnsureCreated();
                forumContext.Database.EnsureCreated();

                app.Run();
            }
        }
    }
}
