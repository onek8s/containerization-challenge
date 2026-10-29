using Coho.Billing.Core.Reads;
using Xunit;

namespace Coho.Billing.Core.Tests;

public class ReadCheckTests
{
    private static readonly DateTime Today = new DateTime(2026, 5, 2);
    private readonly ReadValidator _validator = new ReadValidator(new ReadCheckSettings());

    [Fact]
    public void Normal_read_passes()
    {
        var result = _validator.Check(TestData.Account(), TestData.Read(new DateTime(2026, 1, 28), 1832.4m), TestData.Read(new DateTime(2026, 4, 28), 1863.1m), Today);

        Assert.True(result.Passed);
        Assert.Equal(90, result.Days);
        Assert.Equal(30.7m, result.Consumption);
    }

    [Fact]
    public void High_consumption_is_an_exception()
    {
        var result = _validator.Check(TestData.Account(0.21m), TestData.Read(new DateTime(2026, 1, 28), 977m), TestData.Read(new DateTime(2026, 4, 28), 1045m), Today);

        Assert.Equal(new[] { ReadExceptionCodes.High }, result.Exceptions);
    }

    [Fact]
    public void Low_consumption_is_an_exception()
    {
        var result = _validator.Check(TestData.Account(), TestData.Read(new DateTime(2026, 1, 28), 1000m), TestData.Read(new DateTime(2026, 4, 28), 1004m), Today);

        Assert.Equal(new[] { ReadExceptionCodes.Low }, result.Exceptions);
    }

    [Fact]
    public void Small_users_are_not_checked_for_high_or_low()
    {
        var result = _validator.Check(TestData.Account(0.01m), TestData.Read(new DateTime(2026, 1, 28), 10m), TestData.Read(new DateTime(2026, 4, 28), 14m), Today);

        Assert.True(result.Passed);
    }

    [Fact]
    public void Register_rollover_is_detected()
    {
        var account = TestData.Account(0.28m, dials: 4);
        var result = _validator.Check(account, TestData.Read(new DateTime(2026, 1, 29), 9985.0m), TestData.Read(new DateTime(2026, 4, 28), 9.8m), Today);

        Assert.True(result.Rollover);
        Assert.Equal(24.8m, result.Consumption);
        Assert.True(result.Passed);
    }

    [Fact]
    public void Negative_advance_is_an_exception()
    {
        var result = _validator.Check(TestData.Account(), TestData.Read(new DateTime(2026, 1, 28), 1832.4m), TestData.Read(new DateTime(2026, 4, 28), 1820m), Today);

        Assert.False(result.Rollover);
        Assert.Equal(new[] { ReadExceptionCodes.Negative }, result.Exceptions);
        Assert.True(result.Consumption < 0);
    }

    [Fact]
    public void Read_before_previous_is_out_of_sequence()
    {
        var result = _validator.Check(TestData.Account(), TestData.Read(new DateTime(2026, 1, 28), 1832.4m), TestData.Read(new DateTime(2026, 1, 20), 1840m), Today);

        Assert.Equal(new[] { ReadExceptionCodes.OutOfSequence }, result.Exceptions);
    }

    [Fact]
    public void Zero_consumption_and_future_date_are_reported()
    {
        var result = _validator.Check(TestData.Account(), TestData.Read(new DateTime(2026, 1, 28), 1832.4m), TestData.Read(new DateTime(2026, 5, 10), 1832.4m), Today);

        Assert.Contains(ReadExceptionCodes.Zero, result.Exceptions);
        Assert.Contains(ReadExceptionCodes.FutureDate, result.Exceptions);
    }

    [Fact]
    public void Multiplier_is_applied_and_leak_flag_reported()
    {
        var account = TestData.Account(61.5m, dials: 6, multiplier: 10m);
        var result = _validator.Check(account, TestData.Read(new DateTime(2026, 3, 31), 7710.2m), TestData.Read(new DateTime(2026, 4, 30), 7894.9m, "LK"), Today);

        Assert.Equal(1847m, result.Consumption);
        Assert.Equal(new[] { ReadExceptionCodes.Leak }, result.Exceptions);
    }

    [Fact]
    public void Estimate_follows_the_seasonal_profile()
    {
        var estimator = new ConsumptionEstimator();
        var january = estimator.EstimateConsumption(1m, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));
        var july = estimator.EstimateConsumption(1m, new DateTime(2026, 7, 1), new DateTime(2026, 8, 1));

        Assert.Equal(29m, january);
        Assert.Equal(37m, july);
    }

    [Fact]
    public void Estimated_read_rolls_over_the_register()
    {
        var account = TestData.Account(1m, dials: 4);
        var estimate = new ConsumptionEstimator().Estimate(account, TestData.Read(new DateTime(2026, 1, 1), 9990m), new DateTime(2026, 2, 1));

        Assert.Equal(ReadType.Estimated, estimate.ReadType);
        Assert.Equal(19m, estimate.Reading);
        Assert.Equal("EST", estimate.ReaderCode);
    }
}
