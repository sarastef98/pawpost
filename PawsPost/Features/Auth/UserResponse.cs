namespace PawsPost.Features.Auth;

/// <summary>The caller's own public account data. Never contains password, hash or other users' data.</summary>
public record UserResponse(Guid Id, string Email);
