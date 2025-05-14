using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace bottomsport_backend.Models;

public class Game
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal BetAmount { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public decimal StartingMultiplier { get; set; }
    public int LanesCompleted { get; set; }
    public decimal FinalMultiplier { get; set; }
    public bool IsComplete { get; set; }
    public bool IsWon { get; set; }
    public decimal Payout { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

public class StartGameRequest
{
    [Required]
    [JsonPropertyName("userId")]
    public int UserId { get; set; }

    [Required]
    [JsonPropertyName("betAmount")]
    public decimal BetAmount { get; set; }

    [Required]
    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = string.Empty;
}

public class GameMoveRequest
{
    [Required]
    [JsonPropertyName("gameId")]
    public int GameId { get; set; }
}

public class CashoutRequest
{
    [Required]
    [JsonPropertyName("gameId")]
    public int GameId { get; set; }
}

public class GameMoveResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("collisionOccurred")]
    public bool CollisionOccurred { get; set; }

    [JsonPropertyName("currentMultiplier")]
    public decimal CurrentMultiplier { get; set; }

    [JsonPropertyName("lanesCompleted")]
    public int LanesCompleted { get; set; }

    [JsonPropertyName("gameOver")]
    public bool GameOver { get; set; }
}

public class CashoutResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("winnings")]
    public decimal Winnings { get; set; }

    [JsonPropertyName("finalMultiplier")]
    public decimal FinalMultiplier { get; set; }

    [JsonPropertyName("newBalance")]
    public decimal NewBalance { get; set; }
}

public class DifficultySettings
{
    public string Name { get; set; } = string.Empty;
    public double CollisionProbability { get; set; }
    public decimal StartingMultiplier { get; set; }
    
    public static Dictionary<string, DifficultySettings> GetSettings()
    {
        return new Dictionary<string, DifficultySettings>
        {
            { "Easy", new DifficultySettings { 
                Name = "Easy", 
                CollisionProbability = 1.0/25.0, 
                StartingMultiplier = 1.00m 
            }},
            { "Medium", new DifficultySettings { 
                Name = "Medium", 
                CollisionProbability = 3.0/25.0, 
                StartingMultiplier = 1.09m 
            }},
            { "Hard", new DifficultySettings { 
                Name = "Hard", 
                CollisionProbability = 5.0/25.0, 
                StartingMultiplier = 1.20m 
            }},
            { "Daredevil", new DifficultySettings { 
                Name = "Daredevil", 
                CollisionProbability = 10.0/25.0, 
                StartingMultiplier = 1.60m 
            }}
        };
    }
} 