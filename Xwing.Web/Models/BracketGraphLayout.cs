namespace Xwing.Web.Models;
public static class BracketGraphLayout
{
    public const int CellSize = 40;
    public const int BoxOffset = 6;
    public const int BoxWidth = 168;
    public const int BoxHeight = 88;
    public const int DefaultGraphWidthCells = 30;
    public const int DefaultGraphHeightCells = 30;
    public const int MinGraphCells = 5;
    public const int MaxGraphCells = 500;
    public static int ToSurfacePixels(int graphCells) => graphCells * CellSize;
    public static int MaxMatchPositionX(int graphWidthCells) =>
        Math.Max(0, (graphWidthCells * CellSize - BoxOffset - BoxWidth) / CellSize);
    public static int MaxMatchPositionY(int graphHeightCells) =>
        Math.Max(0, (graphHeightCells * CellSize - BoxOffset - BoxHeight) / CellSize);

    public static (int PositionX, int PositionY) CenterMatchPosition(int graphWidthCells, int graphHeightCells) =>
        (MaxMatchPositionX(graphWidthCells) / 2, MaxMatchPositionY(graphHeightCells) / 2);

    public static Result? ValidateGraphSize(int widthCells, int heightCells)
    {
        if (widthCells < MinGraphCells || heightCells < MinGraphCells)
        {
            return Result.Fail($"Canvas must be at least {MinGraphCells} cells in each dimension.");
        }

        if (widthCells > MaxGraphCells || heightCells > MaxGraphCells)
        {
            return Result.Fail($"Canvas must be at most {MaxGraphCells} cells in each dimension.");
        }

        return null;
    }
}
