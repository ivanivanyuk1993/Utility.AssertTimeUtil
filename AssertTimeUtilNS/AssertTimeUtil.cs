using System.Reactive.Concurrency;
using System.Reactive.Linq;
using GetJsonDateProviderNS;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RunAfterProviderNS;

namespace AssertTimeUtilNS;

public static class AssertTimeUtil
{
    /// <summary>
    ///     Most likely we will use <see cref="Task"/>-s without schedulers, hence we can benefit from this variable
    ///     (from DRY perspective and performance perspective, since we will call `GetMaxRunAfterTimeError` only
    ///     once)
    /// </summary>
    public static readonly Task<TimeSpan> SafeToUseInTaskAssertsAllowedTimeErrorTask = GetMaxRunAfterTimeError()
        .ContinueWith(continuationFunction: maxRunAfterTimeErrorTask => maxRunAfterTimeErrorTask.Result * 2);

    private const int BigEnoughCountToGetSafeMaxExpensively = (int) 2e4;

    public static void AssertTimeWithAllowedError(
        DateTimeOffset expectedTime,
        DateTimeOffset actualTime,
        TimeSpan allowedToBeEarlierByTimeError,
        TimeSpan allowedToBeLaterByTimeError
    )
    {
        Assert.IsTrue(
            condition: expectedTime - allowedToBeEarlierByTimeError <= actualTime,
            message: $@"
{nameof(expectedTime)} - {nameof(allowedToBeEarlierByTimeError)} <= {nameof(actualTime)}
{expectedTime.GetJsonDate()} - {allowedToBeEarlierByTimeError.GetJsonTimeSpan()} <= {actualTime.GetJsonDate()}
"
        );
        Assert.IsTrue(
            condition: expectedTime + allowedToBeLaterByTimeError >= actualTime,
            message: $@"
{nameof(expectedTime)} + {nameof(allowedToBeLaterByTimeError)} >= {nameof(actualTime)}
{expectedTime.GetJsonDate()} + {allowedToBeLaterByTimeError.GetJsonTimeSpan()} >= {actualTime.GetJsonDate()}
"
        );
    }

    public static void AssertTimeWithAllowedError(
        DateTimeOffset expectedTime,
        DateTimeOffset actualTime,
        TimeSpan allowedToBeLaterByTimeError
    )
    {
        AssertTimeWithAllowedError(
            expectedTime: expectedTime,
            actualTime: actualTime,
            allowedToBeLaterByTimeError: allowedToBeLaterByTimeError,
            allowedToBeEarlierByTimeError: TimeSpan.Zero
        );
    }

    public static void AssertTimeWithAllowedErrorFromBothSides(
        DateTimeOffset expectedTime,
        DateTimeOffset actualTime,
        TimeSpan allowedTimeError
    )
    {
        AssertTimeWithAllowedError(
            expectedTime: expectedTime,
            actualTime: actualTime,
            allowedToBeLaterByTimeError: allowedTimeError,
            allowedToBeEarlierByTimeError: allowedTimeError
        );
    }

    public static IObservable<TimeSpan> GetMaxRunAfterTimeError(IScheduler scheduler)
    {
        var runAfterTimeSpan = TimeSpan.FromMilliseconds(value: 1);
        return Enumerable
            .Range(
                start: 0,
                count: BigEnoughCountToGetSafeMaxExpensively
            )
            .Select(
                selector: _ => Observable.FromAsync(
                    functionAsync: async () =>
                    {
                        var startTime = DateTimeOffset.UtcNow;
                        await RunAfterProvider.RunInOrAfter(
                            timeSpan: runAfterTimeSpan,
                            scheduler: scheduler
                        );
                        var finishTime = DateTimeOffset.UtcNow;
                        return finishTime - startTime - runAfterTimeSpan;
                    },
                    scheduler: scheduler
                )
            )
            .Merge(scheduler: scheduler)
            .Max();
    }

    private static async Task<TimeSpan> GetMaxRunAfterTimeError()
    {
        var runAfterTimeSpan = TimeSpan.FromMilliseconds(value: 1);
        var timeErrorList = await Task.WhenAll(
            tasks: Enumerable
                .Range(
                    start: 0,
                    count: BigEnoughCountToGetSafeMaxExpensively
                )
                .Select(
                    selector: _ => Task.Run(
                        function: async () =>
                        {
                            var startTime = DateTimeOffset.UtcNow;
                            await RunAfterProvider.RunInOrAfter(
                                timeSpan: runAfterTimeSpan,
                                cancellationToken: CancellationToken.None
                            );
                            var finishTime = DateTimeOffset.UtcNow;
                            return finishTime - startTime - runAfterTimeSpan;
                        }
                    )
                )
        );
        return timeErrorList.Max();
    }
}