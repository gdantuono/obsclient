using OBSStudioClient;
using OBSStudioClient.Enums;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Xunit.Sdk;
using Microsoft.Extensions.Logging.Debug;
using IntegrationTests.Services;

namespace IntegrationTests;

public class ConnectionTests
{
    private readonly TestService testService = new();

    [Fact]
    public async Task Stress()
    {
        var timeoutCounter = 0;
        var successCounter = 0;
        var inputVolumeMeterEventCount = 0;
        using var client = testService.CreateClient();
        client.HighVolumeEventThrottleInterval = TimeSpan.FromMilliseconds(100);
        client.RequestTimeout = 500;
        client.InputVolumeMeters += (sender, e) =>
        {
            Interlocked.Increment(ref inputVolumeMeterEventCount);

            // Simulate some processing time for the event handler
            Thread.Sleep(10);
        };

        await testService.ConnectOrThrowAsync(client,
            eventSubscription: OBSStudioClient.Enums.EventSubscriptions.All
                | OBSStudioClient.Enums.EventSubscriptions.InputVolumeMeters);

        using var timer = new Timer(async (state) =>
        {
            try
            {
                if (client.ConnectionState != ConnectionState.Connected)
                    return;
                Assert.NotNull(await client.GetStats());
            }
            catch (TimeoutException)
            {
                Interlocked.Increment(ref timeoutCounter);
            }
        }, null, 0, 200);
        await Parallel.ForEachAsync(Enumerable.Range(0, 1000), async (i, ct) =>
        {
            try
            {
                Assert.NotNull(await client.GetVersion());
                Interlocked.Increment(ref successCounter);
                await Task.Delay(50);
            }
            catch (TimeoutException)
            {
                Interlocked.Increment(ref timeoutCounter);
                await Task.Delay(100);
            }
        });
        client.Disconnect();
        await testService.ConnectOrThrowAsync(client);
        Assert.NotNull(await client.GetVersion());
        Assert.True(timeoutCounter == 0, $"Expected no timeouts, but got {timeoutCounter} timeouts.");
        Assert.True(successCounter > 0, $"Expected some successful requests, but got {successCounter}.");
        Assert.True(inputVolumeMeterEventCount > 0, $"Expected some input volume meter events, but got {inputVolumeMeterEventCount}.");
    }

    [Fact]
    public async Task OpenClose()
    {
        using var client = testService.CreateClient();
        client.RequestTimeout = 500;
        for (int i = 0; i < 10; i++)
        {
            await testService.ConnectOrThrowAsync(client,
                eventSubscription: OBSStudioClient.Enums.EventSubscriptions.All
                    | OBSStudioClient.Enums.EventSubscriptions.InputVolumeMeters);
            var version = await client.GetVersion();
            Assert.NotNull(version);
            await Task.Delay(100);
            client.Disconnect();
            await testService.ConnectOrThrowAsync(client);
            version = await client.GetVersion();
            Assert.NotNull(version);
            client.Disconnect();
        }
    }

}
