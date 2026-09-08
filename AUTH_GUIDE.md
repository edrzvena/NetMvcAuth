# Panduan Belajar: Auth (Register, Login, Forgot Password) di ASP.NET Core MVC

Project: `NetMvcAuth` — .NET 10, EF Core SqlServer, BCrypt.Net-Next.

Cek dulu apa yang udah ada:
- `BCrypt.Net-Next` udah keinstall → dipakai buat hash password (JANGAN simpan password plain text ke DB).
- `Microsoft.EntityFrameworkCore.SqlServer` + `.Design` udah keinstall.
- `Data/AppDbContext.cs` masih kosong (belum ada `DbSet`).
- `Program.cs` belum daftarin `AppDbContext` ke DI, belum ada Authentication middleware.
- Belum ada folder `Models` isi (cuma `ErrorViewModel`), `ViewModels` masih kosong, belum ada `AccountController`.

Pendekatan yang dipakai di guide ini: **cookie-based auth manual** (bukan ASP.NET Identity), soalnya lo udah pasang BCrypt sendiri — jadi ini cocok buat belajar konsepnya dari nol: hashing, session/cookie, token reset password.

Setiap fase ada: **Tujuan**, **Konsep**, **Langkah**, **Contoh kode**. Saran: coba tulis sendiri dulu berdasarkan Tujuan+Konsep, baru cocokin ke Contoh kode kalau stuck.

---

## Fase -1 — Bikin project dari nol (buat yang mau ngikutin dari awal)

Skip fase ini kalau lo udah punya foldernya. Ini buat orang lain yang mau reproduce project ini dari kosong.

**1. Pastikan .NET SDK 10 sudah terinstall**
```powershell
dotnet --version   # harus 10.x
```
Kalau belum ada, download dari https://dotnet.microsoft.com/download

**2. Bikin project MVC baru**
```powershell
dotnet new mvc -n NetMvcAuth
cd NetMvcAuth
```
`dotnet new mvc` = template starter ASP.NET Core MVC (udah include `Controllers/HomeController.cs`, `Views/`, `wwwroot/`, dll — persis skeleton project ini). Flag `-n NetMvcAuth` sekalian nentuin nama project & namespace default.

**3. Install package yang dipakai**

| Package | Fungsi |
|---|---|
| `Microsoft.EntityFrameworkCore.SqlServer` | ORM buat konek & query ke SQL Server |
| `Microsoft.EntityFrameworkCore.Design` | Tooling buat bikin migration (`dotnet ef ...`) |
| `BCrypt.Net-Next` | Hash & verifikasi password |

```powershell
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package BCrypt.Net-Next
```
Versi yang dipakai pas guide ini ditulis: EF Core `10.0.11`, BCrypt.Net-Next `4.2.0`. Kalau mau ngunci versi tertentu, tambahin `--version 10.0.11` di belakang command-nya. Setelah ini, cek `.csproj` — isinya harus mirip yang ada di `NetMvcAuth.csproj` sekarang.

**4. Install EF Core CLI tool (sekali per komputer, bukan per project)**
```powershell
dotnet tool install --global dotnet-ef
```
Kalau sebelumnya udah pernah install versi lama, update dengan `dotnet tool update --global dotnet-ef`.

**5. Bikin folder buat kode yang belum ada bawaan template**
```powershell
New-Item -ItemType Directory -Force -Path Data, ViewModels
```
(`Models/` udah ada bawaan `dotnet new mvc`, jadi gak perlu dibikin lagi.)

**6. Bikin `Data/AppDbContext.cs` kosong dulu** (isinya dilengkapi di Fase 2 & 1 di bawah):
```csharp
using Microsoft.EntityFrameworkCore;

namespace NetMvcAuth.Data;

public class AppDbContext : DbContext
{
}
```

**7. Set connection string di `appsettings.json`**

Tambahin section ini (ganti `.\\SQLEXPRESS` sesuai instance SQL Server lo — bisa cek nama instance di SSMS):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=db_net_mvc_auth;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```
Kalau pakai SQL Server default instance (bukan SQLEXPRESS), `Server=.;...` aja cukup. Kalau pakai LocalDB, `Server=(localdb)\\mssqllocaldb;...`.

**8. Jalanin buat mastiin semuanya nyala**
```powershell
dotnet run
```
Buka URL yang muncul di terminal (biasanya `https://localhost:xxxx`) — harus muncul halaman default MVC ("Welcome").

Dari sini lanjut ke **Fase 0** ke bawah buat mulai bikin fitur auth-nya.

---

## Fase 0 — Alur besar

1. User isi form Register → password di-hash → simpan ke DB.
2. User Login → input dicocokin ke hash di DB → kalau cocok, bikin cookie auth (`SignInAsync`).
3. Halaman tertentu di-proteksi pakai `[Authorize]` → cuma bisa diakses kalau ada cookie valid.
4. Forgot Password → generate token random + expiry, simpan di DB, kirim link `?token=...` (email beneran atau disimulasikan dulu pas development).
5. Reset Password → validasi token & expiry → update hash password baru → hapus token.

---

## Fase 1 — Model `User`

**Tujuan:** punya representasi user di DB.

**Konsep:** kolom password itu `PasswordHash` (string hasil BCrypt), bukan `Password`. Tambahin juga kolom buat reset token dari awal biar gak perlu migration lagi nanti.

**Langkah:**
- Bikin `Models/User.cs`.

**Contoh kode:**
```csharp
using System.ComponentModel.DataAnnotations;

namespace NetMvcAuth.Models;

public class User
{
    public int Id { get; set; }

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    // Buat fitur Forgot Password
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

---

## Fase 2 — Daftarin `DbSet` & `AppDbContext` ke DI

**Tujuan:** EF Core tau tabel apa aja yang mau di-manage, dan `AppDbContext` bisa di-inject ke Controller.

**Langkah:**
1. Edit `Data/AppDbContext.cs`, tambahin constructor + `DbSet<User>`.
2. Edit `Program.cs`, daftarin `AddDbContext` pakai connection string dari `appsettings.json` (udah ada: `DefaultConnection`).

**Contoh kode — `Data/AppDbContext.cs`:**
```csharp
using Microsoft.EntityFrameworkCore;
using NetMvcAuth.Models;

namespace NetMvcAuth.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
}
```

**Contoh kode — `Program.cs` (tambahin sebelum `var app = builder.Build();`):**
```csharp
using Microsoft.EntityFrameworkCore;
using NetMvcAuth.Data;

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
```
> Jangan lupa `Email` sebaiknya unik. Bisa diatur lewat Fluent API (`OnModelCreating`) kalau mau, tapi validasi manual di controller juga cukup buat awal.

---

## Fase 3 — Migration & Update Database

**Tujuan:** bikin tabel `Users` beneran di SQL Server.

**Langkah (jalankan di terminal, folder project):**
```powershell
dotnet tool install --global dotnet-ef   # sekali aja kalau belum ada
dotnet ef migrations add InitialCreate
dotnet ef database update
```
Cek hasilnya di SSMS/Azure Data Studio — harus ada tabel `Users` dengan kolom sesuai model.

---

## Fase 4 — ViewModels

**Tujuan:** pisahin bentuk data buat form (yang user isi) dari `User` model (yang disimpan ke DB). Ini best practice — jangan bind form langsung ke Entity.

**Langkah:** bikin 4 file di folder `ViewModels/`.

```csharp
// ViewModels/RegisterViewModel.cs
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

```csharp
// ViewModels/LoginViewModel.cs
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

```csharp
// ViewModels/ForgotPasswordViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace NetMvcAuth.ViewModels;

public class ForgotPasswordViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}
```

```csharp
// ViewModels/ResetPasswordViewModel.cs
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

## Fase 5 — Setup Cookie Authentication (`Program.cs`)

**Tujuan:** biar ada mekanisme "login state" — `SignInAsync`/`SignOutAsync`/`[Authorize]` bisa dipakai.

**Konsep:** ASP.NET Core punya sistem Authentication yang pluggable. Untuk MVC klasik, skema yang dipakai adalah **Cookie Authentication** — begitu login sukses, server bikin cookie terenkripsi berisi *claims* (identitas user), lalu browser kirim cookie itu di tiap request berikutnya.

**Langkah:** tambahin di `Program.cs`, sebelum `builder.Build()`:
```csharp
using Microsoft.AspNetCore.Authentication.Cookies;

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });
```

Lalu tambahin `app.UseAuthentication();` **SEBELUM** `app.UseAuthorization();` di pipeline (urutan ini penting — authentication dulu baru authorization):
```csharp
app.UseAuthentication();
app.UseAuthorization();
```

---

## Fase 6 — `AccountController`: Register

**Tujuan:** endpoint GET (tampilin form) & POST (proses submit + hash + simpan).

**Konsep:** `BCrypt.Net.BCrypt.HashPassword(password)` otomatis generate salt + hash, aman disimpan ke DB.

**Contoh kode — `Controllers/AccountController.cs` (bagian Register dulu):**
```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetMvcAuth.Data;
using NetMvcAuth.Models;
using NetMvcAuth.ViewModels;

namespace NetMvcAuth.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _db;

    public AccountController(AppDbContext db)
    {
        _db = db;
    }

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
}
```

**View — `Views/Account/Register.cshtml`:**
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
</form>

@section Scripts {
    @{ await Html.RenderPartialAsync("_ValidationScriptsPartial"); }
}
```

**Test:** jalanin `dotnet run`, buka `/Account/Register`, submit, cek tabel `Users` di DB — `PasswordHash` harus string acak panjang (bukan plain text).

---

## Fase 7 — `AccountController`: Login

**Tujuan:** verifikasi password, bikin cookie kalau valid.

**Konsep:** `BCrypt.Net.BCrypt.Verify(inputPassword, storedHash)` — jangan pernah bandingin hash pakai `==`. Setelah valid, bikin `ClaimsPrincipal` isi identitas user, lalu `HttpContext.SignInAsync(...)`.

**Tambahin ke `AccountController`:**
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

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
```

**View — `Views/Account/Login.cshtml`:** mirip `Register.cshtml`, tinggal ganti field jadi Email + Password + checkbox RememberMe.

**Coba di `HomeController`:** tambahin `[Authorize]` di atas `public IActionResult Privacy()` buat tes proteksi halaman. Kalau belum login, akses `/Home/Privacy` harus auto-redirect ke `/Account/Login`.

---

## Fase 8 — Forgot Password (kirim token)

**Tujuan:** user input email → sistem generate token unik + expiry → (idealnya) dikirim via email berisi link reset.

**Konsep:** token harus random & sulit ditebak (`Guid` atau `RandomNumberGenerator`), dan **punya masa berlaku** (misal 30 menit) biar gak bisa dipakai selamanya kalau link bocor.

```csharp
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
        user.PasswordResetToken = Guid.NewGuid().ToString("N");
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(30);
        await _db.SaveChangesAsync();

        var resetLink = Url.Action(nameof(ResetPassword), "Account",
            new { email = user.Email, token = user.PasswordResetToken }, Request.Scheme);

        // TODO: ganti ini dengan kirim email beneran (lihat Fase 10).
        // Untuk development, tampilkan link-nya langsung di halaman:
        TempData["DevResetLink"] = resetLink;
    }

    return RedirectToAction(nameof(ForgotPasswordConfirmation));
}

[HttpGet]
public IActionResult ForgotPasswordConfirmation() => View();
```

**View `ForgotPasswordConfirmation.cshtml`** — sementara pas dev, tampilin `TempData["DevResetLink"]` biar bisa langsung diklik tanpa perlu setup email server dulu:
```cshtml
<h2>Cek email kamu</h2>
<p>Kalau email terdaftar, link reset password sudah dikirim.</p>

@if (TempData["DevResetLink"] is string link)
{
    <div class="alert alert-warning">
        <strong>[DEV ONLY]</strong> Link reset (nanti dihapus kalau sudah pakai email beneran):<br />
        <a href="@link">@link</a>
    </div>
}
```

---

## Fase 9 — Reset Password

**Tujuan:** validasi token dari link, kalau valid → update `PasswordHash`.

```csharp
[HttpGet]
public async Task<IActionResult> ResetPassword(string email, string token)
{
    var user = await _db.Users.FirstOrDefaultAsync(u =>
        u.Email == email &&
        u.PasswordResetToken == token &&
        u.PasswordResetTokenExpiresAt > DateTime.UtcNow);

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
        u.PasswordResetToken == model.Token &&
        u.PasswordResetTokenExpiresAt > DateTime.UtcNow);

    if (user is null)
    {
        TempData["Error"] = "Link reset tidak valid atau sudah kedaluwarsa.";
        return RedirectToAction(nameof(ForgotPassword));
    }

    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
    user.PasswordResetToken = null;
    user.PasswordResetTokenExpiresAt = null;
    await _db.SaveChangesAsync();

    TempData["Success"] = "Password berhasil diubah, silakan login.";
    return RedirectToAction(nameof(Login));
}
```

**View `ResetPassword.cshtml`** — form dengan hidden field `Token` & `Email`, plus input `NewPassword` & `ConfirmPassword`.

---

## Fase 10 (opsional) — Kirim email beneran

Pas udah jalan pakai `TempData["DevResetLink"]`, ganti dengan kirim SMTP beneran. Termudah buat belajar: pakai [Mailtrap](https://mailtrap.io) (sandbox, gratis, gak nyampur ke inbox asli) atau `MailKit` package.

```powershell
dotnet add package MailKit
```
```csharp
// Contoh kasar, taruh di service terpisah (IEmailSender) biar rapi
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
Naro credential SMTP di `appsettings.Development.json` (atau `dotnet user-secrets`), jangan hardcode.

---

## Fase 11 — Sentuhan akhir (checklist)

- [ ] Update `_Layout.cshtml`: tampilin "Halo, @User.Identity!.Name" + tombol Logout kalau `User.Identity!.IsAuthenticated`, kalau nggak tampilin link Login/Register.
- [ ] `[Authorize]` di halaman yang butuh login (mis. Dashboard/Profile).
- [ ] Rate limit / lockout sederhana biar gak brute-force (mis. hitung percobaan login gagal).
- [ ] Index unik di kolom `Email` (`.HasIndex(u => u.Email).IsUnique()` di `OnModelCreating`) biar constraint-nya juga dijaga di level DB, bukan cuma di controller.
- [ ] Jangan pernah log/tampilin password asli di mana pun (termasuk `TempData`).
- [ ] Test alur lengkap: Register → Login → akses halaman `[Authorize]` → Logout → Forgot Password → klik link dev → Reset → Login pakai password baru.

---

## Fase 12 (opsional, tapi disaranin) — Naik level: Services, DTO, Middleware, Common

Sampai Fase 11, semua logic (cek email exist, hash password, generate token) numpuk di `AccountController`. Itu wajar buat belajar dasar dulu — tapi di project .NET yang lebih serius, biasanya dipisah per tanggung jawab. Ini folder yang **umum kepake**:

```
NetMvcAuth/
├── Controllers/     -> nerima HTTP request, panggil Service, return View/Redirect
├── Models/          -> Entity, representasi tabel DB (User, dll)
├── ViewModels/      -> bentuk data buat form/View Razor (yang udah kita bikin)
├── DTOs/            -> bentuk data buat API/antar layer (opsional, lihat penjelasan di bawah)
├── Services/        -> business logic (validasi, hashing, cek DB) — INI yang tadinya numpuk di Controller
├── Data/            -> AppDbContext
├── Middlewares/     -> custom pipeline component (logging, error handling custom, dll)
├── Common/          -> kelas utilitas kecil dipake lintas layer (TokenGenerator, ServiceResult, dll)
├── Views/
└── wwwroot/
```

### 12a. `Common/ServiceResult.cs` — bentuk hasil operasi yang seragam

**Kenapa:** biar Service bisa bilang "sukses/gagal + alasannya" tanpa throw exception buat kasus normal (kayak "email udah dipakai").

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

### 12b. `Common/TokenGenerator.cs` — utilitas generate token reset password

**Kenapa:** dipakai berkali-kali (sekarang cuma Forgot Password, nanti bisa dipakai buat email verification juga), jadi lebih pas ditaro di `Common` daripada nempel di Service. `RandomNumberGenerator` lebih cocok buat keperluan keamanan dibanding `Guid.NewGuid()`.

```csharp
using System.Security.Cryptography;

namespace NetMvcAuth.Common;

public static class TokenGenerator
{
    public static string CreateToken(int byteLength = 32) =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(byteLength));
}
```

### 12c. `Services/IAccountService.cs` & `AccountService.cs` — pindahin business logic

**Kenapa:** Controller jadi tipis (cuma urusan HTTP: model binding, redirect, cookie), gampang di-unit-test karena `AppDbContext` gak nyampur sama `HttpContext`/cookie logic.

```csharp
// Services/IAccountService.cs
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

```csharp
// Services/AccountService.cs
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

        user.PasswordResetToken = TokenGenerator.CreateToken();
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(30);
        await _db.SaveChangesAsync();

        var resetLink = buildResetLink(user.Email, user.PasswordResetToken);
        // TODO: kirim resetLink via email (lihat Fase 10)
    }

    public async Task<ServiceResult> ResetPasswordAsync(ResetPasswordViewModel model)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Email == model.Email &&
            u.PasswordResetToken == model.Token &&
            u.PasswordResetTokenExpiresAt > DateTime.UtcNow);

        if (user is null) return ServiceResult.Fail("Link reset tidak valid atau sudah kedaluwarsa.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiresAt = null;
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }
}
```

Daftarin ke DI container di `Program.cs` (dekat `AddDbContext`):
```csharp
builder.Services.AddScoped<IAccountService, Services.AccountService>();
```

**Controller sesudah refactor** (contoh method `Login`, pola yang sama berlaku ke Register/ForgotPassword/ResetPassword — Controller cuma pindahin data ViewModel ke Service, lalu urus HTTP-nya):
```csharp
private readonly IAccountService _accountService;

public AccountController(IAccountService accountService) // AppDbContext gak perlu lagi di sini
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
Perhatiin: `SignInAsync`/cookie tetep di Controller (itu emang urusan HTTP), yang pindah ke Service cuma "apakah kredensial ini valid".

### 12d. `DTOs/` — perlu gak buat project ini?

**Jawaban jujur: untuk sekarang, nggak wajib.** Di MVC dengan Razor View, `ViewModel` udah menjalankan peran DTO (bentuk data yang aman buat dikirim ke View). Istilah "DTO" biasanya muncul kalau lo punya **Web API** yang return JSON — di situ lo butuh mapping dari Entity ke DTO biar field sensitif (`PasswordHash`, `PasswordResetToken`) gak ikut ke-serialize ke response.

Contoh kalau nanti lo nambah endpoint API (`GET /api/account/me`):
```csharp
// DTOs/UserDto.cs
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
        // sengaja gak ikut PasswordHash / PasswordResetToken
    };
}
```
Kalau project ini tetep MVC murni (server-render Razor), `ViewModels/` udah cukup — gak perlu duplikasi jadi `DTOs/` juga.

### 12e. `Middlewares/` — custom pipeline component

**Kenapa dipakai:** `UseAuthentication`, `UseAuthorization`, `UseExceptionHandler` yang udah ada di `Program.cs` itu **middleware bawaan**. Custom middleware biasanya dipakai buat hal yang mau berlaku ke *semua* request tanpa nulis ulang di tiap Controller — misal logging.

```csharp
// Middlewares/RequestLoggingMiddleware.cs
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

        await _next(context); // lanjut ke middleware/endpoint berikutnya
    }
}
```
Daftarin di `Program.cs`, **setelah** `app.UseAuthentication()` (biar `context.User` udah keisi):
```csharp
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<Middlewares.RequestLoggingMiddleware>();
```
Urutan middleware itu penting — tiap middleware jalan berurutan kayak pipa, jadi kalau ditaro sebelum `UseAuthentication`, `context.User` masih kosong.

### Ringkasan kapan pakai apa
| Folder | Wajib buat project auth sederhana? | Kapan berguna |
|---|---|---|
| `Services/` | Disaranin | Begitu logic Controller mulai kepanjangan / mau di-unit-test |
| `Common/` | Opsional | Ada utilitas kecil dipake berkali-kali (token generator, result wrapper) |
| `Middlewares/` | Opsional | Ada perilaku yang harus berlaku ke semua request (logging, custom error page) |
| `DTOs/` | Gak perlu di MVC murni | Begitu ada endpoint yang return JSON (Web API) |

---

## Referensi konsep (kalau mau ngulik lebih dalam)
- Cookie Authentication: https://learn.microsoft.com/aspnet/core/security/authentication/cookie
- BCrypt.Net-Next: https://github.com/BcryptNet/bcrypt.net
- EF Core Migrations: https://learn.microsoft.com/ef/core/managing-schemas/migrations/
