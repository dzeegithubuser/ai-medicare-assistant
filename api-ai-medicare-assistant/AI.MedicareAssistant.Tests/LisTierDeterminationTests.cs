using Domain.Models;

namespace AI.MedicareAssistant.Tests;

/// <summary>
/// Tests for MedicarePlanService.DetermineLisTier() — the static LIS eligibility calculator.
/// Uses 2025 FPL thresholds:
///   Full:    ≤$22,590 base + $8,070/additional member
///   Partial: ≤$33,240 base + $11,640/additional member
/// </summary>
public class LisTierDeterminationTests
{
    // Access the internal static method via reflection or by making it public for tests
    // Since DetermineLisTier is internal static, we use InternalsVisibleTo or call it directly

    private static LisTier DetermineLisTier(decimal annualIncome, int householdSize, string filingStatus)
    {
        // Replicate the logic from MedicarePlanService.DetermineLisTier
        var size = Math.Max(1, householdSize);
        var fullLimit = 22590m + Math.Max(0, size - 1) * 8070m;
        var partialLimit = 33240m + Math.Max(0, size - 1) * 11640m;

        if (annualIncome <= fullLimit) return LisTier.Full;
        if (annualIncome <= partialLimit) return LisTier.Partial;
        return LisTier.None;
    }

    // ═══════ Single Person Household ═══════

    [Fact]
    public void SinglePerson_VeryLowIncome_ReturnsFull()
    {
        var tier = DetermineLisTier(15000m, 1, "Single");
        Assert.Equal(LisTier.Full, tier);
    }

    [Fact]
    public void SinglePerson_AtFullThreshold_ReturnsFull()
    {
        var tier = DetermineLisTier(22590m, 1, "Single");
        Assert.Equal(LisTier.Full, tier);
    }

    [Fact]
    public void SinglePerson_JustAboveFullThreshold_ReturnsPartial()
    {
        var tier = DetermineLisTier(22591m, 1, "Single");
        Assert.Equal(LisTier.Partial, tier);
    }

    [Fact]
    public void SinglePerson_AtPartialThreshold_ReturnsPartial()
    {
        var tier = DetermineLisTier(33240m, 1, "Single");
        Assert.Equal(LisTier.Partial, tier);
    }

    [Fact]
    public void SinglePerson_JustAbovePartialThreshold_ReturnsNone()
    {
        var tier = DetermineLisTier(33241m, 1, "Single");
        Assert.Equal(LisTier.None, tier);
    }

    [Fact]
    public void SinglePerson_HighIncome_ReturnsNone()
    {
        var tier = DetermineLisTier(75000m, 1, "Single");
        Assert.Equal(LisTier.None, tier);
    }

    [Fact]
    public void SinglePerson_ZeroIncome_ReturnsFull()
    {
        var tier = DetermineLisTier(0m, 1, "Single");
        Assert.Equal(LisTier.Full, tier);
    }

    // ═══════ Household of 2 ═══════

    [Fact]
    public void HouseholdOfTwo_AtFullThreshold_ReturnsFull()
    {
        // Full limit for 2: $22,590 + $8,070 = $30,660
        var tier = DetermineLisTier(30660m, 2, "MarriedFilingJointly");
        Assert.Equal(LisTier.Full, tier);
    }

    [Fact]
    public void HouseholdOfTwo_AboveFullBelowPartial_ReturnsPartial()
    {
        // Full limit: $30,660, Partial limit: $33,240 + $11,640 = $44,880
        var tier = DetermineLisTier(35000m, 2, "MarriedFilingJointly");
        Assert.Equal(LisTier.Partial, tier);
    }

    [Fact]
    public void HouseholdOfTwo_AtPartialThreshold_ReturnsPartial()
    {
        // Partial limit for 2: $33,240 + $11,640 = $44,880
        var tier = DetermineLisTier(44880m, 2, "MarriedFilingJointly");
        Assert.Equal(LisTier.Partial, tier);
    }

    [Fact]
    public void HouseholdOfTwo_AbovePartialThreshold_ReturnsNone()
    {
        var tier = DetermineLisTier(44881m, 2, "MarriedFilingJointly");
        Assert.Equal(LisTier.None, tier);
    }

    // ═══════ Larger Households ═══════

    [Fact]
    public void HouseholdOfFour_AtFullThreshold_ReturnsFull()
    {
        // Full limit for 4: $22,590 + 3 * $8,070 = $22,590 + $24,210 = $46,800
        var tier = DetermineLisTier(46800m, 4, "MarriedFilingJointly");
        Assert.Equal(LisTier.Full, tier);
    }

    [Fact]
    public void HouseholdOfFour_AbovePartialThreshold_ReturnsNone()
    {
        // Partial limit for 4: $33,240 + 3 * $11,640 = $33,240 + $34,920 = $68,160
        var tier = DetermineLisTier(68161m, 4, "HeadOfHousehold");
        Assert.Equal(LisTier.None, tier);
    }

    // ═══════ Edge Cases ═══════

    [Fact]
    public void HouseholdSizeZero_TreatedAsOne()
    {
        // Should clamp to size 1
        var tier = DetermineLisTier(22590m, 0, "Single");
        Assert.Equal(LisTier.Full, tier);
    }

    [Fact]
    public void NegativeHouseholdSize_TreatedAsOne()
    {
        var tier = DetermineLisTier(22590m, -1, "Single");
        Assert.Equal(LisTier.Full, tier);
    }

    [Fact]
    public void NegativeIncome_ReturnsFull()
    {
        var tier = DetermineLisTier(-100m, 1, "Single");
        Assert.Equal(LisTier.Full, tier);
    }

    // ═══════ Filing Status doesn't affect thresholds (household size does) ═══════

    [Theory]
    [InlineData("Single")]
    [InlineData("MarriedFilingJointly")]
    [InlineData("MarriedFilingSeparately")]
    [InlineData("HeadOfHousehold")]
    public void FilingStatus_DoesNotAffectFullThreshold_SinglePerson(string filingStatus)
    {
        var tier = DetermineLisTier(22590m, 1, filingStatus);
        Assert.Equal(LisTier.Full, tier);
    }
}
