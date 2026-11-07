namespace Management.Application.DTOs;

public sealed record AuthenticationResponse(string AccessToken, string TokenType, int ExpiresIn,
    DateTime RefreshTokenExpiresAt, string? RefreshToken = null);
public sealed record TokenPair(string AccessToken, int ExpiresIn, string RefreshToken, DateTime RefreshTokenExpiresAt);
