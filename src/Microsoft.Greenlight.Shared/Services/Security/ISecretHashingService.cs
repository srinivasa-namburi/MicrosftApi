// Copyright (c) Microsoft Corporation. All rights reserved.
namespace Microsoft.Greenlight.Shared.Services.Security;

/// <summary>
/// Provides hashing and verification for secrets using per-secret salts.
/// </summary>
public interface ISecretHashingService
{
    (string SaltBase64, string HashBase64) Hash(string plaintext);
    bool Verify(string plaintext, string saltBase64, string hashBase64);
}

