using Management.Application.DTOs;

namespace Management.Application.Abstractions.Authentication;

public interface IAuthenticationService
{
    Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<TokenPair> LoginAsync(LoginRequest request, bool browser, CancellationToken ct);
    Task<TokenPair> RefreshAsync(string token, bool browser, CancellationToken ct);
    Task LogoutAsync(string token, bool browser, CancellationToken ct);
}
