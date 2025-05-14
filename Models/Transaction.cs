using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace bottomsport_backend.Models;

public class Transaction
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? GameId { get; set; }
    public string Type { get; set; } = string.Empty; // "Bet", "Win", "Deposit"
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public DateTime Timestamp { get; set; }
} 