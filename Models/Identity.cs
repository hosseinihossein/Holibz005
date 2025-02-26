using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Holibz005.Models;

public class Identity_DbContext : IdentityDbContext<Identity_UserModel, Identity_RoleModel, string>
{
    public Identity_DbContext(DbContextOptions<Identity_DbContext> options) : base(options) { }
}

public class Identity_UserModel : IdentityUser
{
    public string UserGuid { get; set; } = string.Empty;
    public string PasswordLiteral { get; set; } = string.Empty;
    public string? EmailValidationCode { get; set; } = null;
    //public bool DisplayEmail { get; set; } = false;
    public DateTime? EmailValidationDate { get; set; } = null;
    //public bool DisplayEmailPublicly { get; set; } = false;
    public int Version { get; set; } = 0;
    public bool Active { get; set; } = true;
    public string Description { get; set; } = "Description..";
    public bool HasSpecificShowcase { get; set; } = false;
    public string? SpecificShowcaseBy { get; set; } = null;
    public string? SpecificShowcaseLink { get; set; } = null;
    public bool HasSpecificProfileImage { get; set; } = false;


    //public List<string> Notifications { get; set; } = new();
    //public virtual List<string> LoggedInDevices { get; set; } = new();
    //public int NumberOfLogins { get; set; } = 0;
    //[Unicode]
}

public class Identity_RoleModel : IdentityRole
{
    public Identity_RoleModel() : base() { }
    public Identity_RoleModel(string roleName) : base(roleName) { }
    public string Description { get; set; } = string.Empty;
}

public class Identity_LoginModel
{
    public string? ReturnUrl { get; set; } = string.Empty;
    public string UsernameOrEmail { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IsPersistent { get; set; } = false;

    [FromForm(Name = "cf-turnstile-response")]
    public string CfTurnstileResponse { get; set; } = string.Empty;
}

public class Identity_SignupModel
{
    [StringLength(50, MinimumLength = 8)]
    public string Username { get; set; } = string.Empty;

    [StringLength(100, MinimumLength = 8)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [StringLength(50, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    /*[Compare(nameof(Password))]
    public string RepeatPassword { get; set; } = string.Empty;*/

    [FromForm(Name = "cf-turnstile-response")]
    public string CfTurnstileResponse { get; set; } = string.Empty;
}

public class Identity_ProfileModel
{
    public Identity_UserModel? MyUser { get; set; }
    //public List<Library_ItemModel> ItemModels { get; set; } = new();
    public int NumberOfProjects { get; set; } = 0;
    public int NumberOfLikes { get; set; } = 0;
}

//********************* administration ********************
public class Identity_UserAndRolesModel(Identity_UserModel user, List<string> roles)
{
    public Identity_UserModel User { get; set; } = user;
    public List<string> Roles { get; set; } = roles;
}

public class Identity_RolesListModel(string role, string roleDescription, int numberOfUsers)
{
    public string Role { get; set; } = role;
    public string RoleDescription { get; set; } = roleDescription;
    public int NumberOfMembers { get; set; } = numberOfUsers;
}

public class Identity_AddNewUserModel
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    [Compare(nameof(Password))]
    public string RepeatPassword { get; set; } = string.Empty;
}

public class Identity_AddNewRoleModel
{
    public string RoleName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class Identity_EditRoleModel
{
    public string RoleName { get; set; } = string.Empty;
    public List<Identity_UserModel> Members { get; set; } = new();
    public List<Identity_UserModel> NotMembers { get; set; } = new();
}

//**************************** Backup ************************
/*public class Identity_BackupModel
{
    public string UserGuid { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; } = false;
    public string PasswordLiteral { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
    public int ProfileImageVersion { get; set; } = 0;
    public List<string> Roles { get; set; } = new();
}*/


//**************************** token provider ******************************
public class Identity_EmailTokenProvider : AuthenticatorTokenProvider<Identity_UserModel>
{
    public override Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<Identity_UserModel> userManager, Identity_UserModel user)
    {
        return base.CanGenerateTwoFactorTokenAsync(userManager, user);
    }
    public override async Task<string> GenerateAsync(string purpose, UserManager<Identity_UserModel> userManager, Identity_UserModel user)
    {
        //string token = string.Empty;
        string code = new Random().Next(1000, 99999).ToString();
        user.EmailValidationCode = code;
        user.EmailValidationDate = DateTime.Now;
        IdentityResult result = await userManager.UpdateAsync(user);
        string token = result.Succeeded ? code : string.Empty;
        return token;
    }
    public override async Task<bool> ValidateAsync(string purpose, string token, UserManager<Identity_UserModel> userManager, Identity_UserModel user)
    {
        bool isValid = false;
        if (token == user.EmailValidationCode)
        {
            isValid = true;
            user.EmailValidationCode = null;
            user.EmailValidationDate = null;
            await userManager.UpdateAsync(user);
        }
        return isValid;
    }
}