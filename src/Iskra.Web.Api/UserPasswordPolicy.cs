namespace Iskra.Web.Api;

internal enum UserPasswordPolicyError
{
    Empty,
    TooShort,
    InvalidCharacter,
    MissingUppercase,
    MissingLetter,
    MissingDigit
}

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
                    if (!Specials.Contains(c))
                    {
                        error = UserPasswordPolicyError.InvalidCharacter;
                        return false;
                    }

                    break;
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

    public static string Describe(UserPasswordPolicyError error) => error switch
    {
        UserPasswordPolicyError.Empty => "pass.empty",
        UserPasswordPolicyError.TooShort => "pass.too_short",
        UserPasswordPolicyError.InvalidCharacter => "pass.invalid_char",
        UserPasswordPolicyError.MissingUppercase => "pass.need_upper",
        UserPasswordPolicyError.MissingLetter => "pass.need_letter",
        UserPasswordPolicyError.MissingDigit => "pass.need_digit",
        _ => "pass.invalid"
    };
}
