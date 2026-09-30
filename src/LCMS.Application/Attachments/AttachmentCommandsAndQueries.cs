using System.Security.Cryptography;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LCMS.Application.Attachments;

public sealed class AttachmentStorageOptions
{
    public const string SectionName = "Attachments";

    /// <summary>
    /// Root directory on Contabo VPS (/opt/cms/attachments in production; ./data/attachments in dev).
    /// </summary>
    public string StoragePath { get; set; } = "/opt/cms/attachments";

    public long MaxFileSizeBytes { get; set; } = 15 * 1024 * 1024;
}

public sealed record AttachmentDto(
    Guid Id,
    string ObjectType,
    Guid ObjectId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256Hash,
    string? Notes,
    Guid? UploadedBy,
    DateTimeOffset UploadedAt,
    string ContentUrl);

public sealed record AttachmentContentResult(
    byte[] Bytes,
    string ContentType,
    string FileName,
    string Sha256Hash);

public sealed record UploadAttachmentCommand(
    string ObjectType,
    Guid ObjectId,
    string FileName,
    string ContentType,
    byte[] Bytes,
    string? Notes = null) : IRequest<AttachmentDto>;

public sealed record ListAttachmentsQuery(
    string ObjectType,
    Guid ObjectId) : IRequest<IReadOnlyList<AttachmentDto>>;

public sealed record GetAttachmentContentQuery(Guid Id) : IRequest<AttachmentContentResult>;

public sealed record DeleteAttachmentCommand(Guid Id, string? Reason) : IRequest;

internal static class AttachmentAccessGuard
{
    public static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp",
        "image/heic",
        "application/pdf"
    };

    public static async Task EnsureAccessAsync(
        ILcmsDbContext db,
        IPermissionService permissions,
        string objectType,
        Guid objectId,
        bool isWrite,
        CancellationToken ct)
    {
        var normalized = (objectType ?? string.Empty).Trim().ToLowerInvariant();
        if (!AttachmentObjectTypes.IsValid(normalized))
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["objectType"] = ["Loại đối tượng đính kèm không hợp lệ (chỉ hỗ trợ financial_document, bill, cost, revenue, payment, collection)."]
            });
        }

        switch (normalized)
        {
            case AttachmentObjectTypes.Bill:
            {
                var exists = await db.Bills.AsNoTracking().AnyAsync(b => b.Id == objectId, ct);
                if (!exists)
                {
                    throw new NotFoundAppException("Không tìm thấy Bill.");
                }

                if (isWrite)
                {
                    var canUpdateBill = await permissions.HasPermissionAsync(PermissionCodes.BillUpdate, ct)
                        || await permissions.HasPermissionAsync(PermissionCodes.BillCreate, ct)
                        || await permissions.HasPermissionAsync(PermissionCodes.CostCreate, ct)
                        || await permissions.HasPermissionAsync(PermissionCodes.RevenueCreate, ct);
                    if (!canUpdateBill)
                    {
                        throw new ForbiddenAppException("Bạn không có quyền đính kèm chứng từ vào Bill này.");
                    }
                }
                else
                {
                    await permissions.EnsureAsync(
                        PermissionCodes.BillRead,
                        "Bạn không có quyền xem tài liệu đính kèm của Bill.",
                        ct);
                }

                break;
            }

            case AttachmentObjectTypes.Cost:
            {
                var exists = await db.Costs.AsNoTracking().AnyAsync(c => c.Id == objectId, ct);
                if (!exists)
                {
                    throw new NotFoundAppException("Không tìm thấy khoản chi phí.");
                }

                await permissions.EnsureAsync(
                    isWrite ? PermissionCodes.CostCreate : PermissionCodes.CostRead,
                    "Bạn không có quyền truy cập chứng từ đính kèm của khoản chi phí này.",
                    ct);
                break;
            }

            case AttachmentObjectTypes.Payment:
            {
                var exists = await db.Payments.AsNoTracking().AnyAsync(p => p.Id == objectId, ct);
                if (!exists)
                {
                    throw new NotFoundAppException("Không tìm thấy phiếu thanh toán.");
                }

                await permissions.EnsureAsync(
                    isWrite ? PermissionCodes.CostCreate : PermissionCodes.CostRead,
                    "Bạn không có quyền truy cập chứng từ đính kèm của phiếu thanh toán này.",
                    ct);
                break;
            }

            case AttachmentObjectTypes.Revenue:
            {
                var exists = await db.Revenues.AsNoTracking().AnyAsync(r => r.Id == objectId, ct);
                if (!exists)
                {
                    throw new NotFoundAppException("Không tìm thấy khoản doanh thu.");
                }

                await permissions.EnsureAsync(
                    isWrite ? PermissionCodes.RevenueCreate : PermissionCodes.RevenueRead,
                    "Bạn không có quyền truy cập chứng từ đính kèm của khoản doanh thu này.",
                    ct);
                break;
            }

            case AttachmentObjectTypes.Collection:
            {
                var exists = await db.Collections.AsNoTracking().AnyAsync(c => c.Id == objectId, ct);
                if (!exists)
                {
                    throw new NotFoundAppException("Không tìm thấy phiếu thu tiền.");
                }

                await permissions.EnsureAsync(
                    isWrite ? PermissionCodes.RevenueCreate : PermissionCodes.RevenueRead,
                    "Bạn không có quyền truy cập chứng từ đính kèm của phiếu thu tiền này.",
                    ct);
                break;
            }

            case AttachmentObjectTypes.FinancialDocument:
            {
                var doc = await db.FinancialDocuments.AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == objectId, ct)
                    ?? throw new NotFoundAppException("Không tìm thấy chứng từ tài chính.");

                if (string.Equals(doc.Direction, FinancialDocumentDirections.Receivable, StringComparison.OrdinalIgnoreCase))
                {
                    await permissions.EnsureAsync(
                        isWrite ? PermissionCodes.RevenueCreate : PermissionCodes.RevenueRead,
                        "Bạn không có quyền truy cập tệp đính kèm của chứng từ phải thu.",
                        ct);
                }
                else
                {
                    await permissions.EnsureAsync(
                        isWrite ? PermissionCodes.CostCreate : PermissionCodes.CostRead,
                        "Bạn không có quyền truy cập tệp đính kèm của chứng từ phải trả.",
                        ct);
                }

                break;
            }
        }
    }

    public static string ResolveRootPath(AttachmentStorageOptions options)
    {
        var configured = options.StoragePath?.Trim();
        if (string.IsNullOrWhiteSpace(configured))
        {
            return Path.Combine(AppContext.BaseDirectory, "data", "attachments");
        }

        if (OperatingSystem.IsWindows() && configured.StartsWith('/'))
        {
            // Map /opt/cms/attachments to a local directory when running on Windows dev/test
            return Path.Combine(Path.GetTempPath(), "lcms-attachments");
        }

        return Path.GetFullPath(configured);
    }
}

public sealed class UploadAttachmentCommandHandler
    : IRequestHandler<UploadAttachmentCommand, AttachmentDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;
    private readonly IPermissionService _permissions;
    private readonly IAuditWriter _audit;
    private readonly AttachmentStorageOptions _options;

    public UploadAttachmentCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext userContext,
        IPermissionService permissions,
        IAuditWriter audit,
        IOptions<AttachmentStorageOptions> options)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userContext = userContext;
        _permissions = permissions;
        _audit = audit;
        _options = options.Value;
    }

    public async Task<AttachmentDto> Handle(
        UploadAttachmentCommand request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var objectType = (request.ObjectType ?? string.Empty).Trim().ToLowerInvariant();
        await AttachmentAccessGuard.EnsureAccessAsync(
            _db,
            _permissions,
            objectType,
            request.ObjectId,
            isWrite: true,
            cancellationToken);

        if (request.Bytes is null || request.Bytes.Length == 0)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["file"] = ["Tệp đính kèm không được rỗng."]
            });
        }

        var maxBytes = _options.MaxFileSizeBytes > 0 ? _options.MaxFileSizeBytes : 15 * 1024 * 1024;
        if (request.Bytes.Length > maxBytes)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["file"] = [$"Dung lượng tệp vượt quá giới hạn cho phép ({maxBytes / (1024 * 1024)} MB)."]
            });
        }

        var contentType = (request.ContentType ?? string.Empty).Trim().ToLowerInvariant();
        if (contentType == "image/jpg")
        {
            contentType = "image/jpeg";
        }

        if (!AttachmentAccessGuard.AllowedContentTypes.Contains(contentType))
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["contentType"] = ["Định dạng tệp không được hỗ trợ (chỉ chấp nhận JPEG, PNG, WEBP, HEIC, PDF)."]
            });
        }

        var rawFileName = Path.GetFileName(request.FileName?.Trim() ?? "attachment");
        if (string.IsNullOrWhiteSpace(rawFileName))
        {
            rawFileName = "attachment";
        }

        var ext = Path.GetExtension(rawFileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = contentType switch
            {
                "application/pdf" => ".pdf",
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/heic" => ".heic",
                _ => ".jpg"
            };
            rawFileName += ext;
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var utc = DateTimeOffset.UtcNow;
        var attachment = new DocumentAttachment
        {
            TenantId = tenantId,
            ObjectType = objectType,
            ObjectId = request.ObjectId,
            FileName = rawFileName,
            ContentType = contentType,
            SizeBytes = request.Bytes.Length,
            Sha256Hash = Convert.ToHexString(SHA256.HashData(request.Bytes)).ToLowerInvariant(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            UploadedBy = _userContext.HasUser ? _userContext.UserId : null,
            UploadedAt = utc
        };

        var relativeDir = Path.Combine(
            tenantId.ToString("D"),
            utc.Year.ToString("D4"),
            utc.Month.ToString("D2"));
        var storedFileName = $"{attachment.Id:D}{ext}";
        var relativePath = Path.Combine(relativeDir, storedFileName).Replace('\\', '/');
        attachment.StorageRelativePath = relativePath;

        var rootDir = AttachmentAccessGuard.ResolveRootPath(_options);
        var fullDir = Path.Combine(rootDir, relativeDir);
        Directory.CreateDirectory(fullDir);
        var fullFilePath = Path.Combine(fullDir, storedFileName);
        await File.WriteAllBytesAsync(fullFilePath, request.Bytes, cancellationToken);

        _db.DocumentAttachments.Add(attachment);

        _audit.Append(
            AuditActions.AttachmentUpload,
            AuditObjectTypes.Attachment,
            attachment.Id,
            afterJson: AuditJson.Serialize(new
            {
                attachment.ObjectType,
                attachment.ObjectId,
                attachment.FileName,
                attachment.ContentType,
                attachment.SizeBytes,
                attachment.Sha256Hash
            }),
            reason: attachment.Notes);

        await _db.SaveChangesAsync(cancellationToken);

        return new AttachmentDto(
            attachment.Id,
            attachment.ObjectType,
            attachment.ObjectId,
            attachment.FileName,
            attachment.ContentType,
            attachment.SizeBytes,
            attachment.Sha256Hash,
            attachment.Notes,
            attachment.UploadedBy,
            attachment.UploadedAt,
            $"/api/attachments/{attachment.Id}/content");
    }
}

public sealed class ListAttachmentsQueryHandler
    : IRequestHandler<ListAttachmentsQuery, IReadOnlyList<AttachmentDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public ListAttachmentsQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<AttachmentDto>> Handle(
        ListAttachmentsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var objectType = (request.ObjectType ?? string.Empty).Trim().ToLowerInvariant();
        await AttachmentAccessGuard.EnsureAccessAsync(
            _db,
            _permissions,
            objectType,
            request.ObjectId,
            isWrite: false,
            cancellationToken);

        var rows = await _db.DocumentAttachments.AsNoTracking()
            .Where(a => a.ObjectType == objectType && a.ObjectId == request.ObjectId)
            .ToListAsync(cancellationToken);

        var items = rows
            .OrderByDescending(a => a.UploadedAt)
            .Select(a => new AttachmentDto(
                a.Id,
                a.ObjectType,
                a.ObjectId,
                a.FileName,
                a.ContentType,
                a.SizeBytes,
                a.Sha256Hash,
                a.Notes,
                a.UploadedBy,
                a.UploadedAt,
                $"/api/attachments/{a.Id}/content"))
            .ToList();

        return items;
    }
}

public sealed class GetAttachmentContentQueryHandler
    : IRequestHandler<GetAttachmentContentQuery, AttachmentContentResult>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly AttachmentStorageOptions _options;

    public GetAttachmentContentQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        IOptions<AttachmentStorageOptions> options)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _options = options.Value;
    }

    public async Task<AttachmentContentResult> Handle(
        GetAttachmentContentQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var attachment = await _db.DocumentAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy tệp đính kèm.");

        await AttachmentAccessGuard.EnsureAccessAsync(
            _db,
            _permissions,
            attachment.ObjectType,
            attachment.ObjectId,
            isWrite: false,
            cancellationToken);

        var rootDir = AttachmentAccessGuard.ResolveRootPath(_options);
        var fullPath = Path.Combine(
            rootDir,
            attachment.StorageRelativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(fullPath))
        {
            throw new NotFoundAppException("Không tìm thấy nội dung tệp đính kèm trên đĩa lưu trữ.");
        }

        var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
        return new AttachmentContentResult(
            bytes,
            attachment.ContentType,
            attachment.FileName,
            attachment.Sha256Hash);
    }
}

public sealed class DeleteAttachmentCommandHandler : IRequestHandler<DeleteAttachmentCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;
    private readonly IPermissionService _permissions;
    private readonly IAuditWriter _audit;

    public DeleteAttachmentCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext userContext,
        IPermissionService permissions,
        IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userContext = userContext;
        _permissions = permissions;
        _audit = audit;
    }

    public async Task Handle(DeleteAttachmentCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["reason"] = ["Lý do xóa tệp đính kèm là bắt buộc."]
            });
        }

        var attachment = await _db.DocumentAttachments
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy tệp đính kèm.");

        await AttachmentAccessGuard.EnsureAccessAsync(
            _db,
            _permissions,
            attachment.ObjectType,
            attachment.ObjectId,
            isWrite: true,
            cancellationToken);

        var actorId = _userContext.HasUser ? _userContext.UserId : null;
        attachment.DeleteReason = reason;
        attachment.SoftDelete(actorId);

        _audit.Append(
            AuditActions.AttachmentDelete,
            AuditObjectTypes.Attachment,
            attachment.Id,
            beforeJson: AuditJson.Serialize(new
            {
                attachment.ObjectType,
                attachment.ObjectId,
                attachment.FileName,
                attachment.Sha256Hash
            }),
            afterJson: AuditJson.Serialize(new { Deleted = true, DeleteReason = reason }),
            reason: reason);

        await _db.SaveChangesAsync(cancellationToken);
    }
}
