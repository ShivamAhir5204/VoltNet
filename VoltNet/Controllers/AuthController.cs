using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using VoltNet.Data;
using Microsoft.AspNetCore.Identity;
using VoltNet.Models;
using Microsoft.EntityFrameworkCore;

namespace VoltNet.Controllers;

public class AuthController : Controller
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        return View("~/Views/Auth/Login.cshtml");
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, string remember)
    {
        bool isRememberMe = !string.IsNullOrEmpty(remember) && remember == "on";

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ViewBag.Error = "Please enter email and password.";
            return View("~/Views/Auth/Login.cshtml");
        }

        var user = await _context.UserMasters.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null || !user.Isactive)
        {
            ViewBag.Error = "Invalid email or password, or user is inactive.";
            return View("~/Views/Auth/Login.cshtml");
        }

        var hasher = new PasswordHasher<UserMaster>();
        PasswordVerificationResult result;
        try
        {
            result = hasher.VerifyHashedPassword(user, user.Password, password);
        }
        catch (FormatException)
        {
            // Handle plain text passwords (from previous creations)
            if (user.Password == password)
            {
                // Re-hash and save
                user.Password = hasher.HashPassword(user, password);
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
            ViewBag.Error = "Invalid email or password.";
            return View("~/Views/Auth/Login.cshtml");
        }

        // Generate JWT
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = Encoding.ASCII.GetBytes(jwtSettings["Key"] ?? "SuperSecretKeyWhichMustBeAtLeast32BytesLongForSecurity123!");

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("UserId", user.Id.ToString())
        };

        var expires = isRememberMe ? DateTime.UtcNow.AddDays(7) : DateTime.UtcNow.AddMinutes(120);

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

        // Set HttpOnly Cookie
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // Requires HTTPS
            SameSite = SameSiteMode.Strict,
        };

        if (isRememberMe)
        {
            cookieOptions.Expires = expires;
        }

        Response.Cookies.Append("AuthToken", tokenString, cookieOptions);

        if (user.Role == "SuperAdmin")
        {
            return Redirect("/admin/UserMaster");
        }
        
        return RedirectToAction("Index", "Home");
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("AuthToken");
        return Redirect("/login");
    }
}
