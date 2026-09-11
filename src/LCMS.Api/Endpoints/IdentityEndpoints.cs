using LCMS.Application.Roles.Commands;
using LCMS.Application.Roles.Queries;
using LCMS.Application.Users.Commands;
using LCMS.Application.Users.Queries;
using MediatR;

namespace LCMS.Api.Endpoints;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/api/users").WithTags("Users");
        users.MapPost("/", async (CreateUserRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new CreateUserCommand(body.Email, body.DisplayName, body.OrganizationId),
                ct);
            return Results.Created($"/api/users/{id}", new { id });
        });
        users.MapGet("/", async (ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListUsersQuery(), ct);
            return Results.Ok(list);
        });
        users.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var user = await sender.Send(new GetUserByIdQuery(id), ct);
            return Results.Ok(user);
        });
        users.MapPut("/{id:guid}", async (
            Guid id,
            UpdateUserRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(
                new UpdateUserCommand(id, body.DisplayName, body.IsActive, body.OrganizationId),
                ct);
            return Results.NoContent();
        });
        users.MapPost("/{userId:guid}/roles/{roleId:guid}", async (
            Guid userId,
            Guid roleId,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new AssignUserRoleCommand(userId, roleId), ct);
            return Results.NoContent();
        });

        var roles = app.MapGroup("/api/roles").WithTags("Roles");
        roles.MapPost("/", async (CreateRoleRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(new CreateRoleCommand(body.Code, body.Name), ct);
            return Results.Created($"/api/roles/{id}", new { id });
        });
        roles.MapGet("/", async (ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListRolesQuery(), ct);
            return Results.Ok(list);
        });
        roles.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var role = await sender.Send(new GetRoleByIdQuery(id), ct);
            return Results.Ok(role);
        });
        roles.MapGet("/{id:guid}/permissions", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListRolePermissionsQuery(id), ct);
            return Results.Ok(list);
        });
        roles.MapPost("/{id:guid}/permissions", async (
            Guid id,
            AssignRolePermissionRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var permissionId = await sender.Send(
                new AssignRolePermissionCommand(id, body.ActionCode, body.DataScope),
                ct);
            return Results.Created($"/api/roles/{id}/permissions/{permissionId}", new { id = permissionId });
        });

        return app;
    }
}

public sealed record CreateUserRequest(string Email, string DisplayName, Guid? OrganizationId = null);
public sealed record UpdateUserRequest(string DisplayName, bool IsActive, Guid? OrganizationId);
public sealed record CreateRoleRequest(string Code, string Name);
public sealed record AssignRolePermissionRequest(string ActionCode, string DataScope);
