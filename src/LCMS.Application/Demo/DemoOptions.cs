namespace LCMS.Application.Demo;

public sealed class DemoOptions
{
    public const string SectionName = "Demo";

    /// <summary>When true, seed demo catalog once after migrate (idempotent).</summary>
    public bool SeedOnStartup { get; set; }

    /// <summary>When true, expose POST /api/dev/seed-demo outside Development.</summary>
    public bool AllowEndpoint { get; set; }

    /// <summary>Tenant code to seed into (same as Auth bootstrap, default ops).</summary>
    public string TenantCode { get; set; } = "ops";
}
