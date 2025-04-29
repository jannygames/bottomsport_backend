using Microsoft.AspNetCore.Mvc;
using bottomsport_backend.Models;
using bottomsport_backend.Services;
using MySql.Data.MySqlClient;
using System.Data;

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
        using var connection = _databaseService.CreateConnection();
        await connection.OpenAsync();

        var command = new MySqlCommand(@"SELECT * FROM rooms WHERE room_status = 'active'", connection);
        var rooms = new List<Room>();

        using var reader = await command.ExecuteReaderAsync();
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

        return Ok(rooms);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request)
    {
        using var connection = _databaseService.CreateConnection();
        await connection.OpenAsync();

        var command = new MySqlCommand(@"
            INSERT INTO rooms (title, room_creator, min_bet, max_bet, room_status, creation_date)
            VALUES (@title, @creator, @minBet, @maxBet, 'active', @date);
            SELECT LAST_INSERT_ID();
        ", connection);

        command.Parameters.AddWithValue("@title", request.Title);
        command.Parameters.AddWithValue("@creator", request.UserId);
        command.Parameters.AddWithValue("@minBet", request.MinBet);
        command.Parameters.AddWithValue("@maxBet", request.MaxBet);
        command.Parameters.AddWithValue("@date", DateTime.Now.Date);

        var id = Convert.ToInt32(await command.ExecuteScalarAsync());
        return Ok(new { message = "Room created successfully", roomId = id });
    }

    [HttpPost("validate")]
    public async Task<IActionResult> ValidateRoomCode([FromBody] ValidateRoomCodeRequest request)
    {
        using var connection = _databaseService.CreateConnection();
        await connection.OpenAsync();

        var command = new MySqlCommand("SELECT COUNT(*) FROM rooms WHERE id = @roomId", connection);
        command.Parameters.AddWithValue("@roomId", request.RoomId);

        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        bool isValid = count > 0;

        return Ok(new { isValid });
    }

    [HttpGet("inactive")]
    public async Task<IActionResult> GetRoomsInactiveMoreThanOneHour()
    {
        using var connection = _databaseService.CreateConnection();
        await connection.OpenAsync();

        var command = new MySqlCommand(@"
            SELECT * FROM rooms
            WHERE room_status = 'active'
              AND TIMESTAMPDIFF(MINUTE, creation_date, NOW()) > 60
        ", connection);

        var rooms = new List<Room>();
        using var reader = await command.ExecuteReaderAsync();
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

        return Ok(rooms);
    }

    [HttpPost("hide")]
    public async Task<IActionResult> HideInactiveRooms([FromBody] List<int> roomIds)
    {
        using var connection = _databaseService.CreateConnection();
        await connection.OpenAsync();

        foreach (var id in roomIds)
        {
            var command = new MySqlCommand("UPDATE rooms SET room_status = 'hidden' WHERE id = @id", connection);
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync();
        }

        return Ok(new { message = "Rooms hidden successfully" });
    }

    [HttpGet("creator/{roomId}")]
    public async Task<IActionResult> GetRoomCreator(int roomId)
    {
        using var connection = _databaseService.CreateConnection();
        await connection.OpenAsync();

        var command = new MySqlCommand("SELECT room_creator FROM rooms WHERE id = @id", connection);
        command.Parameters.AddWithValue("@id", roomId);

        var creatorId = (int?)await command.ExecuteScalarAsync();
        if (creatorId == null)
            return NotFound(new { message = "Room not found" });

        return Ok(new { creatorId });
    }

    [HttpPut("update/{roomId}")]
    public async Task<IActionResult> UpdateRoomSettings(int roomId, [FromBody] UpdateRoomSettingsRequest request)
    {
        using var connection = _databaseService.CreateConnection();
        await connection.OpenAsync();

        var command = new MySqlCommand(@"
            UPDATE rooms SET 
                title = @title,
                min_bet = @minBet,
                max_bet = @maxBet
            WHERE id = @id
        ", connection);

        command.Parameters.AddWithValue("@title", request.Title);
        command.Parameters.AddWithValue("@minBet", request.MinBet);
        command.Parameters.AddWithValue("@maxBet", request.MaxBet);
        command.Parameters.AddWithValue("@id", roomId);

        await command.ExecuteNonQueryAsync();
        return Ok(new { message = "Room settings updated successfully" });
    }

    [HttpDelete("{roomId}")]
    public async Task<IActionResult> DeleteRoom(int roomId)
    {
        using var connection = _databaseService.CreateConnection();
        await connection.OpenAsync();

        var command = new MySqlCommand("DELETE FROM rooms WHERE id = @id", connection);
        command.Parameters.AddWithValue("@id", roomId);
        var rowsAffected = await command.ExecuteNonQueryAsync();

        if (rowsAffected == 0)
            return NotFound(new { message = "Room not found" });

        return Ok(new { message = "Room deleted successfully" });
    }
}
