namespace ASimpleForum
{
    public static class Logger
    {
        public static readonly int Info = 1000;

        public static string LogFilePath => Path.Combine(Environment.CurrentDirectory, $"logs_{DateTime.Now.Date.ToString("MM_dd_yyyy")}.db");

        public static async Task Log(string message, int level, Guid? sessionId = null, Guid? userId = null)
        {
            using (StreamWriter writer = new StreamWriter(new FileStream(LogFilePath, FileMode.Append, FileAccess.Write)))
            {
                await writer.WriteAsync($"{level}/{DateTime.UtcNow.ToShortTimeString()}");
                if (sessionId != null)
                {
                    await writer.WriteAsync($"/[session:{sessionId}]");
                }
                if (userId != null)
                {
                    await writer.WriteAsync($"/[userid:{userId}]");
                }
                await writer.WriteLineAsync($": {message}");
            }
        }

        public static async Task LogAsync(string message, int level, SessionManager.Session? session = null)
        {
            if (session != null)
            {
                await Log(message, level, session.SessionId, session.UserId);
            }
        }
    }
}
