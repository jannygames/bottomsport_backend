using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace bottomsport_backend.Models;

public class Room
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty;
    
    public int RoomCreator { get; set; }
    
    [Required]
    public float MinBet { get; set; }
    
    [Required]
    public float MaxBet { get; set; }
    
    [Required]
    public string RoomStatus { get; set; } = "active";
    
    public DateTime CreationDate { get; set; } = DateTime.Now;
}

public class CreateRoomRequest
{
    [Required]
    [StringLength(100)]
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    [JsonPropertyName("min_bet")]
    public float MinBet { get; set; }
    
    [Required]
    [JsonPropertyName("max_bet")]
    public float MaxBet { get; set; }
} 