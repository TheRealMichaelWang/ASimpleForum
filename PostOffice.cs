using ASimpleForum.Models;
using Microsoft.AspNetCore.Mvc;
using static ASimpleForum.SessionManager;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ASimpleForum
{
    public sealed class PostOffice
    {
        private record SummarizedMessage(Guid id, string other, string subject, DateTime timeStamp, bool unread, bool flagged);
        private record RetrievedMessage(string sender, string recipient, string subject, string body, DateTime timeStamp, bool read, bool flagged);

        private MessageContext MessageContext;
        private UserContext UserContext;
        private SessionManager SessionManager;

        public PostOffice(MessageContext messageContext, UserContext userContext, SessionManager sessionManager, WebApplication application)
        {
            MessageContext = messageContext;
            UserContext = userContext;
            SessionManager = sessionManager;

            var msgGroup = application.MapGroup("mail");
            msgGroup.MapPost("send", HandleSendMail)
                .DisableAntiforgery()
                .Produces<Guid>(StatusCodes.Status200OK, "text/plain")
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status400BadRequest);
            msgGroup.MapPost("inbox", HandleRetrieveInbox)
                .DisableAntiforgery()
                .Produces<SummarizedMessage[]>(StatusCodes.Status200OK, "application/json")
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status400BadRequest);
            msgGroup.MapPost("outbox", HandleRetrieveOutbox)
                .DisableAntiforgery()
                .Produces<SummarizedMessage[]>(StatusCodes.Status200OK, "application/json")
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status400BadRequest);
            msgGroup.MapPost("msg", HandleRetrieveMailMessage)
                .DisableAntiforgery()
                .Produces<RetrievedMessage>(StatusCodes.Status200OK, "application/json")
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status400BadRequest);
            msgGroup.MapPost("mark", HandleMarkMailMessage)
                .DisableAntiforgery()
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status400BadRequest);
        }

        private async Task<IResult> HandleSendMail([FromForm] string sessionId, [FromForm]string recipient, [FromForm] string subject, [FromForm]string body)
        {
            Session? session = await SessionManager.GetSession(sessionId);
            if (session == null)
            {
                return Results.Content("Invalid session ID provided or session timed out.", "text/plain", null, StatusCodes.Status401Unauthorized);
            }

            User? recipientUser = await UserContext.FindUserAsync(recipient);
            if (recipientUser == null)
            {
                return Results.Content($"Recipient {recipient} not found.", "text/plain", null, StatusCodes.Status400BadRequest);
            }

            await MessageContext.SendMail(session.UserId, recipientUser.Id, subject, body);
            await Logger.LogAsync($"Sent a direct message to {recipientUser.Id}.", Logger.Info, session);

            return Results.Ok();
        }

        private async Task<IResult> HandleRetrieveInbox([FromForm] string sessionId, [FromForm] int offset, [FromForm] int messageLimit, [FromForm] bool filterUnread, [FromForm] bool filterFlagged)
        {
            Session? session = await SessionManager.GetSession(sessionId);
            if (session == null)
            {
                return Results.Content("Invalid session ID provided or session timed out.", "text/plain", null, StatusCodes.Status401Unauthorized);
            }

            var query = MessageContext.MailMessages
                .Where(x => x.Recipient == session.UserId)
                .Skip(offset);

            if (filterUnread)
            {
                query = query.Where(x => !x.MarkedAsRead);
            }
            if (filterFlagged)
            {
                query = query.Where(x => x.MarkedAsFlagged);
            }
            query = query
                .Take(messageLimit)
                .OrderBy(x => x.TimeStamp);

            MailMessage[] messages = await query.ToArrayAsync();
            SummarizedMessage[] summarizedMessages = await Task.WhenAll(messages.Select(async msg =>
            {
                return new SummarizedMessage(
                    msg.Id,
                    await UserContext.GetIdentifier(msg.Sender),
                    msg.Subject,
                    msg.TimeStamp,
                    msg.MarkedAsRead,
                    msg.MarkedAsFlagged
                );
            }));

            return Results.Content(JsonSerializer.Serialize(summarizedMessages), "application/json", null, StatusCodes.Status200OK);
        }

        private async Task<IResult> HandleRetrieveOutbox([FromForm] string sessionId, [FromForm] int offset, [FromForm] int messageLimit)
        {
            Session? session = await SessionManager.GetSession(sessionId);
            if (session == null)
            {
                return Results.Content("Invalid session ID provided or session timed out.", "text/plain", null, StatusCodes.Status401Unauthorized);
            }

            var query = MessageContext.MailMessages
                .Where(x => x.Sender == session.UserId)
                .Skip(offset)
                .Take(messageLimit)
                .OrderBy(x => x.TimeStamp);

            MailMessage[] messages = await query.ToArrayAsync();
            SummarizedMessage[] summarizedMessages = await Task.WhenAll(messages.Select(async msg =>
            {
                return new SummarizedMessage(
                    msg.Id,
                    await UserContext.GetIdentifier(msg.Recipient),
                    msg.Subject,
                    msg.TimeStamp,
                    msg.MarkedAsRead,
                    msg.MarkedAsFlagged
                );
            }));

            return Results.Content(JsonSerializer.Serialize(summarizedMessages), "application/json", null, StatusCodes.Status200OK);
        }

        private async Task<IResult> HandleRetrieveMailMessage([FromForm] string sessionId, [FromForm] string msgId)
        {
            Guid messageId;
            if (!Guid.TryParse(msgId, out messageId))
            {
                return Results.Content("Invalid message GUID provided.", "text/plain", null, StatusCodes.Status400BadRequest);
            }

            Session? session = await SessionManager.GetSession(sessionId);
            if (session == null)
            {
                return Results.Content("Invalid session ID provided or session timed out.", "text/plain", null, StatusCodes.Status401Unauthorized);
            }

            MailMessage? message = await MessageContext.MailMessages.FindAsync(messageId);
            if (message == null || (message.Sender != session.UserId && message.Recipient != session.UserId))
            {
                return Results.Content("You are not allowed to access this message.", "text/plain", null, StatusCodes.Status401Unauthorized);
            }

            return Results.Content(JsonSerializer.Serialize(new RetrievedMessage(
                await UserContext.GetIdentifier(message.Sender),
                await UserContext.GetIdentifier(message.Recipient),
                message.Subject,
                message.Body,
                message.TimeStamp,
                message.MarkedAsRead,
                message.MarkedAsFlagged
            )), "application/json", null, StatusCodes.Status200OK);
        }

        private async Task<IResult> HandleMarkMailMessage([FromForm] string sessionId, [FromForm] string messageId, [FromForm] bool markRead, [FromForm] bool markFlagged)
        {
            Guid id;
            if (!Guid.TryParse(messageId, out id))
            {
                return Results.Content("Invalid message GUID provided.", "text/plain", null, StatusCodes.Status400BadRequest);
            }

            Session? session = await SessionManager.GetSession(sessionId);
            if (session == null)
            {
                return Results.Content("Invalid session ID provided or session timed out.", "text/plain", null, StatusCodes.Status401Unauthorized);
            }

            MailMessage? message = await MessageContext.MailMessages.FindAsync(messageId);
            if (message == null || message.Sender != session.UserId)
            {
                return Results.Content("You are not allowed to access this message.", "text/plain", null, StatusCodes.Status401Unauthorized);
            }

            message.MarkedAsFlagged = markFlagged;
            message.MarkedAsRead = markRead;
            await MessageContext.SaveChangesAsync();

            return Results.Ok();
        }
    }
}
