using ASimpleForum.Models;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.Text.Json;

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

        private record UserInfoResponse(string username, PermissionType userType, DateTime lastLogin, DateTime creationDate);
        private record DetailedUserResponse(string username, PermissionType userType, DateTime lastLogin, DateTime creationDate, string email, bool IsEmailConfirmed);

        private UserContext UserContext;
        private ConcurrentDictionary<Guid, Session> Sessions;

        public SessionManager(UserContext userContext, WebApplication application)
        {
            this.UserContext = userContext;
            Sessions = new ConcurrentDictionary<Guid, Session>();

            application.MapPost("login", HandleLogin)
                .DisableAntiforgery()
                .Produces<Guid>(StatusCodes.Status200OK, "text/plain")
                .Produces(StatusCodes.Status400BadRequest);
            application.MapPost("register", HandleRegister)
                .DisableAntiforgery()
                .Produces<Guid>(StatusCodes.Status200OK, "text/plain")
                .Produces(StatusCodes.Status400BadRequest);
            application.MapPost("logout", HandleLogout)
                .DisableAntiforgery()
                .Produces(StatusCodes.Status401Unauthorized);

            application.MapGet("user", HandleUserInfoQuery)
                .DisableAntiforgery()
                .Produces<UserInfoResponse>(StatusCodes.Status200OK, "application/json")
                .Produces(StatusCodes.Status400BadRequest);

            application.MapPost("admin/user", HandleAdminInfoQuery)
                .DisableAntiforgery()
                .Produces<UserInfoResponse>(StatusCodes.Status200OK, "application/json")
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized);
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

        public async Task<Session?> GetSession(string sessionId)
        {
            Guid id;
            if (!Guid.TryParse(sessionId, out id))
            {
                return null;
            }

            return await GetSession(id);
        }

        public async Task<User?> GetUser(string sessionId)
        {
            Session? session = await GetSession(sessionId);
            if (session != null)
            {
                User? user = await UserContext.Users.FindAsync(session.UserId);
                return user;
            }
            return null;
        }

        private async Task<IResult> HandleLogin([FromForm]string username, [FromForm]string password)
        {
            User? user = await UserContext.FindUserAsync(username);
            if (user == null || !user.PasswordMatches(password))
            {
                return Results.Content("Username or password is invalid.", "text/plain", null, StatusCodes.Status400BadRequest);
            }

            Guid id = Guid.NewGuid();
            Session session = new Session(id, user.Id);
            if(!Sessions.TryAdd(id, session))
            {
                return Results.Content("Failed to create new user session; GUID collision occurred.", "text/plain", null, StatusCodes.Status400BadRequest);
            }

            user.LastLogin = DateTime.UtcNow;
            await UserContext.SaveChangesAsync();
            await Logger.LogAsync($"User {username} logged in.", Logger.Info, session);

            return Results.Content(id.ToString(), "text/plain", null, StatusCodes.Status200OK);
        }

        private async Task<IResult> HandleRegister([FromForm] string username, [FromForm] string email, [FromForm] string password)
        {
            {
                User? existingUser = await UserContext.FindUserAsync(username);
                if (existingUser != null)
                {
                    return Results.Content("Username or email already in use.", "text/plain", null, StatusCodes.Status400BadRequest);
                }
            }

            User user = new()
            {
                Id = Guid.NewGuid(),

                Username = username,
                Email = email,
                PasswordHash = User.HashPassword(password),

                Permissions = PermissionType.RegisteredUser,
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
                return Results.Content($"Failed to register account; GUID collision occurred.", "text/plain", null, StatusCodes.Status400BadRequest);
            }


            Guid id = Guid.NewGuid();
            Session session = new Session(id, user.Id);
            if (!Sessions.TryAdd(id, session))
            {
                return Results.Content($"Successfully registered user {username} but failed to create new user session; GUID collision occurred.", "text/plain", null, StatusCodes.Status400BadRequest);
            }
            await Logger.LogAsync($"User {username} logged in.", Logger.Info, session);

            return Results.Content(id.ToString(), "text/plain", null, StatusCodes.Status200OK);
        }

        private async Task<IResult> HandleLogout([FromForm] string sessionId)
        {
            Session? session = await GetSession(sessionId);
            if (session == null)
            {
                return Results.Content("Invalid session ID provided or session timed out.", "text/plain", null, StatusCodes.Status401Unauthorized);
            }

            if (!Sessions.TryRemove(session.SessionId, out session))
            {
                return Results.Content("Unable to remove session.", "text/plain", null, StatusCodes.Status400BadRequest);
            }

            await Logger.LogAsync($"User logged out.", Logger.Info, session);
            return Results.Ok();
        }

        private async Task<IResult> HandleUserInfoQuery([FromQuery] string userId)
        {
            User? user = await UserContext.FindUserAsync(userId);
            if (user == null)
            {
                return Results.Content("Cannot find user ID.", "text/plain", null, StatusCodes.Status401Unauthorized);
            }

            return Results.Content(JsonSerializer.Serialize(new UserInfoResponse(
                user.Username,
                user.Permissions,
                user.LastLogin,
                user.CreationTimeStamp
            )), "application/json", null, StatusCodes.Status200OK);
        }

        private async Task<IResult> HandleAdminInfoQuery([FromForm] string sessionId, [FromForm] string userId)
        {
            User? currentUser = await GetUser(sessionId);
            if (currentUser == null || currentUser.Permissions < PermissionType.Administrator)
            {
                return Results.Content("Invalid session ID provided, session timed out, or insufficient permissions.", "text/plain", null, StatusCodes.Status401Unauthorized);
            }

            User? user = await UserContext.FindUserAsync(userId);
            if (user == null)
            {
                return Results.Content("Invalid user ID.", "text/plain", null, StatusCodes.Status401Unauthorized);
            }

            return Results.Content(JsonSerializer.Serialize(new DetailedUserResponse(
                user.Username,
                user.Permissions,
                user.LastLogin,
                user.CreationTimeStamp,
                user.Email,
                user.IsEmailConfirmed
            )), "application/json", null, StatusCodes.Status200OK);
        }
    }
}
