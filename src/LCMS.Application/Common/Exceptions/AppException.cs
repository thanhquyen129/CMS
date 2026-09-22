namespace LCMS.Application.Common.Exceptions;

/// <summary>Base application exception — Message is always safe Vietnamese for client UX.</summary>
public abstract class AppException : Exception
{
    public int StatusCode { get; }
    public string ErrorCode { get; }

    protected AppException(string vietnameseMessage, int statusCode, string errorCode)
        : base(vietnameseMessage)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}

public sealed class ValidationAppException : AppException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationAppException(IReadOnlyDictionary<string, string[]> errors)
        : base("Dữ liệu không hợp lệ.", StatusCodes.BadRequest, "validation_failed")
    {
        Errors = errors;
    }
}

public sealed class NotFoundAppException : AppException
{
    public NotFoundAppException(string vietnameseMessage = "Không tìm thấy dữ liệu.")
        : base(vietnameseMessage, StatusCodes.NotFound, "not_found")
    {
    }
}

public sealed class ConflictAppException : AppException
{
    public ConflictAppException(string vietnameseMessage = "Dữ liệu đã bị thay đổi bởi người khác. Vui lòng tải lại và thử lại.")
        : base(vietnameseMessage, StatusCodes.Conflict, "concurrency_conflict")
    {
    }
}

/// <summary>Financial close period/scope lock blocked a money mutation (H-009 / close gates).</summary>
public sealed class PeriodLockedAppException : AppException
{
    public PeriodLockedAppException(string vietnameseMessage)
        : base(vietnameseMessage, StatusCodes.Conflict, "period_locked")
    {
    }
}

public sealed class ForbiddenAppException : AppException
{
    public ForbiddenAppException(string vietnameseMessage = "Bạn không có quyền thực hiện thao tác này.")
        : base(vietnameseMessage, StatusCodes.Forbidden, "forbidden")
    {
    }
}

public sealed class TenantRequiredAppException : AppException
{
    public TenantRequiredAppException()
        : base("Thiếu ngữ cảnh thuê bao. Vui lòng đăng nhập lại.", StatusCodes.Unauthorized, "tenant_required")
    {
    }
}

internal static class StatusCodes
{
    public const int BadRequest = 400;
    public const int Unauthorized = 401;
    public const int Forbidden = 403;
    public const int NotFound = 404;
    public const int Conflict = 409;
}
