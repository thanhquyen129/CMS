with open('src/LCMS.Application/Common/Exceptions/AppException.cs', 'rb') as f:
    raw = f.read()

text = raw.decode('utf-8')
target = """public sealed class ValidationAppException : AppException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationAppException(IReadOnlyDictionary<string, string[]> errors)
        : base("Dữ liệu không hợp lệ.", StatusCodes.BadRequest, "validation_failed")
    {
        Errors = errors;
    }
}"""

replacement = """public sealed class ValidationAppException : AppException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationAppException(IReadOnlyDictionary<string, string[]> errors)
        : base("Dữ liệu không hợp lệ.", StatusCodes.BadRequest, "validation_failed")
    {
        Errors = errors;
    }

    public ValidationAppException(string vietnameseMessage)
        : base(vietnameseMessage, StatusCodes.BadRequest, "validation_failed")
    {
        Errors = new Dictionary<string, string[]> { { string.Empty, new[] { vietnameseMessage } } };
    }

    public ValidationAppException(string field, string error)
        : base(error, StatusCodes.BadRequest, "validation_failed")
    {
        Errors = new Dictionary<string, string[]> { { field, new[] { error } } };
    }
}"""

# Handle both CRLF and LF
if target not in text:
    target = target.replace('\n', '\r\n')
    replacement = replacement.replace('\n', '\r\n')

if target in text:
    text = text.replace(target, replacement)
    with open('src/LCMS.Application/Common/Exceptions/AppException.cs', 'wb') as f:
        f.write(text.encode('utf-8'))
    print("Successfully patched AppException.cs")
else:
    print("Target not found!")

