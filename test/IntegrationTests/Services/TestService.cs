namespace IntegrationTests.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;
using OBSStudioClient;
using OBSStudioClient.Enums;
using Xunit.Sdk;

public class TestService
{
    public static readonly ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
    {
        builder.ClearProviders();
        builder.SetMinimumLevel(LogLevel.Trace);
        builder.AddProvider(new DebugLoggerProvider());
    });
    public static readonly IConfiguration configuration = configuration = new ConfigurationBuilder()
            .AddUserSecrets<TestService>()
            .Build();

    public ObsClient CreateClient()
    {
        ObsClient client = new(loggerFactory.CreateLogger<ObsClient>());
        return client;
    }

    public async Task ConnectOrThrowAsync(ObsClient client, bool autoReconnect = false, EventSubscriptions eventSubscription = EventSubscriptions.All)
    {
        var connectionString = configuration.GetConnectionString("ObsStudio");
        if (connectionString == null)
            throw new XunitException("Connection string for OBS Studio is not configured. Please configure it in user secrets for the IntegrationTests project.");
        
        var result = await client.ConnectAsync(
            autoReconnect: autoReconnect,
            connectionString: connectionString,
            eventSubscription: eventSubscription);
        if (!result)
            throw new XunitException("Failed to connect to OBS WebSocket server.");
    }
}
