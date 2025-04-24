using Microsoft.AspNetCore.Mvc;
using bottomsport_backend.Models;
using bottomsport_backend.Services;

namespace bottomsport_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly DatabaseService _databaseService;
    private readonly ILogger<UserController> _logger;

    public UserController(DatabaseService databaseService, ILogger<UserController> logger)
    {
        _databaseService = databaseService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> RegisterUser([FromBody] RegisterUserRequest request)
    {
        try
        {
            // Validate user data
            var isValid = await _databaseService.ValidateUserData(request.Username);
            if (!isValid)
            {
                return BadRequest(new { message = "Username already exists" });
            }

            // Add new user
            var user = await _databaseService.AddNewUser(request);

            return Ok(new { 
                message = "Registration successful",
                userId = user.Id,
                username = user.Username
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user registration");
            return StatusCode(500, new { message = "An error occurred during registration" });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            _logger.LogInformation($"Login attempt for username: {request.Username}");
            _logger.LogInformation($"Request data - Username: {request.Username}, Password length: {request.Password?.Length ?? 0}");
            
            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                _logger.LogWarning("Login attempt with empty username or password");
                return BadRequest(new { message = "Username and password are required" });
            }

            var user = await _databaseService.ValidateLogin(request.Username, request.Password);
            
            if (user == null)
            {
                _logger.LogWarning($"Failed login attempt for username: {request.Username}");
                return BadRequest(new { message = "Invalid username or password" });
            }

            _logger.LogInformation($"Successful login for username: {request.Username}, User ID: {user.Id}");
            
            // Clear any existing session
            HttpContext.Session.Clear();
            
            // Set session values
            _logger.LogInformation($"Setting session values for user {user.Id}");
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("Username", user.Username);

            // Verify session was set
            var sessionUserId = HttpContext.Session.GetInt32("UserId");
            var sessionUsername = HttpContext.Session.GetString("Username");
            _logger.LogInformation($"Verifying session - UserId: {sessionUserId}, Username: {sessionUsername}");
            _logger.LogInformation($"Session ID: {HttpContext.Session.Id}");

            // Log request headers
            _logger.LogInformation("Request Headers:");
            foreach (var header in HttpContext.Request.Headers)
            {
                _logger.LogInformation($"{header.Key}: {header.Value}");
            }

            // Log response headers
            _logger.LogInformation("Response Headers:");
            foreach (var header in HttpContext.Response.Headers)
            {
                _logger.LogInformation($"{header.Key}: {header.Value}");
            }

            // Force session cookie to be set
            HttpContext.Response.Cookies.Append(".BottomSport.Session", HttpContext.Session.Id, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = false,
                IsEssential = true
            });

            return Ok(new
            {
                id = user.Id,
                username = user.Username,
                role = user.Role,
                sessionId = HttpContext.Session.Id  // Include session ID in response for debugging
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error during login for username: {request.Username}");
            return StatusCode(500, new { message = "An error occurred during login", details = ex.Message });
        }
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        try
        {
            // Clear session
            HttpContext.Session.Clear();
            return Ok(new { message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, new { message = "An error occurred during logout" });
        }
    }
} 