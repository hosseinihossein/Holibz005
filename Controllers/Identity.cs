using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Holibz005.Models;
using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Primitives;

namespace Holibz005.Controllers;

[AutoValidateAntiforgeryToken]
public class IdentityController : Controller
{
    readonly SignInManager<Identity_UserModel> signInManager;
    readonly UserManager<Identity_UserModel> userManager;
    readonly RoleManager<Identity_RoleModel> roleManager;
    readonly IWebHostEnvironment env;
    readonly char ds = Path.DirectorySeparatorChar;
    readonly IEmailSender emailSender;
    //readonly WebComponents_DbContext webComponentsDb;

    public IdentityController(SignInManager<Identity_UserModel> signInManager, UserManager<Identity_UserModel> userManager,
    IWebHostEnvironment env, IEmailSender _emailSender, RoleManager<Identity_RoleModel> roleManager/*,
    WebComponents_DbContext webComponentsDb*/)
    {
        this.signInManager = signInManager;
        this.userManager = userManager;
        this.env = env;
        emailSender = _emailSender;
        this.roleManager = roleManager;
        //this.webComponentsDb = webComponentsDb;
    }

    public IActionResult Login(string returnUrl = "/")
    {
        if (User.Identity is null || !User.Identity.IsAuthenticated)
        {
            Identity_LoginModel loginModel = new()
            {
                ReturnUrl = returnUrl
            };
            return View(loginModel);
        }
        else
        {
            return RedirectToAction(nameof(Dashboard));
        }
    }

    [HttpPost]
    public async Task<IActionResult> SubmitLogin(Identity_LoginModel loginModel)
    {
        if (ModelState.IsValid)
        {
            var formData = new { secret = "0x4AAAAAAAkeZ_VQzHOlwqGq3-wl_DJ_HEw", response = loginModel.CfTurnstileResponse };
            string url = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
            var client = new HttpClient();
            var response = await client.PostAsJsonAsync(url, formData);
            string responseContentString = await response.Content.ReadAsStringAsync();
            dynamic? responseContent = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(responseContentString);
            if (!response.IsSuccessStatusCode || (responseContent?.success ?? false) == false)
            {
                ModelState.AddModelError("Turnstile", "Cloudn't pass the CAPTCHA!");
                return View(nameof(Login));
            }

            if (User.Identity?.IsAuthenticated ?? false)
            {
                await signInManager.SignOutAsync();
            }

            Identity_UserModel? user;
            if (loginModel.UsernameOrEmail.Contains('@'))
            {
                user = await userManager.FindByEmailAsync(loginModel.UsernameOrEmail);
            }
            else
            {
                user = await userManager.FindByNameAsync(loginModel.UsernameOrEmail);
            }

            if (user is not null)
            {
                if (!user.Active)
                {
                    ModelState.AddModelError("", "Your Account is inactive! Contact to admin.");
                }
                else
                {
                    Microsoft.AspNetCore.Identity.SignInResult result =
                    await signInManager.PasswordSignInAsync(user, loginModel.Password, loginModel.IsPersistent, false);

                    if (result.Succeeded)
                    {
                        return Redirect(loginModel.ReturnUrl ?? "/");
                    }
                }
            }
            else
            {
                ModelState.AddModelError("", "Invalid Username or Password");
            }
        }
        return View(nameof(Login));
    }

    [Authorize]
    public async Task<IActionResult> Logout(string? returnUrl)
    {
        await signInManager.SignOutAsync();
        return Redirect(returnUrl ?? "/");
    }

    [Authorize]
    public IActionResult AccessDenied()
    {
        return View();
    }

    public IActionResult Signup()
    {
        if (User.Identity is null || !User.Identity.IsAuthenticated)
        {
            Identity_SignupModel signupModel = new();
            return View(signupModel);
        }
        else
        {
            return RedirectToAction(nameof(Dashboard));
        }
    }

    [HttpPost]
    public async Task<IActionResult> SubmitSignup(Identity_SignupModel signupModel)
    {
        if (ModelState.IsValid)
        {
            var formData = new { secret = "0x4AAAAAAAkeZ_VQzHOlwqGq3-wl_DJ_HEw", response = signupModel.CfTurnstileResponse };
            string url = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
            var client = new HttpClient();
            var response = await client.PostAsJsonAsync(url, formData);
            string responseContentString = await response.Content.ReadAsStringAsync();
            dynamic? responseContent = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(responseContentString);
            if (!response.IsSuccessStatusCode || (responseContent?.success ?? false) == false)
            {
                ModelState.AddModelError("Turnstile", "Cloudn't pass the CAPTCHA!");
                return View(nameof(Signup));
            }

            if (User.Identity?.IsAuthenticated ?? false)
            {
                await signInManager.SignOutAsync();
            }

            Identity_UserModel user = new Identity_UserModel()
            {
                UserName = signupModel.Username,
                Email = signupModel.Email,
                EmailConfirmed = false,
                UserGuid = Guid.NewGuid().ToString().Replace("-", ""),
                PasswordLiteral = signupModel.Password
            };
            IdentityResult result = await userManager.CreateAsync(user, signupModel.Password);
            if (result.Succeeded)
            {
                await userManager.SetTwoFactorEnabledAsync(user, true);
                //***** Create Email DB *****
                string validationCode = await userManager.GenerateEmailConfirmationTokenAsync(user);

                //***** Sending Email *****
                string emailMessage = $"<h4>Hi dear {signupModel.Username}</h4>" +
                $"<h4>Your Validation Code: {validationCode}</h4>" +
                "<p>The validation code expires in 30 minutes.</p>";
                /*await*/
                _ = emailSender.SendEmailAsync(signupModel.Username, signupModel.Email,
                "Email Validation", emailMessage);

                //**************** sign in the user **********************
                await signInManager.PasswordSignInAsync(user, signupModel.Password, false, false);

                object o = "<h2>You've just Signed Up successfully</h2>" +
                "<p>Please go to <a href=\"/Identity/Dashboard\">Dashboard</a> to activate your account</p>";
                ViewBag.ResultState = "success";
                return View("Result", o);
            }
            foreach (IdentityError error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
        }
        return View(nameof(Signup));
    }

    [Authorize]
    public async Task<IActionResult> SendEmailValidationCode()
    {
        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user is not null)
        {
            if (user.EmailValidationCode is null
            || (DateTime.Now - (user.EmailValidationDate ?? DateTime.MinValue)).TotalMinutes > 30.00)
            {
                /*user.EmailValidationCode = */
                await userManager.GenerateEmailConfirmationTokenAsync(user);
                //user.EmailValidationDate = DateTime.Now;
                //await userManager.UpdateAsync(user);
            }
            string validationCode = user.EmailValidationCode!;

            string emailMessage = $"<h4>Hi dear {user.UserName}</h4>" +
                    $"<h4>Your Validation Code: {validationCode}</h4>" +
                    "<p>The validation code expires in 30 minutes.</p>";

            await emailSender.SendEmailAsync(user.UserName!, user!.Email!, "Email Validation", emailMessage);

            await signInManager.SignOutAsync();
            object o1 = "<h2>A Validation code is sent to your registered Email.</h2>" +
            "<p>Please check your email and go to <a href=\"/Identity/Dashboard\">Dashboard</a> to activate your account</p>";
            ViewBag.ResultState = "success";
            return View("Result", o1);
        }

        object notFoundMessage = $"<p>User Not found!.</p>";
        ViewBag.ResultState = "danger";
        return View("Result", notFoundMessage);
    }

    [Authorize]
    public async Task<IActionResult> ConfirmEmail(string evc,
    [FromQuery(Name = "cf-turnstile-response")] string CfTurnstileResponse)
    {
        var formData = new { secret = "0x4AAAAAAAkeZ_VQzHOlwqGq3-wl_DJ_HEw", response = CfTurnstileResponse };
        string url = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
        var client = new HttpClient();
        var response = await client.PostAsJsonAsync(url, formData);
        string responseContentString = await response.Content.ReadAsStringAsync();
        dynamic? responseContent = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(responseContentString);
        if (!response.IsSuccessStatusCode || (responseContent?.success ?? false) == false)
        {
            ModelState.AddModelError("Turnstile", "Cloudn't pass the CAPTCHA!");
            object incorrectCaptcha = "<h1>!</h1>";
            ViewBag.ResultState = "danger";
            return View("Result", incorrectCaptcha);
        }

        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user is not null)
        {
            if (user.EmailConfirmed)
            {
                object o1 = "Your Email has already confirmed. Don't need to confirm anymore!";
                ViewBag.ResultState = "info";
                return View("Result", o1);
            }
            if (DateTime.Now - (user.EmailValidationDate ?? DateTime.MinValue) <= TimeSpan.FromMinutes(30.0))
            {
                IdentityResult result = await userManager.ConfirmEmailAsync(user, evc);
                if (result.Succeeded)
                {
                    //user.EmailValidationCode = null;
                    //user.EmailValidationDate = null;
                    object successMessage = "<h1>Your Email hass Successfully Confirmed And Your Account is Active.</h1>";
                    ViewBag.ResultState = "success";
                    return View("Result", successMessage);
                }
            }
            object incorrectVal = "<h1>Your Validation Code Is Incorrect or Expired!</h1>";
            ViewBag.ResultState = "danger";
            return View("Result", incorrectVal);
        }
        object userNotFoundMessage = "<h1>User Not found!!</h1>";
        ViewBag.ResultState = "danger";
        return View("Result", userNotFoundMessage);
    }

    public IActionResult ForgetPassword()
    {
        return View();
    }

    public async Task<IActionResult> SubmitForgetPassword(string email,
    [FromQuery(Name = "cf-turnstile-response")] string CfTurnstileResponse)
    {
        var formData = new { secret = "0x4AAAAAAAkeZ_VQzHOlwqGq3-wl_DJ_HEw", response = CfTurnstileResponse };
        string url = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
        var client = new HttpClient();
        var response = await client.PostAsJsonAsync(url, formData);
        string responseContentString = await response.Content.ReadAsStringAsync();
        dynamic? responseContent = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(responseContentString);
        if (!response.IsSuccessStatusCode || (responseContent?.success ?? false) == false)
        {
            ModelState.AddModelError("Turnstile", "Cloudn't pass the CAPTCHA!");
            return View(nameof(ForgetPassword));
        }

        Identity_UserModel? user = await userManager.FindByEmailAsync(email);
        if (user is not null && user.Email is not null)
        {
            //***** Generate Email verification code *****
            string validationCode = await userManager.GeneratePasswordResetTokenAsync(user);//.GenerateEmailConfirmationTokenAsync(user);

            //***** Sending Email *****
            string emailMessage = $"<h4>Hi dear {user.UserName}</h4>" +
            $"<h4>Your Validation Code: {validationCode}</h4>" +
            "<p>The validation code expires in 10 minutes.</p>";
            await emailSender.SendEmailAsync(user.UserName ?? "Dear Client", user.Email,
            "Reset Password", emailMessage);
        }
        return View("ChangeForgottenPassword", email);
    }

    public async Task<IActionResult> ResetForgottenPassword(string email, string verificationCode,
    string newPassword, string repeatPassword, [FromQuery(Name = "cf-turnstile-response")] string CfTurnstileResponse)
    {
        var formData = new { secret = "0x4AAAAAAAkeZ_VQzHOlwqGq3-wl_DJ_HEw", response = CfTurnstileResponse };
        string url = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
        var client = new HttpClient();
        var response = await client.PostAsJsonAsync(url, formData);
        string responseContentString = await response.Content.ReadAsStringAsync();
        dynamic? responseContent = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(responseContentString);
        if (!response.IsSuccessStatusCode || (responseContent?.success ?? false) == false)
        {
            ModelState.AddModelError("Turnstile", "Cloudn't pass the CAPTCHA!");
            return View("ChangeForgottenPassword", email);
        }

        Identity_UserModel? user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            ModelState.AddModelError("", "Invalid!");
            return View("ChangeForgottenPassword", email);
        }

        if (newPassword != repeatPassword)
        {
            ModelState.AddModelError("", $"New Password=({newPassword}) and Repeat Password=({repeatPassword}) doesn't match!");
            return View("ChangeForgottenPassword", email);
        }

        IdentityResult result = await userManager.ResetPasswordAsync(user, verificationCode, newPassword);
        if (result.Succeeded)
        {
            user.PasswordLiteral = newPassword;
            await userManager.UpdateAsync(user);

            object o = $"The Password for Email \'{email}\' gets reset successfully!";
            ViewBag.ResultState = "success";
            return View("Result", o);
        }

        foreach (IdentityError error in result.Errors)
        {
            ModelState.AddModelError("", error.Description);
        }
        return View("ChangeForgottenPassword", email);
    }

    [Authorize]
    public /*async Task<*/IActionResult Dashboard()
    {
        /*Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user is null)
        {
            object o = $"Couldn't find the user with name: {User.Identity?.Name}";
            ViewBag.ResultState = "danger";
            return View("Result", o);
        }*/
        return View(/*user*/);
    }

    public async Task<IActionResult> Profile(string username)
    {
        Identity_UserModel? user = await userManager.FindByNameAsync(username);
        if (user is null)
        {
            object o = $"Couldn't find the username: {username}";
            ViewBag.ResultState = "danger";
            return View("Result", o);
        }

        Identity_ProfileModel profileModel = new()
        {
            MyUser = user,
            NumberOfProjects = await ,
            NumberOfLikes = await
        };

        return View(profileModel);
    }

    [Authorize]
    public async Task<IActionResult> Settings()
    {
        string username = User.Identity!.Name!;
        Identity_UserModel? user = await userManager.FindByNameAsync(username);
        if (user is null)
        {
            object o = $"Couldn't find the user with name: {username}";
            ViewBag.ResultState = "danger";
            return View("Result", o);
        }

        return View(user);
    }

    [Authorize]
    public async Task<IActionResult> SubmitUsername([StringLength(50)] string username)
    {
        if (!ModelState.IsValid)
        {
            object o1 = "Username must be less than 50 characters!";
            ViewBag.ResultState = "danger";
            return View("Result", o1);
        }

        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user is null)
        {
            object o1 = "user not found!";
            ViewBag.ResultState = "danger";
            return View("Result", o1);
        }
        if (!user.EmailConfirmed)
        {
            ViewBag.ResultState = "danger";
            object o2 = "Please Validate your Email first!";
            return View("Result", o2);
        }

        user.UserName = username;
        IdentityResult result = await userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            await signInManager.SignOutAsync();
            return RedirectToAction(nameof(Dashboard));
        }

        foreach (IdentityError error in result.Errors)
        {
            ModelState.AddModelError("", error.Description);
        }
        object o = "There're some problem in changing username!";
        ViewBag.ResultState = "danger";
        return View("Result", o);
    }

    [Authorize]
    public async Task<IActionResult> SubmitDescription([StringLength(500)] string description)
    {
        if (!ModelState.IsValid)
        {
            object o1 = "Description must be less than 500 characters!";
            ViewBag.ResultState = "danger";
            return View("Result", o1);
        }

        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user is null)
        {
            object o1 = "user not found!";
            ViewBag.ResultState = "danger";
            return View("Result", o1);
        }
        if (!user.EmailConfirmed)
        {
            ViewBag.ResultState = "danger";
            object o2 = "Please Validate your Email first!";
            return View("Result", o2);
        }

        user.Description = description;
        IdentityResult result = await userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            await signInManager.SignOutAsync();
            return RedirectToAction(nameof(Dashboard));
        }

        foreach (IdentityError error in result.Errors)
        {
            ModelState.AddModelError("", error.Description);
        }
        object o = "There're some problem in changing description!";
        ViewBag.ResultState = "danger";
        return View("Result", o);
    }

    [Authorize]
    public async Task<IActionResult> ChangePassword()
    {
        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user is null)
        {
            object o1 = "user not found!";
            ViewBag.ResultState = "danger";
            return View("Result", o1);
        }
        if (!user.EmailConfirmed)
        {
            ViewBag.ResultState = "danger";
            object o2 = "Please Validate your Email first!";
            return View("Result", o2);
        }
        return View();
    }

    [Authorize]
    public async Task<IActionResult> SubmitNewPassword(string currentPassword, [StringLength(50)] string newPassword,
    string repeatNewPassword)
    {
        if (!ModelState.IsValid)
        {
            object o1 = "Password must be less than 50 characters!";
            ViewBag.ResultState = "danger";
            return View("Result", o1);
        }

        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user is null)
        {
            object o1 = "user not found!";
            ViewBag.ResultState = "danger";
            return View("Result", o1);
        }
        if (!user.EmailConfirmed)
        {
            ViewBag.ResultState = "danger";
            object o2 = "Please Validate your Email first!";
            return View("Result", o2);
        }

        if (newPassword != repeatNewPassword)
        {
            ModelState.AddModelError("", $"New Password=({newPassword}) and Repeat New Password=({repeatNewPassword}) are not the same! repeat again.");
            object o1 = "Password didn't change!";
            ViewBag.ResultState = "danger";
            return View("Result", o1);
        }

        IdentityResult result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (result.Succeeded)
        {
            user.PasswordLiteral = newPassword;
            await userManager.UpdateAsync(user);
            await signInManager.SignOutAsync();
            return RedirectToAction(nameof(Dashboard));
        }

        foreach (IdentityError error in result.Errors)
        {
            ModelState.AddModelError("", error.Description);
        }
        object o = "There're some problem in changing password!";
        ViewBag.ResultState = "danger";
        return View("Result", o);
    }

    [Authorize]
    public async Task<IActionResult> SubmitNewEmail([StringLength(100)] string email)
    {
        if (!ModelState.IsValid)
        {
            object o1 = "Email must be less than 100 characters!";
            ViewBag.ResultState = "danger";
            return View("Result", o1);
        }

        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user is null)
        {
            object o1 = "user not found!";
            ViewBag.ResultState = "danger";
            return View("Result", o1);
        }

        user.Email = email;
        user.EmailConfirmed = false;
        user.EmailValidationCode = null;
        user.EmailValidationDate = null;
        IdentityResult result = await userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            return RedirectToAction(nameof(SendEmailValidationCode));
        }

        foreach (IdentityError error in result.Errors)
        {
            ModelState.AddModelError("", error.Description);
        }
        object o = "There's a problem in changing email!";
        ViewBag.ResultState = "danger";
        return View("Result", o);
    }

    [Authorize]
    [HttpPost]
    [RequestSizeLimit(100 * 1024)]
    public async Task<IActionResult> SubmitProfileImage(IFormFile imgFile)
    {
        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user is null)
        {
            object o1 = "user not found!";
            ViewBag.ResultState = "danger";
            return View("Result", o1);
        }
        if (!user.EmailConfirmed)
        {
            ViewBag.ResultState = "danger";
            object o2 = "Please Validate your Email first!";
            return View("Result", o2);
        }

        string userImageDirectoryPath = env.WebRootPath + ds + "Images" + ds + "Users" + ds + user.UserGuid;
        if (!Directory.Exists(userImageDirectoryPath))
        {
            Directory.CreateDirectory(userImageDirectoryPath);
        }
        string userImagePath = userImageDirectoryPath + ds + "profileImage";
        using (FileStream fs = System.IO.File.Create(userImagePath))
        {
            await imgFile.CopyToAsync(fs);
        }

        user.Version++;
        user.HasSpecificProfileImage = true;
        IdentityResult result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (IdentityError error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            object o = "There's a problem in updating user identity! Please try again.";
            ViewBag.ResultState = "danger";
            return View("Result", o);
        }

        return RedirectToAction(nameof(Dashboard));
    }

    [Authorize]
    [HttpPost]
    [RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> SubmitShowcaseImage(IFormFile imgFile, string? by, string? link)
    {
        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user is null)
        {
            object o1 = "user not found!";
            ViewBag.ResultState = "danger";
            return View("Result", o1);
        }
        if (!user.EmailConfirmed)
        {
            ViewBag.ResultState = "danger";
            object o2 = "Please Validate your Email first!";
            return View("Result", o2);
        }

        string userImageDirectoryPath = env.WebRootPath + ds + "Images" + ds + "Users" + ds + user.UserGuid;
        if (!Directory.Exists(userImageDirectoryPath))
        {
            Directory.CreateDirectory(userImageDirectoryPath);
        }
        string userImagePath = userImageDirectoryPath + ds + "profileShowcase";
        using (FileStream fs = System.IO.File.Create(userImagePath))
        {
            await imgFile.CopyToAsync(fs);
        }

        user.Version++;
        user.HasSpecificShowcase = true;
        user.SpecificShowcaseBy = by;
        user.SpecificShowcaseLink = link;
        IdentityResult result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (IdentityError error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            object o = "There's a problem in updating user identity!";
            ViewBag.ResultState = "danger";
            return View("Result", o);
        }

        return RedirectToAction(nameof(Dashboard));
    }

    [Authorize]
    public async Task<IActionResult> RemoveProfileImage()
    {
        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user is null)
        {
            object o1 = "user not found!";
            ViewBag.ResultState = "danger";
            return View("Result", o1);
        }

        string userImageDirectoryPath = env.WebRootPath + ds + "Images" + ds + "Users" + ds + user.UserGuid;
        string userImagePath = userImageDirectoryPath + ds + "profileImage";
        if (System.IO.File.Exists(userImagePath))
        {
            System.IO.File.Delete(userImagePath);
        }

        user.Version++;
        user.HasSpecificProfileImage = false;
        IdentityResult result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (IdentityError error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            object o = "There's a problem in updating user identity!";
            ViewBag.ResultState = "danger";
            return View("Result", o);
        }

        return RedirectToAction(nameof(Dashboard));
    }

    [Authorize]
    public async Task<IActionResult> RemoveProfileShowcase()
    {
        Identity_UserModel? user = await userManager.FindByNameAsync(User.Identity!.Name!);
        if (user is null)
        {
            object o1 = "user not found!";
            ViewBag.ResultState = "danger";
            return View("Result", o1);
        }

        string userImageDirectoryPath = env.WebRootPath + ds + "Images" + ds + "Users" + ds + user.UserGuid;
        string userImagePath = userImageDirectoryPath + ds + "profileShowcase";
        if (System.IO.File.Exists(userImagePath))
        {
            System.IO.File.Delete(userImagePath);
        }

        user.Version++;
        user.HasSpecificShowcase = false;
        user.SpecificShowcaseBy = null;
        user.SpecificShowcaseLink = null;
        IdentityResult result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (IdentityError error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            object o = "There's a problem in updating user identity!";
            ViewBag.ResultState = "danger";
            return View("Result", o);
        }

        return RedirectToAction(nameof(Dashboard));
    }

    public async Task<IActionResult> UserImage(string? username)
    {
        if (string.IsNullOrEmpty(username))
        {
            string defaultClientImagePath1 = env.WebRootPath + ds + "Images" + ds + "Users" + ds + "defaultProfile.jpg";
            return PhysicalFile(defaultClientImagePath1, "Image/*");
        }
        Identity_UserModel? user = await userManager.FindByNameAsync(username);
        if (user is null || !user.HasSpecificProfileImage)
        {
            string defaultClientImagePath1 = env.WebRootPath + ds + "Images" + ds + "Users" + ds + "defaultProfile.jpg";
            return PhysicalFile(defaultClientImagePath1, "Image/*");
        }

        string userImagePath = env.WebRootPath + ds + "Images" + ds + "Users" + ds + user.UserGuid + ds + "profileImage";

        if (System.IO.File.Exists(userImagePath))
        {
            return PhysicalFile(userImagePath, "Image/*");
        }

        string defaultUserImagePath = env.WebRootPath + ds + "Images" + ds + "Users" + ds + "defaultProfile.jpg";
        return PhysicalFile(defaultUserImagePath, "Image/*");
    }

    public async Task<IActionResult> UserShowcase(string? username)
    {
        if (string.IsNullOrEmpty(username))
        {
            string defaultClientImagePath1 = env.WebRootPath + ds + "Images" + ds + "Users" + ds + "defaultShowcase.webp";
            return PhysicalFile(defaultClientImagePath1, "Image/*");
        }
        Identity_UserModel? user = await userManager.FindByNameAsync(username);
        if (user is null || !user.HasSpecificShowcase)
        {
            //return NotFound();
            string defaultClientImagePath1 = env.WebRootPath + ds + "Images" + ds + "Users" + ds + "defaultShowcase.webp";
            return PhysicalFile(defaultClientImagePath1, "Image/*");
        }

        string clientImagePath = env.WebRootPath + ds + "Images" + ds + "Users" + ds + user.UserGuid + ds + "profileShowcase";

        if (System.IO.File.Exists(clientImagePath))
        {
            return PhysicalFile(clientImagePath, "Image/*");
        }

        string defaultClientImagePath = env.WebRootPath + ds + "Images" + ds + "Users" + ds + "defaultShowcase.webp";
        return PhysicalFile(defaultClientImagePath, "Image/*");
    }

    //************************************ Users Administration ************************************
    [Authorize(Roles = "Identity_Admins")]
    public async Task<IActionResult> UsersList()
    {
        List<Identity_UserAndRolesModel> userAndRole_List = [];
        foreach (Identity_UserModel user in await userManager.Users.ToListAsync())
        {
            if (user is null) continue;
            Identity_UserAndRolesModel userListModel = new(user, [.. await userManager.GetRolesAsync(user)]);
            userAndRole_List.Add(userListModel);
        }
        return View(userAndRole_List);
    }

    /*[Authorize(Roles = "Identity_Admins")]
    public IActionResult AddUser()
    {
        return View(new Identity_AddNewUserModel());
    }

    [HttpPost]
    [Authorize(Roles = "Identity_Admins")]
    public async Task<IActionResult> SubmitNewUser(Identity_AddNewUserModel newUserModel)
    {
        if (ModelState.IsValid)
        {
            Identity_UserModel user = new()
            {
                UserName = newUserModel.Username,
                Email = newUserModel.Email,
                EmailConfirmed = true,
                UserGuid = Guid.NewGuid().ToString().Replace("-", ""),
                PasswordLiteral = newUserModel.Password
            };
            IdentityResult result = await userManager.CreateAsync(user, newUserModel.Password);
            if (result.Succeeded)
            {
                return RedirectToAction(nameof(UsersList));
            }

            foreach (IdentityError error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
        }
        return View(nameof(AddUser));
    }*/

    [Authorize(Roles = "Identity_Admins")]
    public async Task<IActionResult> EditUser(string userGuid)
    {
        Identity_UserModel? user = await userManager.Users.FirstOrDefaultAsync(u => u.UserGuid == userGuid);
        if (user is null)
        {
            return NotFound();
        }
        Identity_UserAndRolesModel userModel = new(user, [.. (await userManager.GetRolesAsync(user))]);
        return View(userModel);
    }

    [Authorize(Roles = "Identity_Admins")]
    public async Task<IActionResult> SubmitUsername(string userGuid, string username)
    {
        Identity_UserModel? user = await userManager.Users.FirstOrDefaultAsync(u => u.UserGuid == userGuid);
        if (user is null)
        {
            return NotFound();
        }
        user.UserName = username;
        IdentityResult result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (IdentityError error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            Identity_UserAndRolesModel userModel = new(user, [.. (await userManager.GetRolesAsync(user))]);
            return View(nameof(EditUser), userModel);
        }
        return RedirectToAction(nameof(EditUser), new { userGuid });
    }

    [Authorize(Roles = "Identity_Admins")]
    public async Task<IActionResult> SubmitPassword(string userGuid, string password)
    {
        Identity_UserModel? user = await userManager.Users.FirstOrDefaultAsync(u => u.UserGuid == userGuid);
        if (user is null)
        {
            return NotFound();
        }
        IdentityResult result = await userManager.ChangePasswordAsync(user, user.PasswordLiteral, password);

        if (result.Succeeded)
        {
            user.PasswordLiteral = password;
            result = await userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (IdentityError error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
                Identity_UserAndRolesModel userModel = new(user, [.. (await userManager.GetRolesAsync(user))]);
                return View(nameof(EditUser), userModel);
            }
        }
        else
        {
            foreach (IdentityError error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            Identity_UserAndRolesModel userModel = new(user, [.. (await userManager.GetRolesAsync(user))]);
            return View(nameof(EditUser), userModel);
        }
        return RedirectToAction(nameof(EditUser), new { userGuid });
    }

    [Authorize(Roles = "Identity_Admins")]
    public async Task<IActionResult> SubmitEmail(string userGuid, string email)
    {
        Identity_UserModel? user = await userManager.Users.FirstOrDefaultAsync(u => u.UserGuid == userGuid);
        if (user is null)
        {
            return NotFound();
        }
        user.Email = email;
        user.EmailConfirmed = true;
        IdentityResult result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (IdentityError error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            Identity_UserAndRolesModel userModel = new(user, [.. (await userManager.GetRolesAsync(user))]);
            return View(nameof(EditUser), userModel);
        }
        return RedirectToAction(nameof(EditUser), new { userGuid });
    }

    [Authorize(Roles = "Identity_Admins")]
    public async Task<IActionResult> SubmitActiveState(string userGuid, bool activeState)
    {
        Identity_UserModel? user = await userManager.Users.FirstOrDefaultAsync(u => u.UserGuid == userGuid);
        if (user is null)
        {
            return NotFound();
        }
        user.Active = activeState;
        IdentityResult result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (IdentityError error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            Identity_UserAndRolesModel userModel = new(user, [.. await userManager.GetRolesAsync(user)]);
            return View(nameof(EditUser), userModel);
        }

        await userManager.UpdateSecurityStampAsync(user);
        return RedirectToAction(nameof(EditUser), new { userGuid });
    }

    [Authorize(Roles = "Identity_Admins")]
    public async Task<IActionResult> DeleteUser(string userGuid)
    {
        Identity_UserModel? user = await userManager.Users.FirstOrDefaultAsync(u => u.UserGuid == userGuid);
        if (user is null)
        {
            return NotFound();
        }

        if (userGuid == "admin")
        {
            ModelState.AddModelError("", "Can NOT Delete \'admin\'!");
            Identity_UserAndRolesModel userModel = new(user, [.. (await userManager.GetRolesAsync(user))]);
            return View(nameof(EditUser), userModel);
        }

        IdentityResult result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            foreach (IdentityError error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            Identity_UserAndRolesModel userModel = new(user, [.. (await userManager.GetRolesAsync(user))]);
            return View(nameof(EditUser), userModel);
        }

        string userImagePath = env.WebRootPath + ds + "Images" + ds + "Users" + ds + user.UserGuid;
        if (System.IO.File.Exists(userImagePath))
        {
            System.IO.File.Delete(userImagePath);
        }

        return RedirectToAction(nameof(UsersList));
    }

    //************************************ Roles Administration ************************************
    [Authorize(Roles = "Identity_Admins")]
    public async Task<IActionResult> RolesList()
    {
        List<Identity_RolesListModel> roleListModel_List = [];
        foreach (var role in await roleManager.Roles.ToListAsync())
        {
            if (role.Name is null) continue;
            Identity_RolesListModel roleListModel =
            new(role.Name, role.Description, (await userManager.GetUsersInRoleAsync(role.Name)).Count);
            roleListModel_List.Add(roleListModel);
        }
        return View(roleListModel_List);
    }

    /*[Authorize(Roles = "Identity_Admins")]
    public IActionResult AddRole()
    {
        return View(new Identity_AddNewRoleModel());
    }

    [HttpPost]
    [Authorize(Roles = "Identity_Admins")]
    public async Task<IActionResult> SubmitNewRole(Identity_AddNewRoleModel newRoleModel)
    {
        if (ModelState.IsValid)
        {
            Identity_RoleModel? role = await roleManager.FindByNameAsync(newRoleModel.RoleName);
            if (role is null)
            {
                role = new(newRoleModel.RoleName) { Description = newRoleModel.Description };
                IdentityResult result = await roleManager.CreateAsync(role);
                if (result.Succeeded)
                {
                    return RedirectToAction(nameof(RolesList));
                }
                foreach (IdentityError error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
            else
            {
                ModelState.AddModelError("", $"Role named \'{newRoleModel.RoleName}\' already exists!");
            }
        }
        return View(nameof(AddRole));
    }*/

    [Authorize(Roles = "Identity_Admins")]
    public async Task<IActionResult> EditRole(string roleName)
    {
        Identity_RoleModel? role = await roleManager.FindByNameAsync(roleName);
        if (role is not null)
        {
            List<Identity_UserModel> members = (await userManager.GetUsersInRoleAsync(role.Name!)).ToList() ?? [];
            List<Identity_UserModel> notMembers = (await userManager.Users.ToListAsync()).ExceptBy(members.Select(m => m.UserGuid), u => u.UserGuid).ToList();
            Identity_EditRoleModel editRoleModel = new()
            {
                RoleName = role.Name!,
                Members = members,
                NotMembers = notMembers
            };

            return View(editRoleModel);
        }
        return NotFound();
    }

    [Authorize(Roles = "Identity_Admins")]
    public async Task<IActionResult> RemoveMemberRole(string userGuid, string roleName)
    {
        if (userGuid == "admin")
        {
            ViewBag.ResultState = "danger";
            return View("Result", "Can NOT remove admin from any role!");
        }
        Identity_RoleModel? role = await roleManager.FindByNameAsync(roleName);
        if (role is null)
        {
            return NotFound();
        }
        Identity_UserModel? user = await userManager.Users.FirstOrDefaultAsync(u => u.UserGuid == userGuid);
        if (user is null)
        {
            return NotFound();
        }

        IdentityResult result = await userManager.RemoveFromRoleAsync(user, roleName);
        if (result.Succeeded)
        {
            return RedirectToAction(nameof(EditRole), new { roleName = roleName });
        }
        string errorMessage = string.Empty;
        foreach (var error in result.Errors)
        {
            errorMessage += error.Description;
        }
        ViewBag.ResultState = "danger";
        return View("Result", errorMessage);
    }

    [Authorize(Roles = "Identity_Admins")]
    public async Task<IActionResult> AddMemberRole(string userGuid, string roleName)
    {
        Identity_RoleModel? role = await roleManager.FindByNameAsync(roleName);
        if (role is null)
        {
            return NotFound();
        }
        Identity_UserModel? user = await userManager.Users.FirstOrDefaultAsync(u => u.UserGuid == userGuid);
        if (user is null)
        {
            return NotFound();
        }

        IdentityResult result = await userManager.AddToRoleAsync(user, roleName);
        if (result.Succeeded)
        {
            return RedirectToAction(nameof(EditRole), new { roleName = roleName });
        }
        string errorMessage = string.Empty;
        foreach (var error in result.Errors)
        {
            errorMessage += error.Description;
        }
        ViewBag.ResultState = "danger";
        return View("Result", errorMessage);
    }

    [Authorize(Roles = "Identity_Admins")]
    public async Task<IActionResult> DeleteRole(string roleName)
    {
        if (roleName == "Identity_Admins")
        {
            ModelState.AddModelError("", "Can Not Delete Role \'Identity_Admins\'!");
            return View(nameof(EditRole), roleName);
        }

        Identity_RoleModel? role = await roleManager.FindByNameAsync(roleName);
        if (role is null)
        {
            return NotFound();
        }

        IdentityResult result = await roleManager.DeleteAsync(role);
        if (result.Succeeded)
        {
            return RedirectToAction(nameof(RolesList));
        }
        foreach (IdentityError error in result.Errors)
        {
            ModelState.AddModelError("", error.Description);
        }
        return View(nameof(EditRole), roleName);
    }

}