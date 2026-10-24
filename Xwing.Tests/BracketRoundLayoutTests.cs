using Xwing.Web.Models;

namespace Xwing.Tests;

public sealed class BracketRoundLayoutTests
{
    [Fact]
    public void GetRoundNumberForPositionShouldReturnColumnIndex()
    {
        int? round = BracketRoundLayout.GetRoundNumberForPosition(7, [10, 10, 10]);
        round.ShouldBe(1);
        BracketRoundLayout.GetRoundNumberForPosition(10, [10, 10, 10]).ShouldBe(2);
        BracketRoundLayout.GetRoundNumberForPosition(25, [10, 10, 10]).ShouldBe(3);
    }

    [Fact]
    public void GetRoundNumberForPositionShouldReturnNullWhenNoColumns()
    {
        BracketRoundLayout.GetRoundNumberForPosition(3, []).ShouldBeNull();
    }

    [Fact]
    public void TryCreateInitialSplitShouldRequireMinimumWidth()
    {
        Result<List<int>> split = BracketRoundLayout.TryCreateInitialSplit(30);
        split.IsSuccess.ShouldBeTrue();
        split.Value.ShouldNotBeNull();
        split.Value.Sum().ShouldBe(30);
        split.Value[0].ShouldBeGreaterThanOrEqualTo(BracketRoundLayout.MinColumnWidthCells);
        BracketRoundLayout.TryCreateInitialSplit(9).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void TryResizeBoundaryShouldRespectMinimumColumnWidth()
    {
        Result<List<int>> resized = BracketRoundLayout.TryResizeBoundary([10, 10], 0, 3);
        resized.IsSuccess.ShouldBeTrue();
        resized.Value!.ShouldBe([13, 7]);
        BracketRoundLayout.TryResizeBoundary([10, 10], 0, 6).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void TryRemoveColumnWithTwoColumnsShouldClearAll()
    {
        Result<List<int>> cleared = BracketRoundLayout.TryRemoveColumn([15, 15], 0);
        cleared.IsSuccess.ShouldBeTrue();
        cleared.Value!.ShouldBeEmpty();
    }

    [Fact]
    public void TryAdjustForCanvasWidthChangeShouldUpdateLastColumn()
    {
        Result<List<int>> adjusted = BracketRoundLayout.TryAdjustForCanvasWidthChange([10, 10, 10], 35);
        adjusted.IsSuccess.ShouldBeTrue();
        adjusted.Value!.ShouldBe([10, 10, 15]);
        BracketRoundLayout.TryAdjustForCanvasWidthChange([10, 10, 10], 24).IsSuccess.ShouldBeFalse();
    }
}
