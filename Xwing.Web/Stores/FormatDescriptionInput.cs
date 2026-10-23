using Xwing.Web.Models;

namespace Xwing.Web.Stores;

internal static class FormatDescriptionInput
{
    public const int MaxLength = 4000;

    public static Result? Validate(string? formatDescription, out string? normalized)
    {
        normalized = string.IsNullOrWhiteSpace(formatDescription) ? null : formatDescription.Trim();
        if (normalized is not null && normalized.Length > MaxLength)
        {
            return Result.Fail($"Format description must be at most {MaxLength} characters.");
        }

        return null;
    }
}
