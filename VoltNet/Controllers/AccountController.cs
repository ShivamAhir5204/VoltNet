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

namespace VoltNet.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;

    public AccountController(AppDbContext context, IConfiguration configuration, IMemoryCache cache, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _configuration = configuration;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            if (User.IsInRole("StationOwner"))
                return Redirect("/owner/dashboard");
            if (User.IsInRole("StationManager"))
                return Redirect("/manager/dashboard");
            if (User.IsInRole("Admin") || User.IsInRole("SuperAdmin"))
                return Redirect("/admin/Dashboard");

            return Redirect("/customer/dashboard");
        }
        return View("~/Views/Auth/Login.cshtml");
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string emailOrPhone, string password, string remember)
    {
        bool isRememberMe = !string.IsNullOrEmpty(remember) && remember == "on";

        if (string.IsNullOrWhiteSpace(emailOrPhone) || string.IsNullOrWhiteSpace(password))
        {
            ViewBag.Error = "Please enter email or phone number and password.";
            return View("~/Views/Auth/Login.cshtml");
        }

        var trimmedInput = emailOrPhone.Trim();
        var user = await _context.UserMasters
            .FirstOrDefaultAsync(u => u.Email == trimmedInput || u.Mobile == trimmedInput);

        if (user == null || !user.Isactive)
        {
            ViewBag.Error = "Invalid credentials, or account is inactive.";
            return View("~/Views/Auth/Login.cshtml");
        }

        var hasher = new PasswordHasher<UserMaster>();
        PasswordVerificationResult result;
        try
        {
            result = hasher.VerifyHashedPassword(user, user.Password ?? "", password);
        }
        catch (FormatException)
        {
            if (user.Password == password)
            {
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
            ViewBag.Error = "Invalid email/phone or password.";
            return View("~/Views/Auth/Login.cshtml");
        }

        // Generate JWT
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = Encoding.ASCII.GetBytes(jwtSettings["Key"] ?? "SuperSecretKeyWhichMustBeAtLeast32BytesLongForSecurity123!");

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Fullname ?? user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("UserId", user.Id.ToString())
        };

        // Add role-specific profile ID claims for ownership scoping
        if (user.Role == "StationOwner")
        {
            var owner = await _context.StationOwners.FirstOrDefaultAsync(o => o.UserId == user.Id);
            if (owner != null)
                claims.Add(new Claim("StationOwnerId", owner.Id.ToString()));
        }
        else if (user.Role == "StationManager")
        {
            var manager = await _context.StationManagers.FirstOrDefaultAsync(m => m.UserId == user.Id);
            if (manager != null)
            {
                claims.Add(new Claim("StationManagerId", manager.Id.ToString()));
                claims.Add(new Claim("StationId", manager.StationId.ToString()));
            }
        }

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

        // Role-based redirect
        if (user.Role == "StationOwner")
            return Redirect("/owner/dashboard");
        if (user.Role == "StationManager")
            return Redirect("/manager/dashboard");

        // Default: Customer goes to Customer Dashboard
        return Redirect("/customer/dashboard");
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public IActionResult LogoutPost()
    {
        Response.Cookies.Delete("AuthToken");
        return Redirect("/login");
    }

    [HttpGet("logout")]
    public IActionResult LogoutGet()
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
        if (string.IsNullOrWhiteSpace(fullname) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(mobile) || string.IsNullOrWhiteSpace(password))
        {
            ViewBag.Error = "All fields are required.";
            return View("~/Views/Auth/Register.cshtml");
        }

        var trimmedEmail = email.Trim();
        var trimmedMobile = mobile.Trim();

        var existingUser = await _context.UserMasters
            .FirstOrDefaultAsync(u => u.Email == trimmedEmail || u.Mobile == trimmedMobile);
        if (existingUser != null)
        {
            ViewBag.Error = "Email or Phone number is already registered.";
            return View("~/Views/Auth/Register.cshtml");
        }

        // Generate 6-digit OTP
        var otp = new Random().Next(100000, 999999).ToString();

        // Store registration payload + OTP in memory cache for 15 minutes
        var cacheData = new
        {
            FullName = fullname.Trim(),
            Email = trimmedEmail,
            Mobile = trimmedMobile,
            Password = password,
            Otp = otp
        };

        _cache.Set($"registration_{trimmedEmail}", cacheData, TimeSpan.FromMinutes(15));

        // Send OTP via API
        var client = _httpClientFactory.CreateClient();
        var apiKey = _configuration["MailApi:ApiKey"];
        if (!string.IsNullOrEmpty(apiKey))
        {
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        }

        var formContent = new MultipartFormDataContent();
        formContent.Add(new StringContent(trimmedEmail), "to");
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

        return Redirect($"/verify-otp?email={Uri.EscapeDataString(trimmedEmail)}");
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
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp))
        {
            ViewBag.Error = "Email and OTP are required.";
            ViewBag.Email = email;
            return View("~/Views/Auth/VerifyOtp.cshtml");
        }

        var trimmedEmail = email.Trim();
        var trimmedOtp = otp.Trim();

        if (_cache.TryGetValue($"registration_{trimmedEmail}", out dynamic? cacheData))
        {
            if (cacheData?.Otp == trimmedOtp)
            {
                // Create the user in UserMaster
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

                _cache.Remove($"registration_{trimmedEmail}");

                return Redirect("/login");
            }
        }

        ViewBag.Error = "Invalid or expired OTP.";
        ViewBag.Email = trimmedEmail;
        return View("~/Views/Auth/VerifyOtp.cshtml");
    }
}
