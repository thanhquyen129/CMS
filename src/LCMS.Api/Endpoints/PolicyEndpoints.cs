using LCMS.Application.Policies.Commands;
using LCMS.Application.Policies.Queries;
using MediatR;

namespace LCMS.Api.Endpoints;

public static class PolicyEndpoints
{
    public static IEndpointRouteBuilder MapPolicyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/policies").WithTags("Policies");

        group.MapPost("/ensure-catalog", async (ISender sender, CancellationToken ct) =>
        {
            var created = await sender.Send(new EnsurePolicyCatalogCommand(), ct);
            return Results.Ok(new { created });
        });

        group.MapGet("/", async (
            string? policyKey,
            bool? latestOnly,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(new ListPoliciesQuery(policyKey, latestOnly ?? true), ct);
            return Results.Ok(list);
        });

        group.MapGet("/{policyKey}/active", async (
            string policyKey,
            DateOnly? asOf,
            ISender sender,
            CancellationToken ct) =>
        {
            var item = await sender.Send(new GetActivePolicyByKeyQuery(policyKey, asOf), ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        group.MapPut("/", async (UpsertPolicyRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new UpsertPolicyCommand(
                    body.PolicyKey,
                    body.Title,
                    body.OwnerUserId,
                    body.EffectiveFrom,
                    body.EffectiveTo,
                    body.Status,
                    body.BodyJson,
                    body.Notes,
                    body.CreateNewVersion),
                ct);
            return Results.Ok(new { id });
        });

        return app;
    }
}

public sealed record UpsertPolicyRequest(
    string PolicyKey,
    string? Title,
    Guid? OwnerUserId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string Status,
    string? BodyJson,
    string? Notes,
    bool CreateNewVersion = false);
