using IntegrationTests.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;
using OBSStudioClient;
using OBSStudioClient.Enums;
using System.Diagnostics;
using Xunit.Sdk;

namespace IntegrationTests;

public class InputTests
{
    private readonly TestService testService = new();
    readonly string inputText = "chapter@1-content-text";

    [Fact]
    public async Task InputVolumeMeters_Test()
    {
        var eventCount = 0;
        using var client = testService.CreateClient();
        client.HighVolumeEventThrottleInterval = TimeSpan.FromMilliseconds(500);
        client.InputVolumeMeters += (sender, e) =>
        {
            Assert.NotNull(e);
            Assert.NotNull(e.Inputs);
            Assert.NotEmpty(e.Inputs);

            foreach (var input in e.Inputs)
            {
                Assert.NotNull(input.InputName);
                Assert.NotNull(input.InputLevelsMul);
                if (input.InputLevelsMul.Length > 0)
                {
                    Assert.True(input.InputLevelsMul[0].Length == 3);// livello RMS/magnitude, livello peak, livello peak senza filtro
                    Assert.True(input.InputLevelsMul[1].Length == 3);// livello RMS/magnitude, livello peak, livello peak senza filtro
                    Assert.True(input.GetLevelDb(0) > float.NegativeInfinity);
                    Assert.True(input.GetLevelDb(1) > float.NegativeInfinity);
                    var left = input.GetLevelDb(0);
                    var right = input.GetLevelDb(1);
                    Debug.WriteLine($"Received InputVolumeMeters event for input: {input.InputName}, level L: {left}db, level R: {right}db");
                }
            }
            eventCount++;
        };
        await testService.ConnectOrThrowAsync(client,
                eventSubscription: OBSStudioClient.Enums.EventSubscriptions.All
                | OBSStudioClient.Enums.EventSubscriptions.InputVolumeMeters);
        await Task.Delay(1600);
        Assert.True(eventCount > 3, "Expected to receive at least 3 InputVolumeMeters events");
    }

    [Fact]
    public async Task GetInputSettings_Test()
    {
        using var client = testService.CreateClient();
        client.RequestTimeout = 500;
        await testService.ConnectOrThrowAsync(client,
            eventSubscription: OBSStudioClient.Enums.EventSubscriptions.All
                | OBSStudioClient.Enums.EventSubscriptions.InputVolumeMeters);

        var timeoutCounter = 0;
        var successCounter = 0;
        await Parallel.ForEachAsync(Enumerable.Range(0, 10), async (i, ct) =>
        {
            try
            {
                await client.GetInputSettings(inputText);
                Interlocked.Increment(ref successCounter);
                await Task.Delay(600);
            }
            catch (TimeoutException)
            {
                Interlocked.Increment(ref timeoutCounter);
                await Task.Delay(1000);
            }
        });
        client.Disconnect();
        await testService.ConnectOrThrowAsync(client);
        await client.GetInputSettings(inputText);
        Assert.True(timeoutCounter == 0, $"Expected no timeouts, but got {timeoutCounter} timeouts.");
    }
}
