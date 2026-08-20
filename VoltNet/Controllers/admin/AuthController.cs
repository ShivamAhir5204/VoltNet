using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using VoltNet.Data;
using Microsoft.AspNetCore.Identity;
using VoltNet.Models;
using Microsoft.EntityFrameworkCore;

namespace VoltNet.Controllers.admin;

public class AuthController : Controller
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpGet("admin/login")]
    public IActionResult Login()
    {
        if (User.Identity != null && User.Identity.IsAuthenticated && (User.IsInRole("SuperAdmin") || User.IsInRole("Admin")))
        {
            return Redirect("/admin/UserMaster");
        }
        return View("~/Views/admin/auth/Login.cshtml");
    }

    [HttpPost("admin/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, string remember)
    {
        bool isRememberMe = !string.IsNullOrEmpty(remember) && remember == "on";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ViewBag.Error = "Please enter your email and password.";
            return View("~/Views/admin/auth/Login.cshtml");
        }

        var trimmedEmail = email.Trim();
        var admin = await _context.AdminUsers
            .FirstOrDefaultAsync(u => u.Email == trimmedEmail);

        if (admin == null || !admin.Isactive)
        {
            ViewBag.Error = "Invalid credentials, or admin account is inactive.";
            return View("~/Views/admin/auth/Login.cshtml");
        }

        // Validate password
        var hasher = new PasswordHasher<AdminUser>();
        PasswordVerificationResult result;
        try
        {
            result = hasher.VerifyHashedPassword(admin, admin.Password, password);
        }
        catch (FormatException)
        {
            // Fallback if plain text was stored
            if (admin.Password == password)
            {
                admin.Password = hasher.HashPassword(admin, password);
                await _context.SaveChangesAsync();
                result = PasswordVerificationResult.Success;
            }
            else
            {
                result = PasswordVerificationResult.Failed;
            }
        }

        if (result == PasswordVerificationResult.Failed)
        {
            ViewBag.Error = "Invalid credentials.";
            return View("~/Views/admin/auth/Login.cshtml");
        }

        // Generate JWT
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = Encoding.ASCII.GetBytes(jwtSettings["Key"] ?? "SuperSecretKeyWhichMustBeAtLeast32BytesLongForSecurity123!");

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, admin.Email),
            new Claim(ClaimTypes.Name, admin.Name),
            new Claim(ClaimTypes.Role, admin.Role),
            new Claim("AdminId", admin.Id.ToString())
        };

        var expires = isRememberMe ? DateTime.UtcNow.AddDays(7) : DateTime.UtcNow.AddHours(8);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
        };

        if (isRememberMe)
        {
            cookieOptions.Expires = expires;
        }

        Response.Cookies.Append("AuthToken", tokenString, cookieOptions);

        return Redirect("/admin/UserMaster");
    }

    [HttpPost("admin/logout")]
    [ValidateAntiForgeryToken]
    public IActionResult LogoutPost()
    {
        Response.Cookies.Delete("AuthToken");
        return Redirect("/admin/login");
    }

    [HttpGet("admin/logout")]
    public IActionResult LogoutGet()
    {
        Response.Cookies.Delete("AuthToken");
        return Redirect("/admin/login");
    }
}
