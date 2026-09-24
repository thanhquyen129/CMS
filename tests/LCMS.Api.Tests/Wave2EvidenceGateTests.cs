namespace LCMS.Api.Tests;

public sealed class Wave2EvidenceGateTests
{
    [Fact]
    public void CloseGate_RefusesPixelPerfect_WhileScreenshotsAndOpenItemsRemain()
    {
        var path = FindUat();
        var text = File.ReadAllText(path);

        Assert.Contains("| UX-14 |", text, StringComparison.Ordinal);
        Assert.DoesNotContain("| **Pass*** |", text, StringComparison.Ordinal);

        var evidenceDir = Path.Combine(Path.GetDirectoryName(path)!, "evidence");
        var missing = Enumerable.Range(1, 15)
            .Count(i => !File.Exists(Path.Combine(evidenceDir, $"ui-{i:00}.png")));
        if (missing > 0)
        {
            Assert.Contains("chưa có file ảnh", text, StringComparison.Ordinal);
            Assert.Contains("P0/P1 mở không bằng 0", text, StringComparison.Ordinal);
            Assert.Contains("Không gọi Pixel-perfect xong", text, StringComparison.Ordinal);
        }
    }

    private static string FindUat()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "uat", "UAT-PIXEL-PERFECT-WAVE2.md");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Không thấy docs/uat/UAT-PIXEL-PERFECT-WAVE2.md từ thư mục test.");
    }
}
