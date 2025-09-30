// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Security.Cryptography;

namespace Microsoft.Greenlight.Shared.Services.Security;

/// <summary>
/// Implements secret hashing with per-secret salt using HMACSHA256.
/// </summary>
public sealed class SecretHashingService : ISecretHashingService
{
    public (string SaltBase64, string HashBase64) Hash(string plaintext)
    {
        var salt = RandomNumberGenerator.GetBytes(32);
        using var hmac = new HMACSHA256(salt);
        var data = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var hash = hmac.ComputeHash(data);
        return (Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public bool Verify(string plaintext, string saltBase64, string hashBase64)
    {
        if (string.IsNullOrEmpty(plaintext) || string.IsNullOrEmpty(saltBase64) || string.IsNullOrEmpty(hashBase64))
        {
            return false;
        }
        var salt = Convert.FromBase64String(saltBase64);
        using var hmac = new HMACSHA256(salt);
        var data = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var computed = hmac.ComputeHash(data);
        var expected = Convert.FromBase64String(hashBase64);
        return CryptographicOperations.FixedTimeEquals(computed, expected);
    }
}

