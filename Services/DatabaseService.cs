using MySql.Data.MySqlClient;
using System.Security.Cryptography;
using System.Text;
using bottomsport_backend.Models;
using Microsoft.Extensions.Logging;

namespace bottomsport_backend.Services;

public class DatabaseService
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(IConfiguration configuration, ILogger<DatabaseService> logger)
    {
        _connectionString = configuration.GetConnectionString("bottomsport") 
            ?? throw new InvalidOperationException("Connection string 'bottomsport' not found.");
        _logger = logger;
    }

    public async Task<bool> ValidateUserData(string username)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new MySqlCommand(
            "SELECT COUNT(*) FROM Users WHERE username = @username",
            connection);
        command.Parameters.AddWithValue("@username", username);

        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        return count == 0; // Returns true if username is available
    }

    public async Task<User> AddNewUser(RegisterUserRequest request)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        var passwordHash = HashPassword(request.Password);

        var command = new MySqlCommand(
            @"INSERT INTO Users (username, password_hash, role, balance, registration_date) 
              VALUES (@username, @passwordHash, 'user', 0, @registrationDate);
              SELECT LAST_INSERT_ID();",
            connection);

        command.Parameters.AddWithValue("@username", request.Username);
        command.Parameters.AddWithValue("@passwordHash", passwordHash);
        command.Parameters.AddWithValue("@registrationDate", DateTime.Now.Date);

        var userId = Convert.ToInt32(await command.ExecuteScalarAsync());

        return new User
        {
            Id = userId,
            Username = request.Username,
            PasswordHash = passwordHash,
            Role = "user",
            Balance = 0,
            RegistrationDate = DateTime.Now.Date
        };
    }

    private string HashPassword(string password)
    {
        /*using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        var hash = BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
        Console.WriteLine($"Password: {password}");
        Console.WriteLine($"Hash: {hash}");*/
        return password;
    }

    public async Task<User?> ValidateLogin(string username, string password)
    {
        try
        {
            Console.WriteLine($"Attempting to connect to database with connection string: {_connectionString}");
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            Console.WriteLine("Database connection successful");

            var passwordHash = HashPassword(password);
            Console.WriteLine($"Database validation - Username: {username}");
            Console.WriteLine($"Generated Hash: {passwordHash}");

            var command = new MySqlCommand(
                @"SELECT id, username, role, balance, registration_date 
                  FROM Users 
                  WHERE username = @username AND password_hash = @passwordHash",
                connection);

            command.Parameters.AddWithValue("@username", username);
            command.Parameters.AddWithValue("@passwordHash", passwordHash);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                Console.WriteLine("Login successful");
                return new User
                {
                    Id = Convert.ToInt32(reader["id"]),
                    Username = reader["username"].ToString() ?? string.Empty,
                    Role = reader["role"].ToString() ?? string.Empty,
                    Balance = reader["balance"] != DBNull.Value ? Convert.ToSingle(reader["balance"]) : null,
                    RegistrationDate = Convert.ToDateTime(reader["registration_date"])
                };
            }

            Console.WriteLine("Login failed - Invalid credentials");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database error during login: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    public MySqlConnection CreateConnection()
    {
        return new MySqlConnection(_connectionString);
    }

    public async Task<float> GetUserBalance(int userId)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var command = new MySqlCommand(
                "SELECT balance FROM Users WHERE id = @userId",
                connection);
            command.Parameters.AddWithValue("@userId", userId);

            var result = await command.ExecuteScalarAsync();
            return result == DBNull.Value ? 0 : Convert.ToSingle(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting balance for user {userId}");
            throw;
        }
    }

    public async Task UpdateUserBalance(int userId, float amount)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            var command = new MySqlCommand(
                @"UPDATE Users 
                  SET balance = balance + @amount 
                  WHERE id = @userId",
                connection);
            
            command.Parameters.AddWithValue("@amount", amount);
            command.Parameters.AddWithValue("@userId", userId);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating balance for user {userId}");
            throw;
        }
    }
} 