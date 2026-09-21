using LCMS.Domain.Entities;

namespace LCMS.Application.Abstractions;

public interface IPasswordHasher
{
    string HashPassword(User user, string password);
    bool VerifyPassword(User user, string password);
}
