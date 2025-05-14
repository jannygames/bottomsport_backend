using Microsoft.AspNetCore.Mvc;
using Stripe;
using bottomsport_backend.Services;
using MySql.Data.MySqlClient;

namespace bottomsport_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly DatabaseService _databaseService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        DatabaseService databaseService,
        IConfiguration configuration,
        ILogger<PaymentController> logger)
    {
        _databaseService = databaseService;
        _configuration = configuration;
        _logger = logger;
        StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
    }

    [HttpPost("create-intent")]
    public async Task<IActionResult> CreatePaymentIntent([FromBody] CreatePaymentIntentRequest request)
    {
        try
        {
            _logger.LogInformation($"Creating payment intent with amount: {request.Amount} cents for user: {request.UserId}");
            
            // Create payment intent with automatic payment methods enabled
            var options = new PaymentIntentCreateOptions
            {
                Amount = request.Amount, // Amount in cents (e.g. 500 for $5.00)
                Currency = "usd",
                // IMPORTANT: This enables automatic payment methods
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true
                },
                // Do NOT specify PaymentMethodTypes when using automatic payment methods
                Metadata = new Dictionary<string, string>
                {
                    { "userId", request.UserId.ToString() }
                }
            };

            var service = new PaymentIntentService();
            var intent = await service.CreateAsync(options);
            
            _logger.LogInformation($"Created payment intent: {intent.Id} with amount: {intent.Amount} cents");
            _logger.LogInformation($"Automatic payment methods enabled: {intent.AutomaticPaymentMethods?.Enabled}");

            return Ok(new { clientSecret = intent.ClientSecret });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payment intent");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPaymentRequest request)
    {
        try
        {
            _logger.LogInformation($"Confirming payment: {request.PaymentIntentId}");
            
            var service = new PaymentIntentService();
            var intent = await service.GetAsync(request.PaymentIntentId);
            
            _logger.LogInformation($"Payment status: {intent.Status}, amount: {intent.Amount} cents, metadata: {string.Join(", ", intent.Metadata.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");

            if (intent.Status == "succeeded")
            {
                int userId = int.Parse(intent.Metadata["userId"]);
                decimal amount = intent.Amount / 100m; // Convert cents to dollars

                _logger.LogInformation($"Updating balance for user {userId} with amount ${amount}");

                using var connection = _databaseService.CreateConnection();
                await connection.OpenAsync();

                // Fix: Use COALESCE to handle NULL values in the balance column
                var command = new MySqlCommand(
                    "UPDATE Users SET balance = COALESCE(balance, 0) + @amount WHERE id = @userId",
                    connection);

                command.Parameters.AddWithValue("@amount", amount);
                command.Parameters.AddWithValue("@userId", userId);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                _logger.LogInformation($"Rows affected by balance update: {rowsAffected}");

                // Get updated balance
                command = new MySqlCommand(
                    "SELECT COALESCE(balance, 0) FROM Users WHERE id = @userId",
                    connection);

                command.Parameters.AddWithValue("@userId", userId);
                var newBalance = Convert.ToDecimal(await command.ExecuteScalarAsync());
                
                _logger.LogInformation($"New balance for user {userId}: ${newBalance}");

                // One-time fix: If balance is still NULL for any reason, set it to the amount
                if (rowsAffected == 0 || newBalance == 0)
                {
                    command = new MySqlCommand(
                        "UPDATE Users SET balance = @amount WHERE id = @userId AND (balance IS NULL OR balance = 0)",
                        connection);

                    command.Parameters.AddWithValue("@amount", amount);
                    command.Parameters.AddWithValue("@userId", userId);

                    var fixedRows = await command.ExecuteNonQueryAsync();
                    _logger.LogInformation($"Fixed NULL balances: {fixedRows} rows");
                    
                    if (fixedRows > 0)
                    {
                        // Re-fetch balance if we fixed it
                        command = new MySqlCommand(
                            "SELECT COALESCE(balance, 0) FROM Users WHERE id = @userId",
                            connection);

                        command.Parameters.AddWithValue("@userId", userId);
                        newBalance = Convert.ToDecimal(await command.ExecuteScalarAsync());
                        _logger.LogInformation($"Fixed balance for user {userId}: ${newBalance}");
                    }
                }

                // IMPORTANT: Always use the success=true format for consistency
                return Ok(new { success = true, balance = newBalance, message = "Payment confirmed and balance updated" });
            }

            _logger.LogWarning($"Payment not successful: {intent.Status}");
            return BadRequest(new { success = false, error = "Payment not successful" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming payment");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpGet("balance/{userId}")]
    public async Task<IActionResult> GetBalance(int userId)
    {
        try
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync();

            var command = new MySqlCommand(
                "SELECT COALESCE(balance, 0) FROM Users WHERE id = @userId",
                connection);

            command.Parameters.AddWithValue("@userId", userId);
            var balance = await command.ExecuteScalarAsync();

            if (balance == null)
                return NotFound(new { error = "User not found" });

            return Ok(new { balance = Convert.ToDecimal(balance) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting balance");
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class CreatePaymentIntentRequest
{
    public int UserId { get; set; }
    public int Amount { get; set; } // Amount in cents
}

public class ConfirmPaymentRequest
{
    public string PaymentIntentId { get; set; }
} 