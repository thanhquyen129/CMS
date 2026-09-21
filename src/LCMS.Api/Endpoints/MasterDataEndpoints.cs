using LCMS.Application.BusinessParties.Commands;
using LCMS.Application.BusinessParties.Queries;
using LCMS.Application.Catalog.Commands;
using LCMS.Application.Catalog.Queries;
using LCMS.Application.Currencies.Commands;
using LCMS.Application.Currencies.Queries;
using LCMS.Application.Fx.Commands;
using LCMS.Application.Fx.Queries;
using LCMS.Application.Organizations.Commands;
using LCMS.Application.Organizations.Queries;
using LCMS.Application.PartyBankAccounts.Commands;
using LCMS.Application.PartyBankAccounts.Queries;
using LCMS.Application.PartyContacts.Commands;
using LCMS.Application.PartyContacts.Queries;
using LCMS.Application.PartyRoles.Commands;
using LCMS.Application.PartyRoles.Queries;
using MediatR;

namespace LCMS.Api.Endpoints;

public static class MasterDataEndpoints
{
    public static IEndpointRouteBuilder MapMasterDataEndpoints(this IEndpointRouteBuilder app)
    {
        var orgs = app.MapGroup("/api/organizations").WithTags("Organizations");
        orgs.MapPost("/", async (CreateOrganizationRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new CreateOrganizationCommand(body.Code, body.Name, body.ParentId),
                ct);
            return Results.Created($"/api/organizations/{id}", new { id });
        });
        orgs.MapGet("/", async (ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListOrganizationsQuery(), ct);
            return Results.Ok(list);
        });
        orgs.MapGet("/tree", async (ISender sender, CancellationToken ct) =>
        {
            var tree = await sender.Send(new GetOrganizationTreeQuery(), ct);
            return Results.Ok(tree);
        });
        orgs.MapGet("/{id:guid}/children", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var children = await sender.Send(new ListOrganizationChildrenQuery(id), ct);
            return Results.Ok(children);
        });
        orgs.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var org = await sender.Send(new GetOrganizationByIdQuery(id), ct);
            return Results.Ok(org);
        });
        orgs.MapPut("/{id:guid}", async (
            Guid id,
            UpdateOrganizationRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(
                new UpdateOrganizationCommand(id, body.Name, body.ParentId, body.IsActive),
                ct);
            return Results.NoContent();
        });
        orgs.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new SoftDeleteOrganizationCommand(id), ct);
            return Results.NoContent();
        });

        var parties = app.MapGroup("/api/business-parties").WithTags("BusinessParties");
        parties.MapGet("/lookup", async (
            string? q,
            string? roleCode,
            bool? usableOnly,
            int? take,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(
                new LookupBusinessPartiesQuery(q, roleCode, usableOnly ?? true, take ?? 20),
                ct);
            return Results.Ok(list);
        });
        parties.MapGet("/directory", async (
            string? search,
            string? roleCode,
            string? status,
            string? kind,
            string? groupCode,
            int? page,
            int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(
                new ListBusinessPartyDirectoryQuery(search, roleCode, status, kind, groupCode, page, pageSize),
                ct);
            return Results.Ok(list);
        });
        parties.MapGet("/summary", async (
            string? search,
            string? roleCode,
            string? status,
            string? kind,
            string? groupCode,
            ISender sender,
            CancellationToken ct) =>
        {
            var summary = await sender.Send(
                new GetBusinessPartyDirectorySummaryQuery(search, roleCode, status, kind, groupCode),
                ct);
            return Results.Ok(summary);
        });
        parties.MapGet("/duplicates", async (
            string? taxId,
            string? phone,
            string? email,
            Guid? excludeId,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(
                new GetBusinessPartyDuplicatesQuery(taxId, phone, email, excludeId),
                ct);
            return Results.Ok(list);
        });
        parties.MapGet("/export", async (
            string? search,
            string? roleCode,
            string? status,
            string? kind,
            string? groupCode,
            ISender sender,
            CancellationToken ct) =>
        {
            var csv = await sender.Send(
                new ExportBusinessPartyDirectoryQuery(search, roleCode, status, kind, groupCode),
                ct);
            return Results.File(csv, "text/csv; charset=utf-8", $"doi-tac-{DateTime.UtcNow:yyyyMMdd}.csv");
        });
        parties.MapPost("/", async (CreateBusinessPartyRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new CreateBusinessPartyCommand(
                    body.Code,
                    body.Name,
                    body.LegalName,
                    body.TaxId,
                    body.Phone,
                    body.Email,
                    body.Website,
                    body.AddressLine1,
                    body.AddressLine2,
                    body.Ward,
                    body.District,
                    body.City,
                    body.Province,
                    body.CountryCode,
                    body.PostalCode,
                    body.DefaultCurrencyCode,
                    body.PaymentTermDays,
                    body.CreditLimit,
                    body.CreditLimitCurrencyCode,
                    body.Notes,
                    body.RoleCodes,
                    body.PartyKind,
                    body.ShortName,
                    body.LegalType,
                    body.GroupCode,
                    body.ExternalCode,
                    body.IndustryCode,
                    body.InvoiceEmail,
                    body.VatRegistered,
                    body.AssignedUserId,
                    body.ParentPartyId,
                    body.CreditControlMode),
                ct);
            return Results.Created($"/api/business-parties/{id}", new { id });
        });
        parties.MapGet("/", async (
            string? search,
            string? roleCode,
            bool? isActive,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(new ListBusinessPartiesQuery(search, roleCode, isActive), ct);
            return Results.Ok(list);
        });
        parties.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var party = await sender.Send(new GetBusinessPartyByIdQuery(id), ct);
            return Results.Ok(party);
        });
        parties.MapGet("/{id:guid}/financial", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var view = await sender.Send(new GetBusinessPartyFinancialQuery(id), ct);
            return Results.Ok(view);
        });
        parties.MapPost("/{id:guid}/block", async (
            Guid id,
            BlockBusinessPartyRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new BlockBusinessPartyCommand(id, body.Reason), ct);
            return Results.NoContent();
        });
        parties.MapPost("/{id:guid}/unblock", async (
            Guid id,
            UnblockBusinessPartyRequest? body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new UnblockBusinessPartyCommand(id, body?.Reason), ct);
            return Results.NoContent();
        });
        parties.MapPut("/{id:guid}", async (
            Guid id,
            UpdateBusinessPartyRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(
                new UpdateBusinessPartyCommand(
                    id,
                    body.Name,
                    body.IsActive,
                    body.LegalName,
                    body.TaxId,
                    body.Phone,
                    body.Email,
                    body.Website,
                    body.AddressLine1,
                    body.AddressLine2,
                    body.Ward,
                    body.District,
                    body.City,
                    body.Province,
                    body.CountryCode,
                    body.PostalCode,
                    body.DefaultCurrencyCode,
                    body.PaymentTermDays,
                    body.CreditLimit,
                    body.CreditLimitCurrencyCode,
                    body.Notes,
                    body.PartyKind,
                    body.ShortName,
                    body.LegalType,
                    body.GroupCode,
                    body.ExternalCode,
                    body.IndustryCode,
                    body.InvoiceEmail,
                    body.VatRegistered,
                    body.AssignedUserId,
                    body.ParentPartyId,
                    body.CreditControlMode),
                ct);
            return Results.NoContent();
        });
        parties.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new SoftDeleteBusinessPartyCommand(id), ct);
            return Results.NoContent();
        });
        parties.MapPost("/{id:guid}/roles", async (
            Guid id,
            AssignPartyRoleRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var roleId = await sender.Send(new AssignPartyRoleCommand(id, body.RoleCode), ct);
            return Results.Created($"/api/business-parties/{id}/roles/{body.RoleCode}", new { id = roleId });
        });
        parties.MapGet("/{id:guid}/roles", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListPartyRolesQuery(id), ct);
            return Results.Ok(list);
        });
        parties.MapDelete("/{id:guid}/roles/{roleCode}", async (
            Guid id,
            string roleCode,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new RevokePartyRoleCommand(id, roleCode), ct);
            return Results.NoContent();
        });

        parties.MapGet("/{id:guid}/bank-accounts", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListPartyBankAccountsQuery(id), ct);
            return Results.Ok(list);
        });
        parties.MapPost("/{id:guid}/bank-accounts", async (
            Guid id,
            UpsertPartyBankAccountRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var bankId = await sender.Send(
                new UpsertPartyBankAccountCommand(
                    id,
                    null,
                    body.BankName,
                    body.BankBranch,
                    body.BankCode,
                    body.SwiftBic,
                    body.AccountNumber,
                    body.AccountName,
                    body.CurrencyCode,
                    body.IsDefault,
                    body.IsActive,
                    body.Note),
                ct);
            return Results.Created($"/api/business-parties/{id}/bank-accounts/{bankId}", new { id = bankId });
        });
        parties.MapPut("/{id:guid}/bank-accounts/{bankAccountId:guid}", async (
            Guid id,
            Guid bankAccountId,
            UpsertPartyBankAccountRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(
                new UpsertPartyBankAccountCommand(
                    id,
                    bankAccountId,
                    body.BankName,
                    body.BankBranch,
                    body.BankCode,
                    body.SwiftBic,
                    body.AccountNumber,
                    body.AccountName,
                    body.CurrencyCode,
                    body.IsDefault,
                    body.IsActive,
                    body.Note),
                ct);
            return Results.NoContent();
        });
        parties.MapDelete("/{id:guid}/bank-accounts/{bankAccountId:guid}", async (
            Guid id,
            Guid bankAccountId,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new SoftDeletePartyBankAccountCommand(id, bankAccountId), ct);
            return Results.NoContent();
        });

        parties.MapGet("/{id:guid}/contacts", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListPartyContactsQuery(id), ct);
            return Results.Ok(list);
        });
        parties.MapPost("/{id:guid}/contacts", async (
            Guid id,
            UpsertPartyContactRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var contactId = await sender.Send(
                new UpsertPartyContactCommand(
                    id,
                    null,
                    body.FullName,
                    body.Title,
                    body.FunctionCode,
                    body.Phone,
                    body.Email,
                    body.IsPrimary,
                    body.IsActive,
                    body.Note),
                ct);
            return Results.Created($"/api/business-parties/{id}/contacts/{contactId}", new { id = contactId });
        });
        parties.MapPut("/{id:guid}/contacts/{contactId:guid}", async (
            Guid id,
            Guid contactId,
            UpsertPartyContactRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(
                new UpsertPartyContactCommand(
                    id,
                    contactId,
                    body.FullName,
                    body.Title,
                    body.FunctionCode,
                    body.Phone,
                    body.Email,
                    body.IsPrimary,
                    body.IsActive,
                    body.Note),
                ct);
            return Results.NoContent();
        });
        parties.MapDelete("/{id:guid}/contacts/{contactId:guid}", async (
            Guid id,
            Guid contactId,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new SoftDeletePartyContactCommand(id, contactId), ct);
            return Results.NoContent();
        });

        var currencies = app.MapGroup("/api/currencies").WithTags("Currencies");
        currencies.MapGet("/", async (bool? activeOnly, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListCurrenciesQuery(activeOnly), ct);
            return Results.Ok(list);
        });
        currencies.MapGet("/{code}", async (string code, ISender sender, CancellationToken ct) =>
        {
            var currency = await sender.Send(new GetCurrencyByCodeQuery(code), ct);
            return Results.Ok(currency);
        });
        currencies.MapPut("/", async (UpsertCurrencyRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new UpsertCurrencyCommand(body.Code, body.Name, body.DecimalPlaces, body.IsActive),
                ct);
            return Results.Ok(new { id });
        });

        var fxRates = app.MapGroup("/api/fx-rates").WithTags("FxRates");
        fxRates.MapGet("/", async (
            string? fromCurrencyCode,
            string? toCurrencyCode,
            DateOnly? fromDate,
            DateOnly? toDate,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(
                new ListFxRatesQuery(fromCurrencyCode, toCurrencyCode, fromDate, toDate),
                ct);
            return Results.Ok(list);
        });
        fxRates.MapGet("/resolve", async (
            string fromCurrencyCode,
            string toCurrencyCode,
            DateOnly asOf,
            ISender sender,
            CancellationToken ct) =>
        {
            var row = await sender.Send(
                new ResolveFxRateQuery(fromCurrencyCode, toCurrencyCode, asOf),
                ct);
            return row is null ? Results.NotFound() : Results.Ok(row);
        });
        fxRates.MapPost("/", async (UpsertFxRateRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new UpsertFxRateCommand(
                    body.FromCurrencyCode,
                    body.ToCurrencyCode,
                    body.RateDate,
                    body.Rate,
                    body.Source,
                    body.Version,
                    body.Note),
                ct);
            return Results.Created($"/api/fx-rates/{id}", new { id });
        });
        fxRates.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new SoftDeleteFxRateCommand(id), ct);
            return Results.NoContent();
        });

        var catalog = app.MapGroup("/api/master-catalog").WithTags("MasterCatalog");
        catalog.MapGet("/", async (string? kind, bool? activeOnly, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListMasterCatalogItemsQuery(kind, activeOnly), ct);
            return Results.Ok(list);
        });
        catalog.MapPut("/", async (UpsertMasterCatalogItemRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new UpsertMasterCatalogItemCommand(
                    body.Kind,
                    body.Code,
                    body.Name,
                    body.Description,
                    body.IsActive ?? true,
                    body.SortOrder,
                    body.AttributesJson),
                ct);
            return Results.Ok(new { id });
        });

        return app;
    }
}

public sealed record UpsertFxRateRequest(
    string FromCurrencyCode,
    string ToCurrencyCode,
    DateOnly RateDate,
    decimal Rate,
    string? Source,
    int? Version,
    string? Note);

public sealed record UpsertMasterCatalogItemRequest(
    string Kind,
    string Code,
    string Name,
    string? Description,
    bool? IsActive,
    int? SortOrder,
    string? AttributesJson = null);

public sealed record CreateOrganizationRequest(string Code, string Name, Guid? ParentId);
public sealed record UpdateOrganizationRequest(string Name, Guid? ParentId, bool IsActive);
public sealed record CreateBusinessPartyRequest(
    string Code,
    string Name,
    string? LegalName = null,
    string? TaxId = null,
    string? Phone = null,
    string? Email = null,
    string? Website = null,
    string? AddressLine1 = null,
    string? AddressLine2 = null,
    string? Ward = null,
    string? District = null,
    string? City = null,
    string? Province = null,
    string? CountryCode = null,
    string? PostalCode = null,
    string? DefaultCurrencyCode = null,
    int? PaymentTermDays = null,
    decimal? CreditLimit = null,
    string? CreditLimitCurrencyCode = null,
    string? Notes = null,
    IReadOnlyList<string>? RoleCodes = null,
    string? PartyKind = null,
    string? ShortName = null,
    string? LegalType = null,
    string? GroupCode = null,
    string? ExternalCode = null,
    string? IndustryCode = null,
    string? InvoiceEmail = null,
    bool? VatRegistered = null,
    Guid? AssignedUserId = null,
    Guid? ParentPartyId = null,
    string? CreditControlMode = null);
public sealed record UpdateBusinessPartyRequest(
    string Name,
    bool IsActive,
    string? LegalName = null,
    string? TaxId = null,
    string? Phone = null,
    string? Email = null,
    string? Website = null,
    string? AddressLine1 = null,
    string? AddressLine2 = null,
    string? Ward = null,
    string? District = null,
    string? City = null,
    string? Province = null,
    string? CountryCode = null,
    string? PostalCode = null,
    string? DefaultCurrencyCode = null,
    int? PaymentTermDays = null,
    decimal? CreditLimit = null,
    string? CreditLimitCurrencyCode = null,
    string? Notes = null,
    string? PartyKind = null,
    string? ShortName = null,
    string? LegalType = null,
    string? GroupCode = null,
    string? ExternalCode = null,
    string? IndustryCode = null,
    string? InvoiceEmail = null,
    bool? VatRegistered = null,
    Guid? AssignedUserId = null,
    Guid? ParentPartyId = null,
    string? CreditControlMode = null);
public sealed record AssignPartyRoleRequest(string RoleCode);
public sealed record BlockBusinessPartyRequest(string Reason);
public sealed record UnblockBusinessPartyRequest(string? Reason = null);
public sealed record UpsertPartyBankAccountRequest(
    string BankName,
    string? BankBranch,
    string AccountNumber,
    string? AccountName,
    string CurrencyCode,
    bool IsDefault,
    bool IsActive,
    string? Note,
    string? BankCode = null,
    string? SwiftBic = null);
public sealed record UpsertPartyContactRequest(
    string FullName,
    string? Title,
    string? Phone,
    string? Email,
    bool IsPrimary,
    bool IsActive,
    string? Note,
    string? FunctionCode = null);
public sealed record UpsertCurrencyRequest(string Code, string Name, int DecimalPlaces, bool IsActive);
