using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Common;
using Microsoft.Extensions.Options;

namespace LCMS.Application.Common;

public sealed class ConcurrencyOptions
{
    public const string SectionName = "Concurrency";

    /// <summary>When true, money mutations reject a missing If-Match. A present token is always checked.</summary>
    public bool RequireIfMatch { get; set; }
}

public interface IRowVersionGuard
{
    void EnsureCurrent(EntityBase entity, string? ifMatch);
}

public sealed class RowVersionGuard : IRowVersionGuard
{
    private readonly ConcurrencyOptions _options;

    public RowVersionGuard(IOptions<ConcurrencyOptions> options) => _options = options.Value;

    public void EnsureCurrent(EntityBase entity, string? ifMatch)
    {
        var token = ifMatch?.Trim().Trim('"');
        if (string.IsNullOrEmpty(token))
        {
            if (_options.RequireIfMatch)
            {
                throw new ConflictAppException(
                    "Thiếu phiên bản dữ liệu. Tải lại trang rồi thử lại.");
            }

            return;
        }

        byte[] presented;
        try
        {
            presented = Convert.FromBase64String(token);
        }
        catch (FormatException)
        {
            throw new ConflictAppException(
                "Phiên bản dữ liệu không hợp lệ. Tải lại trang rồi thử lại.");
        }

        if (!presented.AsSpan().SequenceEqual(entity.RowVersion))
        {
            throw new ConflictAppException(
                "Dữ liệu đã bị thay đổi bởi người khác. Vui lòng tải lại và thử lại.");
        }
    }
}
