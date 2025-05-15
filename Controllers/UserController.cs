using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using bottomsport_backend.Models;
using bottomsport_backend.Services;

namespace bottomsport_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly DatabaseService _databaseService;
    private readonly GameService _gameService;
    private readonly ILogger<UserController> _logger;

    public UserController(
        DatabaseService databaseService, 
        GameService gameService, 
        ILogger<UserController> logger)
    {
        _databaseService = databaseService;
        _gameService = gameService;
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
                SameSite = HttpContext.Request.Host.Host == "localhost" ? SameSiteMode.Lax : SameSiteMode.None,
                Secure = HttpContext.Request.Host.Host != "localhost",
                IsEssential = true,
                Path = "/",
                Expires = DateTimeOffset.Now.AddDays(7)
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

    [HttpPost("cashout")]
    public async Task<IActionResult> CashoutFunds([FromBody] FundsCashoutRequest request)
    {
        try
        {
            if (request.Amount <= 0)
            {
                return BadRequest(new { error = "Amount must be greater than zero." });
            }

            // Get current user balance
            decimal currentBalance = await _gameService.GetUserBalance(request.UserId);
            
            // Check if user has sufficient balance
            if (currentBalance < request.Amount)
            {
                return BadRequest(new { error = $"Insufficient balance. Your current balance is ${currentBalance}." });
            }

            // Perform cashout by updating user balance
            await ProcessCashout(request.UserId, request.Amount);
            
            // Get new balance after cashout
            decimal newBalance = await _gameService.GetUserBalance(request.UserId);
            
            return Ok(new { 
                success = true, 
                message = "Cashout successful", 
                amount = request.Amount,
                newBalance = newBalance
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing cashout for user {request.UserId}");
            return BadRequest(new { error = "An error occurred while processing your cashout." });
        }
    }

    private async Task ProcessCashout(int userId, decimal amount)
    {
        using var connection = _databaseService.CreateConnection();
        await connection.OpenAsync();
        
        // Start a transaction
        using var transaction = await connection.BeginTransactionAsync();
        
        try
        {
            // Create command to update user balance
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE users SET balance = balance - @amount WHERE id = @userId";
            
            // Add parameters
            var amountParam = command.CreateParameter();
            amountParam.ParameterName = "@amount";
            amountParam.Value = amount;
            command.Parameters.Add(amountParam);
            
            var userIdParam = command.CreateParameter();
            userIdParam.ParameterName = "@userId";
            userIdParam.Value = userId;
            command.Parameters.Add(userIdParam);
            
            // Execute command
            int rowsAffected = await command.ExecuteNonQueryAsync();
            
            if (rowsAffected == 0)
            {
                throw new Exception($"Failed to update balance for user {userId}.");
            }
            
            // Commit transaction
            await transaction.CommitAsync();
            
            _logger.LogInformation($"Cashout successful: User {userId}, Amount ${amount}");
        }
        catch (Exception)
        {
            // Rollback transaction on failure
            await transaction.RollbackAsync();
            throw;
        }
    }
}

public class FundsCashoutRequest
{
    [Required]
    public int UserId { get; set; }
    
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }
} 