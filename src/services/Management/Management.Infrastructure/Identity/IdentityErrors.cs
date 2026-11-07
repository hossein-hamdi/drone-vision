using Management.Application.Common;
using Microsoft.AspNetCore.Identity;

namespace Management.Infrastructure.Identity;

internal static class IdentityErrors
{
    public static void EnsureSuccess(IdentityResult result)
    {
        if (result.Succeeded) return;
        var errors = result.Errors.ToArray();
        if (errors.Any(e => e.Code is "DuplicateUserName" or "DuplicateRoleName" or "ConcurrencyFailure"))
            throw ApiException.Conflict("The username or role already exists, or the record changed concurrently.");
        throw new ApiException(400, "Validation Failed", "One or more validation errors occurred.",
            errors.GroupBy(e => e.Code.StartsWith("Password", StringComparison.Ordinal) ? "password" :
                e.Code.Contains("UserName", StringComparison.Ordinal) ? "userName" : "request")
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
    }
}
