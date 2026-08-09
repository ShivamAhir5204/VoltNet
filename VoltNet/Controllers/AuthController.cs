using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using VoltNet.Data;
using Microsoft.AspNetCore.Identity;
using VoltNet.Models;
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace VoltNet.Controllers;

public class AuthController : Controller
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;

    public AuthController(AppDbContext context, IConfiguration configuration, IMemoryCache cache, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _configuration = configuration;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        return View("~/Views/Auth/Login.cshtml");
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string emailOrPhone, string password, string remember)
    {
        bool isRememberMe = !string.IsNullOrEmpty(remember) && remember == "on";

        if (string.IsNullOrEmpty(emailOrPhone) || string.IsNullOrEmpty(password))
        {
            ViewBag.Error = "Please enter email or phone number and password.";
            return View("~/Views/Auth/Login.cshtml");
        }

        var user = await _context.UserMasters.FirstOrDefaultAsync(u => u.Email == emailOrPhone || u.Mobile == emailOrPhone);
        if (user == null || !user.Isactive)
        {
            ViewBag.Error = "Invalid credentials, or user is inactive.";
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

    [HttpGet("register")]
    public IActionResult Register()
    {
        return View("~/Views/Auth/Register.cshtml");
    }

    [HttpPost("register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string fullname, string email, string mobile, string password)
    {
        if (string.IsNullOrEmpty(fullname) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(mobile) || string.IsNullOrEmpty(password))
        {
            ViewBag.Error = "All fields are required.";
            return View("~/Views/Auth/Register.cshtml");
        }

        var existingUser = await _context.UserMasters.FirstOrDefaultAsync(u => u.Email == email || u.Mobile == mobile);
        if (existingUser != null)
        {
            ViewBag.Error = "Email or Phone number is already registered.";
            return View("~/Views/Auth/Register.cshtml");
        }

        // Generate 6-digit OTP
        var otp = new Random().Next(100000, 999999).ToString();

        // Store registration payload + OTP in memory cache
        var cacheData = new 
        { 
            FullName = fullname, 
            Email = email, 
            Mobile = mobile, 
            Password = password,
            Otp = otp
        };
        
        _cache.Set($"registration_{email}", cacheData, TimeSpan.FromMinutes(15));

        // Send OTP via API
        var client = _httpClientFactory.CreateClient();
        
        var formContent = new MultipartFormDataContent();
        formContent.Add(new StringContent(email), "to");
        formContent.Add(new StringContent("VoltNet Verification Code"), "subject");
        formContent.Add(new StringContent($"Your OTP for VoltNet registration is: {otp}"), "body");

        try
        {
            var response = await client.PostAsync("http://mailsendapi.runasp.net/api/Mailing/send", formContent);
            if (!response.IsSuccessStatusCode)
            {
                ViewBag.Error = "Failed to send OTP email. Please try again later.";
                return View("~/Views/Auth/Register.cshtml");
            }
        }
        catch (Exception ex)
        {
            ViewBag.Error = "Error communicating with mail server: " + ex.Message;
            return View("~/Views/Auth/Register.cshtml");
        }

        return Redirect($"/verify-otp?email={Uri.EscapeDataString(email)}");
    }

    [HttpGet("verify-otp")]
    public IActionResult VerifyOtp(string email)
    {
        if (string.IsNullOrEmpty(email)) return Redirect("/register");
        
        ViewBag.Email = email;
        return View("~/Views/Auth/VerifyOtp.cshtml");
    }

    [HttpPost("verify-otp")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(string email, string otp)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(otp))
        {
            ViewBag.Error = "Email and OTP are required.";
            ViewBag.Email = email;
            return View("~/Views/Auth/VerifyOtp.cshtml");
        }

        if (_cache.TryGetValue($"registration_{email}", out dynamic? cacheData))
        {
            if (cacheData?.Otp == otp)
            {
                // Create the user
                var user = new UserMaster
                {
                    Id = Guid.NewGuid(),
                    Fullname = cacheData.FullName,
                    Email = cacheData.Email,
                    Mobile = cacheData.Mobile,
                    Role = "Customer",
                    Isactive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var hasher = new PasswordHasher<UserMaster>();
                user.Password = hasher.HashPassword(user, cacheData.Password);

                _context.UserMasters.Add(user);
                await _context.SaveChangesAsync();

                _cache.Remove($"registration_{email}");

                return Redirect("/login");
            }
        }

        ViewBag.Error = "Invalid or expired OTP.";
        ViewBag.Email = email;
        return View("~/Views/Auth/VerifyOtp.cshtml");
    }
}
