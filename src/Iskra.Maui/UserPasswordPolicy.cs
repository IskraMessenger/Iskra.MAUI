namespace Iskra.Maui;

internal enum UserPasswordPolicyError
{
    Empty,
    TooShort,
    InvalidCharacter,
    MissingUppercase,
    MissingLetter,
    MissingDigit
}

/// <summary>
/// User account password: at least 8 characters, Latin letters, one uppercase, digits;
/// optional specials <c>~!@#$%^&amp;*()\|/,.&lt;&gt;</c>.
/// </summary>
internal static class UserPasswordPolicy
{
    private const int MinLength = 8;
    public const string AllowedSpecialCharacters = "~!@#$%^&*()\\|/,.<>";

    private static readonly HashSet<char> Specials = new(AllowedSpecialCharacters);

    public static bool TryValidate(string? password, out UserPasswordPolicyError? error)
    {
        if (string.IsNullOrEmpty(password))
        {
            error = UserPasswordPolicyError.Empty;
            return false;
        }

        if (password.Length < MinLength)
        {
            error = UserPasswordPolicyError.TooShort;
            return false;
        }

        var hasUpper = false;
        var hasLetter = false;
        var hasDigit = false;

        foreach (var c in password)
        {
            switch (c)
            {
                case >= 'A' and <= 'Z':
                    hasUpper = true;
                    hasLetter = true;
                    break;
                case >= 'a' and <= 'z':
                    hasLetter = true;
                    break;
                case >= '0' and <= '9':
                    hasDigit = true;
                    break;
                default:
                {
                    if (!Specials.Contains(c))
                    {
                        error = UserPasswordPolicyError.InvalidCharacter;
                        return false;
                    }

                    break;
                }
            }
        }

        if (!hasUpper)
        {
            error = UserPasswordPolicyError.MissingUppercase;
            return false;
        }

        if (!hasLetter)
        {
            error = UserPasswordPolicyError.MissingLetter;
            return false;
        }

        if (!hasDigit)
        {
            error = UserPasswordPolicyError.MissingDigit;
            return false;
        }

        error = null;
        return true;
    }
}
