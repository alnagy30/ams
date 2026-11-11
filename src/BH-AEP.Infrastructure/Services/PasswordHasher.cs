using BH_AEP.Application.Common;

namespace BH_AEP.Infrastructure.Services;

public class PasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));

    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
