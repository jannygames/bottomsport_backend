using Microsoft.AspNetCore.Mvc;
using bottomsport_backend.Models;
using bottomsport_backend.Services;
using System.Data;
using MySql.Data.MySqlClient;

namespace bottomsport_backend.Controllers;

[ApiController]
[Route("api/Room")]
public class RoomController : ControllerBase
{
    private readonly DatabaseService _databaseService;
    private readonly ILogger<RoomController> _logger;

    public RoomController(DatabaseService databaseService, ILogger<RoomController> logger)
    {
        _databaseService = databaseService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetRooms()
    {
        try
        {
            _logger.LogInformation("Attempting to fetch rooms...");
            using var connection = _databaseService.CreateConnection();
            _logger.LogInformation("Connection created, opening...");
            await connection.OpenAsync();
            _logger.LogInformation("Connection opened successfully");

            var command = new MySqlCommand(
                @"SELECT id, title, room_creator, min_bet, max_bet, room_status, creation_date 
                  FROM Rooms 
                  WHERE room_status = 'active'",
                connection);

            var rooms = new List<Room>();
            using var reader = await command.ExecuteReaderAsync();
            _logger.LogInformation("Executed query, reading results...");
            
            while (await reader.ReadAsync())
            {
                rooms.Add(new Room
                {
                    Id = reader.GetInt32("id"),
                    Title = reader.GetString("title"),
                    RoomCreator = reader.GetInt32("room_creator"),
                    MinBet = reader.GetFloat("min_bet"),
                    MaxBet = reader.GetFloat("max_bet"),
                    RoomStatus = reader.GetString("room_status"),
                    CreationDate = reader.GetDateTime("creation_date")
                });
            }
            _logger.LogInformation($"Found {rooms.Count} rooms");

            return Ok(rooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching rooms");
            return StatusCode(500, new { message = "An error occurred while fetching rooms" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request)
    {
        try
        {
            _logger.LogInformation("Attempting to create room...");
            _logger.LogInformation($"Request data: Title={request.Title}, MinBet={request.MinBet}, MaxBet={request.MaxBet}");

            // Log request headers
            _logger.LogInformation("Request Headers:");
            foreach (var header in HttpContext.Request.Headers)
            {
                _logger.LogInformation($"{header.Key}: {header.Value}");
            }

            // Log cookies
            _logger.LogInformation("Request Cookies:");
            foreach (var cookie in HttpContext.Request.Cookies)
            {
                _logger.LogInformation($"{cookie.Key}: {cookie.Value}");
            }

            // Log session state
            _logger.LogInformation($"Session ID: {HttpContext.Session.Id}");
            _logger.LogInformation("Session Keys:");
            foreach (var key in HttpContext.Session.Keys)
            {
                _logger.LogInformation($"Key: {key}");
                if (key == "UserId")
                {
                    var bytes = HttpContext.Session.Get(key);
                    _logger.LogInformation($"UserId bytes length: {bytes?.Length ?? 0}");
                    if (bytes != null && bytes.Length >= 4)
                    {
                        var value = BitConverter.ToInt32(bytes);
                        _logger.LogInformation($"UserId value: {value}");
                    }
                }
            }

            if (!HttpContext.Session.TryGetValue("UserId", out var userIdBytes))
            {
                _logger.LogWarning("User not logged in - no UserId found in session");
                return Unauthorized(new { message = "User not logged in" });
            }

            var userId = BitConverter.ToInt32(userIdBytes);
            _logger.LogInformation($"User ID from session: {userId}");

            // Verify user exists in database
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync();
            
            var verifyUserCommand = new MySqlCommand(
                "SELECT COUNT(*) FROM Users WHERE id = @userId",
                connection);
            verifyUserCommand.Parameters.AddWithValue("@userId", userId);
            
            var userExists = Convert.ToInt32(await verifyUserCommand.ExecuteScalarAsync()) > 0;
            _logger.LogInformation($"User exists in database: {userExists}");

            if (!userExists)
            {
                _logger.LogWarning($"User with ID {userId} not found in database");
                return BadRequest(new { message = "User not found in database" });
            }

            var command = new MySqlCommand(
                @"INSERT INTO Rooms (title, room_creator, min_bet, max_bet, room_status, creation_date) 
                  VALUES (@title, @roomCreator, @minBet, @maxBet, 'active', @creationDate);
                  SELECT LAST_INSERT_ID();",
                connection);

            command.Parameters.AddWithValue("@title", request.Title);
            command.Parameters.AddWithValue("@roomCreator", userId);
            command.Parameters.AddWithValue("@minBet", request.MinBet);
            command.Parameters.AddWithValue("@maxBet", request.MaxBet);
            command.Parameters.AddWithValue("@creationDate", DateTime.Now.Date);

            _logger.LogInformation("Executing room creation query...");
            var roomId = Convert.ToInt32(await command.ExecuteScalarAsync());
            _logger.LogInformation($"Room created with ID: {roomId}");

            // Add creator as participant
            var participantCommand = new MySqlCommand(
                @"INSERT INTO RoomParticipants (user_id, room_id, is_playing, is_creator) 
                  VALUES (@userId, @roomId, false, true)",
                connection);

            participantCommand.Parameters.AddWithValue("@userId", userId);
            participantCommand.Parameters.AddWithValue("@roomId", roomId);
            
            _logger.LogInformation("Adding creator as participant...");
            await participantCommand.ExecuteNonQueryAsync();
            _logger.LogInformation("Creator added as participant successfully");

            return Ok(new { 
                message = "Room created successfully",
                roomId = roomId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating room");
            return StatusCode(500, new { message = "An error occurred while creating the room", details = ex.Message });
        }
    }
} 