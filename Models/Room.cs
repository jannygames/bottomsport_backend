using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace bottomsport_backend.Models;

public class Room
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    
    [JsonPropertyName("room_creator")]
    public int RoomCreator { get; set; }
    
    [Required]
    [JsonPropertyName("min_bet")]
    public float MinBet { get; set; }
    
    [Required]
    [JsonPropertyName("max_bet")]
    public float MaxBet { get; set; }
    
    [JsonPropertyName("room_status")]
    public string RoomStatus { get; set; } = "active";  // ENUM('active', 'inactive', 'hidden')
    
    [JsonPropertyName("creation_date")]
    public DateTime CreationDate { get; set; } = DateTime.Now.Date;
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

    [Required]
    [JsonPropertyName("UserId")]
    public int UserId { get; set; }
} 