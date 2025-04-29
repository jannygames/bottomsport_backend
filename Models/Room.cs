namespace bottomsport_backend.Models;

public class Room
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int RoomCreator { get; set; }
    public double MinBet { get; set; }
    public double MaxBet { get; set; }
    public string RoomStatus { get; set; } = "active"; // 'active', 'inactive', 'hidden'
    public DateTime CreationDate { get; set; } = DateTime.Now.Date;
}

// Request for creating a new room
public class CreateRoomRequest
{
    public string Title { get; set; } = string.Empty;
    public double MinBet { get; set; }
    public double MaxBet { get; set; }
    public int UserId { get; set; }
}

// Request for validating room code
public class ValidateRoomCodeRequest
{
    public int RoomId { get; set; }
}

// Request for updating room settings
public class UpdateRoomSettingsRequest
{
    public string Title { get; set; } = string.Empty;
    public double MinBet { get; set; }
    public double MaxBet { get; set; }
}
