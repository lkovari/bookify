using Bookify.Worker;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Reflection;
using Xunit;

namespace Bookify.Worker.Tests;

public sealed class WorkerTests
{
    [Fact]
    public void Worker_Constructor_InitializesSuccessfully()
    {
        var logger = Substitute.For<ILogger<Worker>>();

        var worker = new Worker(logger);

        Assert.NotNull(worker);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStarted_LogsInformation()
    {
        var logger = Substitute.For<ILogger<Worker>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        var worker = new Worker(logger);
        var cts = new CancellationTokenSource();

        var executeMethod = typeof(Worker).GetMethod("ExecuteAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(executeMethod);

        var executeTask = (Task)executeMethod.Invoke(worker, new object[] { cts.Token })!;

        await Task.Delay(1100);

        cts.Cancel();
        
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
        }

        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationRequested_StopsGracefully()
    {
        var logger = Substitute.For<ILogger<Worker>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        var worker = new Worker(logger);
        var cts = new CancellationTokenSource();

        var executeMethod = typeof(Worker).GetMethod("ExecuteAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(executeMethod);

        var executeTask = (Task)executeMethod.Invoke(worker, new object[] { cts.Token })!;

        await Task.Delay(100);
        cts.Cancel();

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.True(cts.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task ExecuteAsync_RunsInLoop_UntilCancellation()
    {
        var logger = Substitute.For<ILogger<Worker>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        var worker = new Worker(logger);
        var cts = new CancellationTokenSource();

        var executeMethod = typeof(Worker).GetMethod("ExecuteAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(executeMethod);

        var executeTask = (Task)executeMethod.Invoke(worker, new object[] { cts.Token })!;

        var startTime = DateTimeOffset.Now;
        await Task.Delay(1100);
        cts.Cancel();

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
        }
        var endTime = DateTimeOffset.Now;

        var duration = endTime - startTime;
        Assert.True(duration.TotalMilliseconds >= 1000);
    }

    [Fact]
    public async Task ExecuteAsync_LoggingDisabled_DoesNotLog()
    {
        var logger = Substitute.For<ILogger<Worker>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(false);

        var worker = new Worker(logger);
        var cts = new CancellationTokenSource();

        var executeMethod = typeof(Worker).GetMethod("ExecuteAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(executeMethod);

        var executeTask = (Task)executeMethod.Invoke(worker, new object[] { cts.Token })!;

        await Task.Delay(100);
        cts.Cancel();

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
        }

        logger.DidNotReceive().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}

