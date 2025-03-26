using ASimpleForum.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace ASimpleForum
{
    public sealed class SessionManager
    {
        public sealed class Session
        {
            public Guid SessionId { get; private set; }
            public Guid UserId { get; private set; }

            private DateTime ExpirationTimestamp;

            public Session(Guid sessionId, Guid userId)
            {
                SessionId = sessionId;
                UserId = userId;
                ExpirationTimestamp = DateTime.UtcNow.AddMinutes(15);
            }

            public bool IsExpired => DateTime.UtcNow.CompareTo(ExpirationTimestamp) > 0;

            public void ExtendSession() => ExpirationTimestamp = ExpirationTimestamp.AddMinutes(15);
        }

        private UserContext UserContext;
        private ConcurrentDictionary<Guid, Session> Sessions;

        public SessionManager(UserContext userContext, WebApplication application)
        {
            this.UserContext = userContext;
            Sessions = new ConcurrentDictionary<Guid, Session>();

            application.MapPost("login", HandleLogin).DisableAntiforgery();
            application.MapPost("register", HandleRegister).DisableAntiforgery();
            application.MapPost("logout", HandleLogout).DisableAntiforgery();
        }

        public async Task<Session?> GetSession(Guid sessionId)
        {
            Session session;
            
            if (!Sessions.TryGetValue(sessionId, out session!))
            {
                return null;
            }

            if (session.IsExpired)
            {
                await Logger.LogAsync("Session expired. Removing session.", Logger.Info, session);
                Sessions.Remove(sessionId, out session!);
                return null;
            }
            session.ExtendSession();

            return session;
        }

        private async Task<IResult> HandleLogin([FromForm]string username, [FromForm]string password)
        {
            User? user = await UserContext.Users.FirstOrDefaultAsync(x => x.Username == username);
            if (user == null)
            {
                user = await UserContext.Users.FirstOrDefaultAsync(x => x.Email == username);
            }
            if (user == null || !user.PasswordMatches(password))
            {
                return Results.BadRequest("Username or password is invalid.");
            }

            Guid id = Guid.NewGuid();
            Session session = new Session(id, user.Id);
            if(!Sessions.TryAdd(id, session))
            {
                return Results.BadRequest("Failed to create new user session; GUID collision occurred.");
            }

            user.LastLogin = DateTime.UtcNow;
            await UserContext.SaveChangesAsync();
            await Logger.LogAsync($"User {username} logged in.", Logger.Info, session);

            return Results.Ok(id.ToString());
        }

        private async Task<IResult> HandleRegister([FromForm] string username, [FromForm] string email, [FromForm] string password)
        {
            {
                User? user = await UserContext.Users.FirstOrDefaultAsync(x => x.Username == username);
                if (user == null)
                {
                    user = await UserContext.Users.FirstOrDefaultAsync(x => x.Email == username);
                }
                if (user != null)
                {
                    return Results.BadRequest("Username or email already in use.");
                }
            }

            {
                User user = new()
                {
                    Id = Guid.NewGuid(),

                    Username = username,
                    Email = email,
                    PasswordHash = User.HashPassword(password),

                    IsEmailConfirmed = false,
                    LastLogin = DateTime.UtcNow,
                    CreationTimeStamp = DateTime.UtcNow
                };

                try
                {
                    await UserContext.Users.AddAsync(user);
                    await UserContext.SaveChangesAsync();
                    await Logger.Log($"User account {username} registered.", Logger.Info, null, user.Id);
                }
                catch (InvalidOperationException)
                {
                    return Results.BadRequest($"Failed to register account; GUID collision occurred.");
                }


                Guid id = Guid.NewGuid();
                Session session = new Session(id, user.Id);
                if (!Sessions.TryAdd(id, session))
                {
                    return Results.BadRequest($"Successfully registered user {username} but failed to create new user session; GUID collision occurred.");
                }
                await Logger.LogAsync($"User {username} logged in.", Logger.Info, session);

                return Results.Ok(id.ToString());
            }
        }

        private async Task<IResult> HandleLogout([FromForm] string sessionId)
        {
            Guid id;
            if (!Guid.TryParse(sessionId, out id))
            {
                return Results.BadRequest("Invalid GUID provided.");
            }

            Session? session = await GetSession(id);
            if (session == null)
            {
                return Results.BadRequest("Invalid session ID provided or session timed out.");
            }

            if (!Sessions.TryRemove(id, out session))
            {
                return Results.BadRequest("Unable to remove session.");
            }

            return Results.Ok();
        }
    }
}
