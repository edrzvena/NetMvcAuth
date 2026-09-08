# Panduan Lengkap: Auth (Register, Login, Forgot Password) — ASP.NET Core MVC

Project: `NetMvcAuth` — .NET 10, EF Core SqlServer, BCrypt.Net-Next, Cookie Authentication.

Guide ini **lengkap & copy-paste ready** — tiap file dikasih isi final-nya. Ikutin urutannya dari atas ke bawah, jangan loncat, karena tiap fase bergantung ke fase sebelumnya (Model harus ada dulu sebelum migration, migration harus jalan dulu sebelum Controller dites, dst).

> ⚠️ **Catatan penting soal penamaan:** nama property di `Models/User.cs` **harus persis sama** (huruf besar/kecil termasuk) dengan yang dipanggil di `Controllers/AccountController.cs`. Guide ini pakai `ResetPasswordToken` dan `PasswordResetTokenExpireAt` sebagai nama resminya — dipakai konsisten di semua file di bawah. Kalau lo copy sebagian dari sumber lain dan nama-nya beda dikit (misal `PasswordResetToken` vs `ResetPasswordToken`), project **gak akan bisa di-build**. Kalau ragu, tinggal copy-paste utuh dari guide ini.

---

## 0. Prasyarat

- **.NET SDK 10** — cek dengan `dotnet --version`.
- **SQL Server** (Express/LocalDB/Developer edition) — cek nama instance-nya di SSMS/Azure Data Studio.
- Editor: Visual Studio / VS Code / Rider (bebas).

---

## 1. Bikin Project dari Nol

Skip bagian ini kalau foldernya udah ada (kasus lo sekarang).

```powershell
dotnet new mvc -n NetMvcAuth
cd NetMvcAuth
```

**Install package:**
```powershell
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package BCrypt.Net-Next
```

| Package | Fungsi |
|---|---|
| `Microsoft.EntityFrameworkCore.SqlServer` | ORM buat konek & query ke SQL Server |
| `Microsoft.EntityFrameworkCore.Design` | Tooling buat generate migration (`dotnet ef ...`) |
| `BCrypt.Net-Next` | Hash & verifikasi password |

**Install EF Core CLI tool** (sekali per komputer, bukan per project):
```powershell
dotnet tool install --global dotnet-ef
```

**Bikin folder yang belum ada bawaan template:**
```powershell
New-Item -ItemType Directory -Force -Path Data, ViewModels
```

Struktur akhir yang mau dicapai:
```
NetMvcAuth/
├── Controllers/
│   ├── AccountController.cs
│   └── HomeController.cs
├── Models/
│   ├── User.cs
│   └── ErrorViewModel.cs
├── ViewModels/
│   ├── RegisterViewModel.cs
│   ├── LoginViewModel.cs
│   ├── ForgotPasswordViewModel.cs
│   └── ResetPasswordViewModel.cs
├── Data/
│   └── AppDbContext.cs
├── Views/
│   ├── Account/
│   │   ├── Register.cshtml
│   │   ├── Login.cshtml
│   │   ├── ForgotPassword.cshtml
│   │   ├── ForgotPasswordConfirmation.cshtml
│   │   └── ResetPassword.cshtml
│   ├── Home/
│   └── Shared/_Layout.cshtml
├── Program.cs
└── appsettings.json
```

---

## 2. Connection String — `appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=db_net_mvc_auth;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```
Ganti `.\\SQLEXPRESS` sesuai instance SQL Server lo. Default instance → `Server=.;...`. LocalDB → `Server=(localdb)\\mssqllocaldb;...`.

---

## 3. `Models/User.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace NetMvcAuth.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required, EmailAddress, MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        // Feature forgot password
        public string? ResetPasswordToken { get; set; }
        public DateTime? PasswordResetTokenExpireAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
```

---

## 4. ViewModels (4 file)

`ViewModels/RegisterViewModel.cs`
```csharp
using System.ComponentModel.DataAnnotations;

namespace NetMvcAuth.ViewModels;

public class RegisterViewModel
{
    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Password tidak sama.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
```

`ViewModels/LoginViewModel.cs`
```csharp
using System.ComponentModel.DataAnnotations;

namespace NetMvcAuth.ViewModels;

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}
```

`ViewModels/ForgotPasswordViewModel.cs`
```csharp
using System.ComponentModel.DataAnnotations;

namespace NetMvcAuth.ViewModels;

public class ForgotPasswordViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}
```

`ViewModels/ResetPasswordViewModel.cs`
```csharp
using System.ComponentModel.DataAnnotations;

namespace NetMvcAuth.ViewModels;

public class ResetPasswordViewModel
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6), DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Password tidak sama.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
```

---

## 5. `Data/AppDbContext.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using NetMvcAuth.Models;

namespace NetMvcAuth.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
    }
}
```

---

## 6. `Program.cs` (isi lengkap file, ganti semua)

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using NetMvcAuth.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();   // WAJIB sebelum UseAuthorization
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
```

**Poin krusial:** `UseAuthentication()` **harus** dipanggil sebelum `UseAuthorization()`, dan keduanya sebelum `MapControllerRoute`. Kalau kebalik/hilang, `[Authorize]` gak akan jalan dan cookie login gak kebaca.

---

## 7. Migration & Update Database

Jalankan di terminal (folder root project, sejajar dengan `.csproj`):

```powershell
dotnet ef migrations add InitialCreate
dotnet ef database update
```

Cek hasilnya di SSMS — harus muncul database `db_net_mvc_auth` dengan tabel `Users`, kolom: `Id, Email, FullName, PasswordHash, ResetPasswordToken, PasswordResetTokenExpireAt, CreatedAt`.

> Kalau lo edit ulang `Models/User.cs` setelah ini (nambah/ubah kolom), jangan edit tabel manual di SSMS — selalu `dotnet ef migrations add <NamaPerubahan>` lagi, baru `dotnet ef database update`.

---

## 8. `Controllers/AccountController.cs` (isi lengkap file, ganti semua)

```csharp
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
```

---

## 9. Views — `Views/Account/`

`Views/Account/Register.cshtml`
```cshtml
@model NetMvcAuth.ViewModels.RegisterViewModel
@{ ViewData["Title"] = "Register"; }

<h2>Register</h2>
<form asp-action="Register" method="post">
    <div asp-validation-summary="All" class="text-danger"></div>

    <div class="mb-3">
        <label asp-for="FullName" class="form-label"></label>
        <input asp-for="FullName" class="form-control" />
        <span asp-validation-for="FullName" class="text-danger"></span>
    </div>
    <div class="mb-3">
        <label asp-for="Email" class="form-label"></label>
        <input asp-for="Email" class="form-control" />
        <span asp-validation-for="Email" class="text-danger"></span>
    </div>
    <div class="mb-3">
        <label asp-for="Password" class="form-label"></label>
        <input asp-for="Password" class="form-control" />
        <span asp-validation-for="Password" class="text-danger"></span>
    </div>
    <div class="mb-3">
        <label asp-for="ConfirmPassword" class="form-label"></label>
        <input asp-for="ConfirmPassword" class="form-control" />
        <span asp-validation-for="ConfirmPassword" class="text-danger"></span>
    </div>
    <button type="submit" class="btn btn-primary">Register</button>
    <a asp-action="Login" class="btn btn-link">Sudah punya akun? Login</a>
</form>

@section Scripts {
    @{ await Html.RenderPartialAsync("_ValidationScriptsPartial"); }
}
```

`Views/Account/Login.cshtml`
```cshtml
@model NetMvcAuth.ViewModels.LoginViewModel
@{ ViewData["Title"] = "Login"; }

<h2>Login</h2>

@if (TempData["Success"] is string success)
{
    <div class="alert alert-success">@success</div>
}

<form asp-action="Login" asp-route-returnUrl="@ViewData["ReturnUrl"]" method="post">
    <div asp-validation-summary="All" class="text-danger"></div>

    <div class="mb-3">
        <label asp-for="Email" class="form-label"></label>
        <input asp-for="Email" class="form-control" />
        <span asp-validation-for="Email" class="text-danger"></span>
    </div>
    <div class="mb-3">
        <label asp-for="Password" class="form-label"></label>
        <input asp-for="Password" class="form-control" />
        <span asp-validation-for="Password" class="text-danger"></span>
    </div>
    <div class="mb-3 form-check">
        <input asp-for="RememberMe" class="form-check-input" />
        <label asp-for="RememberMe" class="form-check-label"></label>
    </div>
    <button type="submit" class="btn btn-primary">Login</button>
    <a asp-action="Register" class="btn btn-link">Belum punya akun? Register</a>
    <div class="mt-2">
        <a asp-action="ForgotPassword">Lupa password?</a>
    </div>
</form>

@section Scripts {
    @{ await Html.RenderPartialAsync("_ValidationScriptsPartial"); }
}
```

`Views/Account/ForgotPassword.cshtml` **(file baru)**
```cshtml
@model NetMvcAuth.ViewModels.ForgotPasswordViewModel
@{ ViewData["Title"] = "Forgot Password"; }

<h2>Lupa Password</h2>
<p>Masukkan email yang kamu pakai buat register. Kami akan kirim link buat reset password.</p>

<form asp-action="ForgotPassword" method="post">
    <div asp-validation-summary="All" class="text-danger"></div>

    <div class="mb-3">
        <label asp-for="Email" class="form-label"></label>
        <input asp-for="Email" class="form-control" />
        <span asp-validation-for="Email" class="text-danger"></span>
    </div>
    <button type="submit" class="btn btn-primary">Kirim Link Reset</button>
</form>

@section Scripts {
    @{ await Html.RenderPartialAsync("_ValidationScriptsPartial"); }
}
```

`Views/Account/ForgotPasswordConfirmation.cshtml` **(file baru)**
```cshtml
@{ ViewData["Title"] = "Cek Email"; }

<h2>Cek email kamu</h2>
<p>Kalau email tadi terdaftar di sistem kami, link reset password sudah "dikirim".</p>

@if (TempData["DevResetLink"] is string link)
{
    <div class="alert alert-warning">
        <strong>[DEV ONLY]</strong> Ini link reset-nya (blok ini dihapus kalau sudah pakai email beneran, lihat bagian "Kirim email beneran" di guide):<br />
        <a href="@link">@link</a>
    </div>
}
```

`Views/Account/ResetPassword.cshtml` **(saat ini kosong — isi dengan ini)**
```cshtml
@model NetMvcAuth.ViewModels.ResetPasswordViewModel
@{ ViewData["Title"] = "Reset Password"; }

<h2>Reset Password</h2>

@if (TempData["Error"] is string error)
{
    <div class="alert alert-danger">@error</div>
}

<form asp-action="ResetPassword" method="post">
    <div asp-validation-summary="All" class="text-danger"></div>

    <input type="hidden" asp-for="Email" />
    <input type="hidden" asp-for="Token" />

    <div class="mb-3">
        <label asp-for="NewPassword" class="form-label"></label>
        <input asp-for="NewPassword" class="form-control" />
        <span asp-validation-for="NewPassword" class="text-danger"></span>
    </div>
    <div class="mb-3">
        <label asp-for="ConfirmPassword" class="form-label"></label>
        <input asp-for="ConfirmPassword" class="form-control" />
        <span asp-validation-for="ConfirmPassword" class="text-danger"></span>
    </div>
    <button type="submit" class="btn btn-primary">Simpan Password Baru</button>
</form>

@section Scripts {
    @{ await Html.RenderPartialAsync("_ValidationScriptsPartial"); }
}
```

---

## 10. `Views/Shared/_Layout.cshtml` — tampilin status login di navbar

Ganti bagian `<div class="navbar-collapse collapse d-sm-inline-flex justify-content-between">` jadi begini (bagian lain di file dibiarin sama):

```cshtml
<div class="navbar-collapse collapse d-sm-inline-flex justify-content-between">
    <ul class="navbar-nav flex-grow-1">
        <li class="nav-item">
            <a class="nav-link text-dark" asp-area="" asp-controller="Home" asp-action="Index">Home</a>
        </li>
        <li class="nav-item">
            <a class="nav-link text-dark" asp-area="" asp-controller="Home" asp-action="Privacy">Privacy</a>
        </li>
    </ul>
    <ul class="navbar-nav">
        @if (User.Identity is not null && User.Identity.IsAuthenticated)
        {
            <li class="nav-item d-flex align-items-center me-2">
                <span class="text-dark">Halo, @User.Identity.Name</span>
            </li>
            <li class="nav-item">
                <form asp-controller="Account" asp-action="Logout" method="post" class="d-inline">
                    <button type="submit" class="btn btn-link nav-link text-dark p-0">Logout</button>
                </form>
            </li>
        }
        else
        {
            <li class="nav-item">
                <a class="nav-link text-dark" asp-controller="Account" asp-action="Login">Login</a>
            </li>
            <li class="nav-item">
                <a class="nav-link text-dark" asp-controller="Account" asp-action="Register">Register</a>
            </li>
        }
    </ul>
</div>
```

**(Opsional)** proteksi halaman Privacy biar cuma bisa diakses kalau udah login — di `Controllers/HomeController.cs`, tambahin `using Microsoft.AspNetCore.Authorization;` di atas, lalu taro `[Authorize]` di atas method `Privacy()`:
```csharp
[Authorize]
public IActionResult Privacy()
{
    return View();
}
```

---

## 11. Jalankan & Testing (checklist urut)

```powershell
dotnet build   # pastiin gak ada error compile dulu
dotnet run
```

Buka URL yang muncul di terminal, lalu tes urut:

1. `/Account/Register` → isi form → submit → harus redirect ke `/Account/Login` dengan pesan sukses.
2. Cek tabel `Users` di SSMS → `PasswordHash` harus string acak panjang (bukan plain text password).
3. `/Account/Login` → login pakai akun tadi → harus redirect ke Home, navbar berubah jadi "Halo, ..." + tombol Logout.
4. Coba buka `/Home/Privacy` (kalau udah dipasangin `[Authorize]`) sambil login → harus bisa diakses.
5. Klik Logout → navbar balik ke Login/Register. Coba akses `/Home/Privacy` lagi → harus ke-redirect ke `/Account/Login`.
6. `/Account/ForgotPassword` → masukin email yang terdaftar → submit → halaman konfirmasi muncul link `[DEV ONLY]`.
7. Klik link dev tadi → form Reset Password muncul → isi password baru → submit → redirect ke Login dengan pesan sukses.
8. Login pakai password **baru** → harus berhasil. Login pakai password **lama** → harus gagal ("Email atau password salah").
9. Coba `/Account/ForgotPassword` pakai email yang **gak terdaftar** → tetap diarahkan ke halaman konfirmasi yang sama, tapi **tanpa** kotak link dev (biar gak bocorin email mana yang terdaftar).

Kalau semua 9 langkah ini lolos, alur auth-nya udah lengkap dan jalan.

---

## 12. Troubleshooting umum

| Gejala | Penyebab | Solusi |
|---|---|---|
| `'User' does not contain a definition for 'ResetPasswordToken'` (atau nama lain) | Nama property di `Models/User.cs` beda sama yang dipanggil di Controller | Samain persis — pakai nama dari guide ini di kedua file |
| `Cannot open database "db_net_mvc_auth"` | Migration belum dijalankan / connection string salah | Cek `appsettings.json`, jalankan `dotnet ef database update` |
| Login sukses tapi `[Authorize]` tetap nolak / navbar gak berubah | `app.UseAuthentication()` hilang atau taronya setelah `UseAuthorization()`/`MapControllerRoute` | Cek urutan di `Program.cs` sesuai bagian 6 |
| Submit form kena error "antiforgery token" | Form gak pakai tag helper `asp-action` / `[ValidateAntiForgeryToken]` di action GET | Pastiin semua `<form>` pakai `asp-action`/`asp-controller`, jangan `action="..."` manual |
| Migration nolak jalan karena kolom lama | Sebelumnya udah pernah migrate dengan nama kolom beda | Hapus folder `Migrations/` + drop database (`dotnet ef database drop`), lalu `dotnet ef migrations add InitialCreate` ulang dari nol |

---

## 13 (Opsional, lanjutan) — Naik level: Services, DTO, Middleware, Common

Bagian ini **gak wajib** buat auth-nya jalan — ini buat belajar struktur project .NET yang lebih rapi setelah versi dasarnya berhasil. Skip dulu kalau fokus lo sekarang cuma "biar jalan".

### Kenapa?
Sampai bagian 8, semua logic (cek email exist, hash password, generate token) numpuk di `AccountController`. Di project .NET yang lebih serius, ini biasa dipisah:

```
Services/    -> business logic (pindahin dari Controller)
Common/      -> utilitas kecil dipake lintas layer (TokenGenerator, ServiceResult)
Middlewares/ -> custom pipeline component (logging, dll)
DTOs/        -> bentuk data buat API JSON (gak perlu di MVC murni — ViewModel udah cukup)
```

### `Common/ServiceResult.cs`
```csharp
namespace NetMvcAuth.Common;

public class ServiceResult
{
    public bool Success { get; init; }
    public List<string> Errors { get; init; } = new();

    public static ServiceResult Ok() => new() { Success = true };
    public static ServiceResult Fail(string error) => new() { Success = false, Errors = { error } };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; init; }

    public static ServiceResult<T> Ok(T data) => new() { Success = true, Data = data };
    public static new ServiceResult<T> Fail(string error) => new() { Success = false, Errors = { error } };
}
```

### `Common/TokenGenerator.cs`
```csharp
using System.Security.Cryptography;

namespace NetMvcAuth.Common;

public static class TokenGenerator
{
    public static string CreateToken(int byteLength = 32) =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(byteLength));
}
```

### `Services/IAccountService.cs`
```csharp
using NetMvcAuth.Common;
using NetMvcAuth.Models;
using NetMvcAuth.ViewModels;

namespace NetMvcAuth.Services;

public interface IAccountService
{
    Task<ServiceResult> RegisterAsync(RegisterViewModel model);
    Task<ServiceResult<User>> ValidateLoginAsync(LoginViewModel model);
    Task RequestPasswordResetAsync(string email, Func<string, string, string> buildResetLink);
    Task<ServiceResult> ResetPasswordAsync(ResetPasswordViewModel model);
}
```

### `Services/AccountService.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using NetMvcAuth.Common;
using NetMvcAuth.Data;
using NetMvcAuth.Models;
using NetMvcAuth.ViewModels;

namespace NetMvcAuth.Services;

public class AccountService : IAccountService
{
    private readonly AppDbContext _db;
    public AccountService(AppDbContext db) => _db = db;

    public async Task<ServiceResult> RegisterAsync(RegisterViewModel model)
    {
        bool emailExists = await _db.Users.AnyAsync(u => u.Email == model.Email);
        if (emailExists) return ServiceResult.Fail("Email sudah terdaftar.");

        var user = new User
        {
            FullName = model.FullName,
            Email = model.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<User>> ValidateLoginAsync(LoginViewModel model)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == model.Email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
            return ServiceResult<User>.Fail("Email atau password salah.");

        return ServiceResult<User>.Ok(user);
    }

    public async Task RequestPasswordResetAsync(string email, Func<string, string, string> buildResetLink)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null) return; // jangan bocorin apakah email terdaftar

        user.ResetPasswordToken = TokenGenerator.CreateToken();
        user.PasswordResetTokenExpireAt = DateTime.UtcNow.AddMinutes(30);
        await _db.SaveChangesAsync();

        var resetLink = buildResetLink(user.Email, user.ResetPasswordToken);
        // TODO: kirim resetLink via email (lihat bagian "Kirim email beneran")
    }

    public async Task<ServiceResult> ResetPasswordAsync(ResetPasswordViewModel model)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Email == model.Email &&
            u.ResetPasswordToken == model.Token &&
            u.PasswordResetTokenExpireAt > DateTime.UtcNow);

        if (user is null) return ServiceResult.Fail("Link reset tidak valid atau sudah kedaluwarsa.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
        user.ResetPasswordToken = null;
        user.PasswordResetTokenExpireAt = null;
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }
}
```

Daftarin ke DI di `Program.cs` (dekat `AddDbContext`):
```csharp
builder.Services.AddScoped<IAccountService, NetMvcAuth.Services.AccountService>();
```

**Contoh Controller sesudah refactor** (method `Login` — pola sama berlaku ke method lain: Controller cuma urus HTTP, Service urus logic):
```csharp
private readonly IAccountService _accountService;

public AccountController(IAccountService accountService)
{
    _accountService = accountService;
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
{
    if (!ModelState.IsValid) return View(model);

    var result = await _accountService.ValidateLoginAsync(model);
    if (!result.Success)
    {
        ModelState.AddModelError(string.Empty, result.Errors.First());
        return View(model);
    }

    var user = result.Data!;
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Name, user.FullName),
        new(ClaimTypes.Email, user.Email),
    };
    var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
        new AuthenticationProperties { IsPersistent = model.RememberMe });

    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
    return RedirectToAction("Index", "Home");
}
```

### `Middlewares/RequestLoggingMiddleware.cs` (contoh custom middleware)
```csharp
namespace NetMvcAuth.Middlewares;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.User.Identity?.IsAuthenticated == true
            ? context.User.Identity!.Name
            : "Anonymous";

        _logger.LogInformation("{Method} {Path} by {User}", context.Request.Method, context.Request.Path, user);

        await _next(context);
    }
}
```
Daftarin di `Program.cs`, **setelah** `app.UseAuthentication()`:
```csharp
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<NetMvcAuth.Middlewares.RequestLoggingMiddleware>();
```

### `DTOs/` — perlu gak?
**Untuk project MVC murni ini, nggak perlu.** `ViewModel` udah menjalankan peran itu (bentuk data aman buat View). DTO baru kepake kalau lo nanti nambah endpoint yang return JSON (Web API), biar field sensitif kayak `PasswordHash`/`ResetPasswordToken` gak ikut ke-serialize:
```csharp
namespace NetMvcAuth.DTOs;

public class UserDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public static UserDto FromEntity(Models.User user) => new()
    {
        Id = user.Id,
        FullName = user.FullName,
        Email = user.Email
    };
}
```

### Kirim email beneran (ganti `TempData["DevResetLink"]`)
Termudah buat belajar: [Mailtrap](https://mailtrap.io) (sandbox gratis, gak nyampur ke inbox asli) + `MailKit`.
```powershell
dotnet add package MailKit
```
```csharp
var message = new MimeMessage();
message.From.Add(MailboxAddress.Parse("noreply@netmvcauth.com"));
message.To.Add(MailboxAddress.Parse(user.Email));
message.Subject = "Reset Password";
message.Body = new TextPart("html") { Text = $"Klik link ini: {resetLink}" };

using var client = new SmtpClient();
await client.ConnectAsync("sandbox.smtp.mailtrap.io", 2525, false);
await client.AuthenticateAsync("<username>", "<password>");
await client.SendAsync(message);
await client.DisconnectAsync(true);
```
Naro credential SMTP di `dotnet user-secrets` atau `appsettings.Development.json`, jangan hardcode / jangan commit ke git.

---

## Referensi
- Cookie Authentication: https://learn.microsoft.com/aspnet/core/security/authentication/cookie
- BCrypt.Net-Next: https://github.com/BcryptNet/bcrypt.net
- EF Core Migrations: https://learn.microsoft.com/ef/core/managing-schemas/migrations/
