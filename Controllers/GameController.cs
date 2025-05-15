using Microsoft.AspNetCore.Mvc;
using bottomsport_backend.Models;
using bottomsport_backend.Services;

namespace bottomsport_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GameController : ControllerBase
{
    private readonly GameService _gameService;
    private readonly ILogger<GameController> _logger;

    public GameController(GameService gameService, ILogger<GameController> logger)
    {
        _gameService = gameService;
        _logger = logger;
    }

    [HttpGet("difficulty")]
    public IActionResult GetDifficultySettings()
    {
        try
        {
            var settings = DifficultySettings.GetSettings();
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting difficulty settings");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartGame([FromBody] StartGameRequest request)
    {
        try
        {
            // Ensure required tables exist
            await _gameService.EnsureTablesExist();
            
            var game = await _gameService.StartGame(
                request.UserId, 
                request.BetAmount, 
                request.Difficulty);
            
            // Get the updated balance after starting the game
            var currentBalance = await _gameService.GetUserBalance(request.UserId);
                
            return Ok(new { 
                gameId = game.Id, 
                startingMultiplier = game.StartingMultiplier,
                currentBalance = currentBalance,
                message = "Game started successfully" 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting game");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("move")]
    public async Task<IActionResult> MakeMove([FromBody] GameMoveRequest request)
    {
        try
        {
            var result = await _gameService.MakeMove(request.GameId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error making move");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("cashout")]
    public async Task<IActionResult> Cashout([FromBody] CashoutRequest request)
    {
        try
        {
            var result = await _gameService.Cashout(request.GameId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cashing out");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("balance/{userId}")]
    public async Task<IActionResult> GetUserBalance(int userId)
    {
        try
        {
            // Use the GameService's GetUserBalance method to get the balance
            var balance = await _gameService.GetUserBalance(userId);
            return Ok(new { balance });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting balance for user {userId}");
            return BadRequest(new { error = ex.Message });
        }
    }
    
    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetLeaderboard()
    {
        try
        {
            _logger.LogInformation("Leaderboard endpoint called");
            var leaderboardData = await _gameService.GetLeaderboard();
            return Ok(leaderboardData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving leaderboard data");
            return BadRequest(new { error = ex.Message });
        }
    }
    
    // Test endpoint to verify routing
    [HttpGet("leaderboard-test")]
    public IActionResult LeaderboardTest()
    {
        return Ok(new { message = "Leaderboard test endpoint working!" });
    }

    [HttpGet("stats/{userId}")]
    public async Task<IActionResult> GetUserStats(int userId)
    {
        try
        {
            _logger.LogInformation($"Stats endpoint called for user {userId}");
            var userStats = await _gameService.GetUserStats(userId);
            return Ok(userStats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving stats for user {userId}");
            return BadRequest(new { error = ex.Message });
        }
    }
} 