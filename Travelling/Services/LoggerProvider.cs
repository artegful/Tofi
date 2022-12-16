namespace Travelling.Services
{
    public class LoggerProvider : ILoggerProvider
    {
        private readonly Database database;

        public LoggerProvider(Database database)
        {
            this.database = database;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new DatabaseLogger(database);
        }

        public void Dispose()
        {
        }
    }
}
