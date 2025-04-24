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
                var room = new Room
                {
                    Id = reader.GetInt32("id"),
                    Title = reader.GetString("title"),
                    RoomCreator = reader.GetInt32("room_creator"),
                    MinBet = reader.GetFloat("min_bet"),
                    MaxBet = reader.GetFloat("max_bet"),
                    RoomStatus = reader.GetString("room_status"),
                    CreationDate = reader.GetDateTime("creation_date")
                };
                _logger.LogInformation($"Read room from database: Id={room.Id}, Title={room.Title}, MinBet={room.MinBet}, MaxBet={room.MaxBet}");
                rooms.Add(room);
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
            _logger.LogInformation($"Request data: Title={request.Title}, MinBet={request.MinBet}, MaxBet={request.MaxBet}, UserId={request.UserId}");

            // Check if user is logged in
            var sessionUserId = HttpContext.Session.GetInt32("UserId");
            _logger.LogInformation($"Session UserId: {sessionUserId}");
            
            if (!sessionUserId.HasValue)
            {
                _logger.LogWarning("User not logged in - no session found");
                return Unauthorized(new { message = "User not logged in" });
            }

            // Verify that the session user matches the request user
            if (sessionUserId.Value != request.UserId)
            {
                _logger.LogWarning($"Session user ({sessionUserId.Value}) does not match request user ({request.UserId})");
                return Unauthorized(new { message = "Session user does not match request user" });
            }

            // Verify user exists in database
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync();
            _logger.LogInformation("Database connection opened");
            
            var verifyUserCommand = new MySqlCommand(
                "SELECT COUNT(*) FROM Users WHERE id = @userId",
                connection);
            verifyUserCommand.Parameters.AddWithValue("@userId", request.UserId);
            _logger.LogInformation($"Executing query: SELECT COUNT(*) FROM Users WHERE id = {request.UserId}");
            
            var count = Convert.ToInt32(await verifyUserCommand.ExecuteScalarAsync());
            _logger.LogInformation($"Query result (count): {count}");
            var userExists = count > 0;
            _logger.LogInformation($"User exists in database: {userExists}");

            if (!userExists)
            {
                _logger.LogWarning($"User with ID {request.UserId} not found in database");
                return BadRequest(new { message = "User not found in database" });
            }

            // Create Rooms table if it doesn't exist
            var createTableCommand = new MySqlCommand(@"
                CREATE TABLE IF NOT EXISTS Rooms (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    title VARCHAR(100) DEFAULT NULL,
                    room_creator INT DEFAULT NULL,
                    min_bet FLOAT DEFAULT NULL,
                    max_bet FLOAT DEFAULT NULL,
                    room_status ENUM('active', 'inactive', 'hidden') DEFAULT 'active',
                    creation_date DATE DEFAULT NULL,
                    FOREIGN KEY (room_creator) REFERENCES Users(id)
                )", connection);
            
            await createTableCommand.ExecuteNonQueryAsync();
            _logger.LogInformation("Rooms table created or verified");

            // Create RoomParticipants table if it doesn't exist
            var createParticipantsTableCommand = new MySqlCommand(@"
                CREATE TABLE IF NOT EXISTS RoomParticipants (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    user_id INT DEFAULT NULL,
                    room_id INT DEFAULT NULL,
                    is_playing TINYINT(1) DEFAULT NULL,
                    is_creator TINYINT(1) DEFAULT NULL,
                    FOREIGN KEY (user_id) REFERENCES Users(id),
                    FOREIGN KEY (room_id) REFERENCES Rooms(id)
                )", connection);
            
            await createParticipantsTableCommand.ExecuteNonQueryAsync();
            _logger.LogInformation("RoomParticipants table created or verified");

            var command = new MySqlCommand(
                @"INSERT INTO Rooms (title, room_creator, min_bet, max_bet, room_status, creation_date) 
                  VALUES (@title, @roomCreator, @minBet, @maxBet, 'active', @creationDate);
                  SELECT LAST_INSERT_ID();",
                connection);

            command.Parameters.AddWithValue("@title", request.Title);
            command.Parameters.AddWithValue("@roomCreator", request.UserId);
            command.Parameters.AddWithValue("@minBet", request.MinBet);
            command.Parameters.AddWithValue("@maxBet", request.MaxBet);
            command.Parameters.AddWithValue("@creationDate", DateTime.Now.Date);

            _logger.LogInformation($"SQL Parameters: Title={request.Title}, MinBet={request.MinBet}, MaxBet={request.MaxBet}, UserId={request.UserId}");

            _logger.LogInformation("Executing room creation query...");
            var roomId = Convert.ToInt32(await command.ExecuteScalarAsync());
            _logger.LogInformation($"Room created with ID: {roomId}");

            // Add creator as participant
            var participantCommand = new MySqlCommand(
                @"INSERT INTO RoomParticipants (user_id, room_id, is_playing, is_creator) 
                  VALUES (@userId, @roomId, 0, 1)",
                connection);

            participantCommand.Parameters.AddWithValue("@userId", request.UserId);
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