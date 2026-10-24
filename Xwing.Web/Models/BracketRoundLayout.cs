namespace Xwing.Web.Models;

public static class BracketRoundLayout
{
    public const int MinColumnWidthCells = 5;
    public const int MinCanvasWidthForFirstSplit = 10;

    public static int? GetRoundNumberForPosition(int positionX, IReadOnlyList<int> columnWidthCells)
    {
        ArgumentNullException.ThrowIfNull(columnWidthCells);
        if (columnWidthCells.Count == 0)
        {
            return null;
        }

        int start = 0;
        for (int i = 0; i < columnWidthCells.Count; i++)
        {
            int width = columnWidthCells[i];
            if (positionX >= start && positionX < start + width)
            {
                return i + 1;
            }

            start += width;
        }

        return null;
    }

    public static Result? ValidateColumnWidths(IReadOnlyList<int> columnWidthCells, int graphWidthCells)
    {
        ArgumentNullException.ThrowIfNull(columnWidthCells);
        if (columnWidthCells.Count == 0)
        {
            return null;
        }

        int sum = 0;
        foreach (int width in columnWidthCells)
        {
            if (width < MinColumnWidthCells)
            {
                return Result.Fail($"Each round column must be at least {MinColumnWidthCells} cells wide.");
            }

            sum += width;
        }

        if (sum != graphWidthCells)
        {
            return Result.Fail("Round column widths must fill the canvas width.");
        }

        return null;
    }

    public static Result<List<int>> TryCreateInitialSplit(int graphWidthCells)
    {
        if (graphWidthCells < MinCanvasWidthForFirstSplit)
        {
            return Result.Fail<List<int>>(
                $"Canvas must be at least {MinCanvasWidthForFirstSplit} cells wide to add rounds.");
        }

        int first = (graphWidthCells + 1) / 2;
        int second = graphWidthCells - first;
        return Result.Ok(new List<int> { first, second });
    }

    public static Result<List<int>> TryAddColumn(IReadOnlyList<int> current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.Count == 0)
        {
            return Result.Fail<List<int>>("Use initial split when no rounds exist.");
        }

        List<int> list = current.ToList();
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] > MinColumnWidthCells)
            {
                list[i] -= MinColumnWidthCells;
                list.Add(MinColumnWidthCells);
                return Result.Ok(list);
            }
        }

        return Result.Fail<List<int>>("No column can spare cells for another round.");
    }

    public static Result<List<int>> TryRemoveColumn(IReadOnlyList<int> current, int columnIndex)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (columnIndex < 0 || columnIndex >= current.Count)
        {
            return Result.Fail<List<int>>("Invalid column.");
        }

        if (current.Count == 2)
        {
            return Result.Ok(new List<int>());
        }

        List<int> list = current.ToList();
        int removed = list[columnIndex];
        list.RemoveAt(columnIndex);
        if (columnIndex == 0)
        {
            list[0] += removed;
        }
        else
        {
            list[columnIndex - 1] += removed;
        }

        return Result.Ok(list);
    }

    public static Result<List<int>> TryResizeBoundary(IReadOnlyList<int> current, int boundaryIndex, int deltaCells)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (boundaryIndex < 0 || boundaryIndex >= current.Count - 1)
        {
            return Result.Fail<List<int>>("Invalid boundary.");
        }

        if (deltaCells == 0)
        {
            return Result.Ok(current.ToList());
        }

        List<int> list = current.ToList();
        int newLeft = list[boundaryIndex] + deltaCells;
        int newRight = list[boundaryIndex + 1] - deltaCells;
        if (newLeft < MinColumnWidthCells || newRight < MinColumnWidthCells)
        {
            return Result.Fail<List<int>>(
                $"Each round column must stay at least {MinColumnWidthCells} cells wide.");
        }

        list[boundaryIndex] = newLeft;
        list[boundaryIndex + 1] = newRight;
        return Result.Ok(list);
    }

    public static Result<List<int>> TryAdjustForCanvasWidthChange(IReadOnlyList<int> current, int newGraphWidthCells)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.Count == 0)
        {
            return Result.Ok(new List<int>());
        }

        int oldSum = current.Sum();
        int delta = newGraphWidthCells - oldSum;
        List<int> list = current.ToList();
        int last = list[^1] + delta;
        if (last < MinColumnWidthCells)
        {
            return Result.Fail<List<int>>("Canvas width change would make the last round column too narrow.");
        }

        list[^1] = last;
        return Result.Ok(list);
    }

    public static int GetBoundaryCellX(int boundaryIndex, IReadOnlyList<int> columnWidthCells)
    {
        ArgumentNullException.ThrowIfNull(columnWidthCells);
        int cells = 0;
        for (int i = 0; i <= boundaryIndex; i++)
        {
            cells += columnWidthCells[i];
        }

        return cells;
    }
}
