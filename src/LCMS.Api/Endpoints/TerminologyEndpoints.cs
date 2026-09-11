using LCMS.Domain.Terminology;

namespace LCMS.Api.Endpoints;

public static class TerminologyEndpoints
{
    public static IEndpointRouteBuilder MapTerminologyEndpoints(this IEndpointRouteBuilder app)
    {
        // UI must consume Vietnamese terms from this contract — never show raw English enums.
        app.MapGet("/api/terminology", () => Results.Ok(VietnameseUiTerms.All))
            .WithTags("Terminology")
            .WithSummary("CP6.5 Vietnamese UI terminology dictionary (CodeKey → Vietnamese term)")
            .AllowAnonymous();

        return app;
    }
}
