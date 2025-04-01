using ASimpleForum.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ASimpleForum
{
    public sealed class ForumManager
    {
        private record ForumSummary(Guid id, string name, string description);
        private record PostSummary(Guid id, string title, string author, DateTime timeStamp);
        
        private record PostResponse(string title, string author, string body, DateTime timeStamp);
        private record ReplyResponse(Guid id, string author, string body, DateTime timeStamp);

        private ForumContext ForumContext;
        private UserContext UserContext;
        private SessionManager SessionManager;

        public ForumManager(ForumContext forumContext, UserContext userContext, SessionManager sessionManager, WebApplication application)
        {
            ForumContext = forumContext;
            UserContext = userContext;
            SessionManager = sessionManager;

            var forumGroup = application.MapGroup("forums");
            forumGroup.MapGet("index", HandleRetrieveForumIndex)
                .DisableAntiforgery()
                .Produces<ForumSummary[]>(StatusCodes.Status200OK, "application/json");
            forumGroup.MapPost("posts", HandleRetrievePostIndex)
                .DisableAntiforgery()
                .Produces<PostSummary[]>(StatusCodes.Status200OK, "application/json")
                .Produces(StatusCodes.Status400BadRequest, null, "text/plain")
                .Produces(StatusCodes.Status401Unauthorized, null, "text/plain");
            forumGroup.MapPost("post", HandleRetrievePost)
                .DisableAntiforgery()
                .Produces<PostResponse>(StatusCodes.Status200OK, "application/json")
                .Produces(StatusCodes.Status400BadRequest, null, "text/plain")
                .Produces(StatusCodes.Status401Unauthorized, null, "text/plain");
            forumGroup.MapPost("replies", HandleRetrieveReplies)
                .DisableAntiforgery()
                .Produces<PostResponse>(StatusCodes.Status200OK, "application/json")
                .Produces(StatusCodes.Status400BadRequest, null, "text/plain")
                .Produces(StatusCodes.Status401Unauthorized, null, "text/plain");
        }

        private async Task<IResult> HandleRetrieveForumIndex([FromQuery] int offset, [FromQuery] int limit, [FromQuery] bool filter)
        {
            var query = ForumContext.Forums.AsQueryable();
            if (!filter)
            {
                query = query.Where(x => x.IsPublic);
            }
            var newQuery = query
                .Skip(offset)
                .Take(limit)
                .Select(forum => new ForumSummary(
                    forum.Id,
                    forum.Name,
                    forum.Description
                ));

            return Results.Content(JsonSerializer.Serialize(await newQuery.ToArrayAsync()), "application/json", null, StatusCodes.Status200OK);
        }

        private async Task<IResult> HandleRetrievePostIndex([FromBody] string id, [FromBody] string sessionId, [FromBody] int offset, [FromBody] int limit)
        {
            Guid forumId;
            Forum? forum;
            if (!Guid.TryParse(id, out forumId) || (forum = await ForumContext.Forums.FindAsync(forumId)) == null)
            {
                return Results.Content("Invalid forum GUID provided.", "text/plain", null, StatusCodes.Status400BadRequest);
            }

            User? user = await SessionManager.GetUser(sessionId);
            if (!forum.IsAuthorized(user))
            {
                return Results.Content("You are not authorized to access this forum", "text/plain", null, StatusCodes.Status401Unauthorized);
            }

            var query = ForumContext.Posts
                .Where(x => x.ForumId == forum.Id)
                .Where(x => !x.Removed)
                .Skip(offset)
                .Take(limit);

            PostSummary[] postSummaries = await Task.WhenAll((query as IEnumerable<Post>).Select(async post => new PostSummary(
                post.Id,
                post.Title,
                await UserContext.GetIdentifier(post.Author),
                post.TimeStamp
            )));

            return Results.Content(JsonSerializer.Serialize(postSummaries), "application/json", null, StatusCodes.Status200OK);
        }

        private async Task<IResult> HandleRetrievePost([FromBody] string id, [FromBody] string sessionId)
        {
            Guid postId;
            Post? post;
            if (!Guid.TryParse(id, out postId) || (post = await ForumContext.Posts.FindAsync(postId)) == null)
            {
                return Results.Content("Invalid post GUID provided.", "text/plain", null, StatusCodes.Status400BadRequest);
            }

            User? user = await SessionManager.GetUser(sessionId);
            Forum? forum = await ForumContext.Forums.FindAsync(post.ForumId);
            if (forum != null && !forum.IsAuthorized(user))
            {
                return Results.Content("You are not authorized to access this forum", "text/plain", null, StatusCodes.Status401Unauthorized);
            }

            return Results.Content(JsonSerializer.Serialize(new PostResponse(
                post.Title,
                await UserContext.GetIdentifier(post.Author),
                post.Body,
                post.TimeStamp
            )), "application/json", null, StatusCodes.Status200OK);
        }


        private async Task<IResult> HandleRetrieveReplies([FromBody] string id, [FromBody] string sessionId, [FromBody] string parent, [FromBody] int offset, [FromBody] int limit)
        {
            Guid postId;
            Post? post;
            if (!Guid.TryParse(id, out postId) || (post = await ForumContext.Posts.FindAsync(postId)) == null)
            {
                return Results.Content("Invalid post GUID provided.", "text/plain", null, StatusCodes.Status400BadRequest);
            }

            User? user = await SessionManager.GetUser(sessionId);
            Forum? forum = await ForumContext.Forums.FindAsync(post.ForumId);
            if (forum != null && !forum.IsAuthorized(user))
            {
                return Results.Content("You are not authorized to access this forum", "text/plain", null, StatusCodes.Status401Unauthorized);
            }

            Guid parentId;
            if (!Guid.TryParse(parent, out parentId))
            {
                return Results.Content("Invalid parent GUID provided.", "text/plain", null, StatusCodes.Status400BadRequest);
            }

            var query = ForumContext.Replies
                .Where(x => x.Id == postId)
                .Where(x => x.ParentReplyId == parentId)
                .Skip(offset)
                .Take(limit);

            ReplyResponse[] replies = await Task.WhenAll((query as IEnumerable<PostReply>).Select(async reply => new ReplyResponse(
                reply.Id,
                await UserContext.GetIdentifier(reply.Author),
                reply.Body,
                reply.TimeStamp
            )));

            return Results.Content(JsonSerializer.Serialize(replies), "application/json", null, StatusCodes.Status200OK);
        }
    }
}
