// Copyright (c) Microsoft. All rights reserved.

using Microsoft.AspNetCore.Authorization;

namespace ProjectVico.Frontend.API.Auth;

/// <summary>
/// Used to require the chat to be owned by the authenticated user.
/// </summary>
public class ChatParticipantRequirement : IAuthorizationRequirement
{
}
