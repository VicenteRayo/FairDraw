using FairDraw.Api.Errors;
using FairDraw.Crypto;

namespace FairDraw.Api.Services;

/// <summary>Structural validation shared by draw creation and editing (Created state only).</summary>
public static class DrawValidation
{
    public const int MinOptionsToLock = 2;
    public const int MinParticipantsToLock = 2;
    private const int MaxItems = 50;
    private const int MaxNameLength = 200;
    private const int MaxDrawNameLength = 200;

    public static void ValidateDraft(string name, IReadOnlyList<string> options, IReadOnlyList<string> participants)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxDrawNameLength)
        {
            throw ApiErrors.Validation("invalid_draw_name");
        }

        if (!CanonicalFormats.IsSafeForCanonical(name))
        {
            throw ApiErrors.Validation("draw_name_contains_reserved_character");
        }

        ValidateItemList(options, "option");
        ValidateItemList(participants, "participant");
    }

    public static void ValidateReadyToLock(IReadOnlyList<string> options, IReadOnlyList<string> participants)
    {
        if (options.Count < MinOptionsToLock)
        {
            throw ApiErrors.Validation("at_least_two_options_required");
        }

        if (participants.Count < MinParticipantsToLock)
        {
            throw ApiErrors.Validation("at_least_two_participants_required");
        }
    }

    private static void ValidateItemList(IReadOnlyList<string> items, string kind)
    {
        if (items.Count > MaxItems)
        {
            throw ApiErrors.Validation($"too_many_{kind}s");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item) || item.Length > MaxNameLength)
            {
                throw ApiErrors.Validation($"invalid_{kind}_name");
            }

            if (!CanonicalFormats.IsSafeForCanonical(item))
            {
                throw ApiErrors.Validation($"{kind}_name_contains_reserved_character");
            }

            if (!seen.Add(item))
            {
                throw ApiErrors.Validation($"duplicate_{kind}_name");
            }
        }
    }
}
