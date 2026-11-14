using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using PawsPost.Tests.Infrastructure;

namespace PawsPost.Tests.Features.Auth;

public class LoggingAndPublicAccessTests
{
    [Fact]
    public async Task AuthFlow_RegisterFailedLoginAndLogout_LogsNoEmailOrPassword()
    {
        // Arrange
        const string email = "private.person@example.com";
        const string password = "Sup3rSecret!pw";
        var provider = new CapturingLoggerProvider();
        using var factory = new PawsPostFactory();
        using var loggedFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(provider)));
        var client = loggedFactory.CreateApiClient();

        // Act
        await client.RegisterAsync(email, password);
        await client.LoginAsync(email, "Wrong1234!");
        await client.LoginAsync(email, password);
        await client.PostAsync("/api/auth/logout", null);

        // Assert
        Assert.NotEmpty(provider.Messages);
        Assert.All(provider.Messages, message =>
        {
            Assert.DoesNotContain(email, message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(password, message);
        });
    }

    [Fact]
    public async Task HealthGet_NoCookie_Returns200()
    {
        // Arrange
        using var factory = new PawsPostFactory();
        var client = factory.CreateApiClient();

        // Act
        var response = await client.GetAsync("/api/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                messages.Enqueue(formatter(state, exception) + exception);
            }
        }
    }
}
