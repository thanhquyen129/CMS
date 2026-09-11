namespace LCMS.Domain.Common;

/// <summary>
/// RFC 9562 UUIDv7 generator for .NET 8 (Guid.CreateVersion7 is .NET 9+).
/// ADR-0001: PK = uuid / UUIDv7.
/// </summary>
public static class UuidV7
{
    public static Guid NewId()
    {
        Span<byte> bytes = stackalloc byte[16];
        var unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        bytes[0] = (byte)(unixMs >> 40);
        bytes[1] = (byte)(unixMs >> 32);
        bytes[2] = (byte)(unixMs >> 24);
        bytes[3] = (byte)(unixMs >> 16);
        bytes[4] = (byte)(unixMs >> 8);
        bytes[5] = (byte)unixMs;

        Random.Shared.NextBytes(bytes[6..]);

        // version 7
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x70);
        // RFC variant 10xxxxxx
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes, bigEndian: true);
    }
}
