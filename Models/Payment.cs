using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace bottomsport_backend.Models;

public class CreatePaymentIntentRequest
{
    [Required]
    [JsonPropertyName("amount")]
    public float Amount { get; set; }

    [Required]
    [JsonPropertyName("userId")]
    public int UserId { get; set; }
}

public class ConfirmPaymentRequest
{
    [Required]
    [JsonPropertyName("paymentIntentId")]
    public string PaymentIntentId { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("userId")]
    public int UserId { get; set; }
} 