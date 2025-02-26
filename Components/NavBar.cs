using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Holibz005.Models;

namespace Holibz005.Components;

public class NavBarComponent : ViewComponent
{
    readonly UserManager<Identity_UserModel> userManager;
    readonly char ds = Path.DirectorySeparatorChar;
    readonly IWebHostEnvironment env;

    public NavBarComponent(UserManager<Identity_UserModel> _userManager, IWebHostEnvironment _env)
    {
        env = _env;
        userManager = _userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        Identity_UserModel? user;
        if (User.Identity is null || !User.Identity.IsAuthenticated)
        {
            user = null;
        }
        else
        {
            user = await userManager.FindByNameAsync(User.Identity.Name!);
        }

        return View(user);
    }
}