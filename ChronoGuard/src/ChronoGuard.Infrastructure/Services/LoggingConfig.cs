using Microsoft.Extensions.Logging;
using System;

namespace ChronoGuard.Infrastructure.Services
{
    public static class LoggingConfig
    {
        public static void ConfigureLogging(ILoggingBuilder builder, string appData)
        {
            builder.AddConsole();
            builder.AddFile($"{appData}/logs/chronoguard-{DateTime.Now:yyyy-MM-dd}.log");
            builder.SetMinimumLevel(LogLevel.Information);
        }
    }
}
