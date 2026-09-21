namespace LCMS.Domain.Identity;

/// <summary>Password policy for operator accounts (ADR-0021).</summary>
public static class PasswordRules
{
    public const int MinLength = 10;
    public const int MaxLength = 128;

    public static string? Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return "Mật khẩu không được để trống.";
        }

        if (password.Length < MinLength)
        {
            return $"Mật khẩu phải có ít nhất {MinLength} ký tự.";
        }

        if (password.Length > MaxLength)
        {
            return $"Mật khẩu không được vượt quá {MaxLength} ký tự.";
        }

        var hasLetter = password.Any(char.IsLetter);
        var hasDigit = password.Any(char.IsDigit);
        if (!hasLetter || !hasDigit)
        {
            return "Mật khẩu phải gồm chữ và số.";
        }

        return null;
    }
}
