namespace Management.Application.Authorization;

public static class Permissions
{
    public const string ClaimType = "permission";
    public const string UsersView = "Users.View";
    public const string UsersCreate = "Users.Create";
    public const string UsersUpdate = "Users.Update";
    public const string UsersDelete = "Users.Delete";
    public const string RolesView = "Roles.View";
    public const string RolesCreate = "Roles.Create";
    public const string RolesUpdate = "Roles.Update";
    public const string RolesDelete = "Roles.Delete";
    public const string PermissionsView = "Permissions.View";
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[]
    {
        UsersView, UsersCreate, UsersUpdate, UsersDelete,
        RolesView, RolesCreate, RolesUpdate, RolesDelete, PermissionsView
    });
}
