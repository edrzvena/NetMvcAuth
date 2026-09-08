using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetMvcAuth.Data;
using NetMvcAuth.Models;
using NetMvcAuth.ViewModels;

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace NetMvcAuth.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _db;

    public AccountController(AppDbContext db)
    {
        _db = db;
    }

    // ---------- REGISTER ----------

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        bool emailExists = await _db.Users.AnyAsync(u => u.Email == model.Email);
        if (emailExists)
        {
            ModelState.AddModelError(nameof(model.Email), "Email sudah terdaftar.");
            return View(model);
        }

        var user = new User
        {
            FullName = model.FullName,
            Email = model.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Registrasi berhasil, silakan login.";
        return RedirectToAction(nameof(Login));
    }

    // ---------- LOGIN / LOGOUT ----------

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == model.Email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Email atau password salah.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = model.RememberMe });

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    // ---------- FORGOT PASSWORD ----------

    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == model.Email);

        // PENTING: jangan kasih tau kalau email gak ketemu — biar gak bisa dipakai
        // orang buat "menebak" email mana yang terdaftar (user enumeration).
        if (user is not null)
        {
            user.ResetPasswordToken = Guid.NewGuid().ToString("N");
            user.PasswordResetTokenExpireAt = DateTime.UtcNow.AddMinutes(30);
            await _db.SaveChangesAsync();

            var resetLink = Url.Action(nameof(ResetPassword), "Account",
                new { email = user.Email, token = user.ResetPasswordToken }, Request.Scheme);

            // TODO: ganti ini dengan kirim email beneran (lihat bagian "Kirim email beneran" di bawah).
            // Untuk development, tampilkan link-nya langsung di halaman konfirmasi:
            TempData["DevResetLink"] = resetLink;
        }

        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [HttpGet]
    public IActionResult ForgotPasswordConfirmation() => View();

    // ---------- RESET PASSWORD ----------

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string email, string token)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Email == email &&
            u.ResetPasswordToken == token &&
            u.PasswordResetTokenExpireAt > DateTime.UtcNow);

        if (user is null)
        {
            TempData["Error"] = "Link reset tidak valid atau sudah kedaluwarsa.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new ResetPasswordViewModel { Email = email, Token = token });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Email == model.Email &&
            u.ResetPasswordToken == model.Token &&
            u.PasswordResetTokenExpireAt > DateTime.UtcNow);

        if (user is null)
        {
            TempData["Error"] = "Link reset tidak valid atau sudah kedaluwarsa.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
        user.ResetPasswordToken = null;
        user.PasswordResetTokenExpireAt = null;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Password berhasil diubah, silakan login.";
        return RedirectToAction(nameof(Login));
    }
}
