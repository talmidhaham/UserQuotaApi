using UserQuotaApi.API.Services;

namespace UserQuotaApi.UnitTests;

public class DataSourceSelectorTests
{
    [Theory]
    [InlineData(9,  true)]   // start of daytime
    [InlineData(12, true)]   // midday
    [InlineData(16, true)]   // last daytime hour
    [InlineData(17, false)]  // just after daytime
    [InlineData(8,  false)]  // just before daytime
    [InlineData(0,  false)]  // midnight
    [InlineData(23, false)]  // late night
    public void IsDaytime_ReturnsCorrectResult(int utcHour, bool expected)
    {
        var fakeTime = new DateTimeOffset(2024, 6, 1, utcHour, 0, 0, TimeSpan.Zero);
        var selector = new UtcTimeDataSourceSelector(new FakeTimeProvider(fakeTime));

        Assert.Equal(expected, selector.IsDaytime());
    }
}

/// <summary>Test double for TimeProvider — returns a fixed DateTimeOffset.</summary>
internal sealed class FakeTimeProvider(DateTimeOffset fixedTime) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => fixedTime;
}
