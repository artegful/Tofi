namespace Travelling.Services
{
    public class DatabaseLogger : ILogger
    {
        private readonly Database database;
        private static object _lock = new object();

        public DatabaseLogger(Database database)
        {
            this.database = database;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (formatter != null)
            {
                lock (_lock)
                {
                    database.Log((int)logLevel, formatter(state, exception)).Wait();
                }
            }
        }
    }
}
