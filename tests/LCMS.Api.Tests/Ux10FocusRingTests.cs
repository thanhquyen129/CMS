namespace LCMS.Api.Tests;

public sealed class Ux10FocusRingTests
{
    [Fact]
    public void PrimaryControls_KeepASolidFocusRing()
    {
        var css = File.ReadAllText(Find("apps", "web", "app", "globals.css"));
        var btn = css.IndexOf(".btn:focus-visible", StringComparison.Ordinal);
        Assert.True(btn >= 0);
        var btnBlock = css.Substring(btn, Math.Min(280, css.Length - btn));
        Assert.Contains("#0b3a75", btnBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("outline: none", btnBlock, StringComparison.Ordinal);

        var field = css.IndexOf(".field input:not([type=\"checkbox\"]):not([type=\"radio\"]):focus", StringComparison.Ordinal);
        Assert.True(field >= 0);
        var fieldBlock = css.Substring(field, Math.Min(320, css.Length - field));
        Assert.Contains("outline: 2px solid #0b3a75", fieldBlock, StringComparison.Ordinal);

        var login = File.ReadAllText(Find("apps", "web", "app", "login", "page.tsx"));
        Assert.Contains("tabIndex={-1}", login, StringComparison.Ordinal);
        Assert.Contains("id=\"login-error\"", login, StringComparison.Ordinal);
    }

    private static string Find(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(string.Join('/', parts));
    }
}
