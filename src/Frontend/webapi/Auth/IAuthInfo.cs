// Copyright (c) Microsoft. All rights reserved.

namespace ProjectVico.Frontend.API.Auth;

public interface IAuthInfo
{
    /// <summary>
    /// The authenticated user's unique ID.
    /// </summary>
    public string UserId { get; }

    /// <summary>
    /// The authenticated user's name.
    /// </summary>
    public string Name { get; }
}
