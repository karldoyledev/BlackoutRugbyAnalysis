using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace BlackoutRugbyDashboard.Services;

/// <summary>
/// Protects the Member Key at rest (D3): ASP.NET Core Data Protection under one
/// dashboard purpose. The plaintext key never touches the database; an
/// undecryptable blob (rotated machine keys) reads back as null, which surfaces
/// as the decision-2 broken-credentials runtime condition — never a third state.
/// </summary>
public interface IMemberKeyProtector
{
    string Protect(string plaintext);

    string? Unprotect(string? ciphertext);
}

public sealed class MemberKeyProtector(IDataProtectionProvider provider) : IMemberKeyProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("BlackoutRugbyDashboard.MemberKey.v1");

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string? Unprotect(string? ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(ciphertext);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}