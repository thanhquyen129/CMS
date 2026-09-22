using System.Text.RegularExpressions;

namespace LCMS.Application.Common;

/// <summary>Scrubs secrets from free-text that may land in logs or integration_errors.</summary>
public static partial class SecretRedactor
{
    private static readonly Regex Bearer = BearerRegex();
    private static readonly Regex PasswordKv = PasswordKvRegex();
    private static readonly Regex ApiKeyKv = ApiKeyKvRegex();
    private static readonly Regex ConnPassword = ConnPasswordRegex();

    public static string? Redact(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var s = Bearer.Replace(input, "Bearer [REDACTED]");
        s = PasswordKv.Replace(s, "$1=[REDACTED]");
        s = ApiKeyKv.Replace(s, "$1=[REDACTED]");
        s = ConnPassword.Replace(s, "$1=[REDACTED]");
        return s;
    }

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerRegex();

    [GeneratedRegex(@"(password|pwd|passwd|secret|client_secret)\s*[:=]\s*[^\s;,]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PasswordKvRegex();

    [GeneratedRegex(@"(api[_-]?key|access[_-]?token|refresh[_-]?token|authorization)\s*[:=]\s*[^\s;,]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ApiKeyKvRegex();

    [GeneratedRegex(@"(Password|Pwd)\s*=\s*[^;]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ConnPasswordRegex();
}
