using MySql.Data.MySqlClient;
using bottomsport_backend.Models;
using Microsoft.Extensions.Logging;

namespace bottomsport_backend.Services;

public class GameService
{
    private readonly DatabaseService _databaseService;
    private readonly ILogger<GameService> _logger;
    private readonly Random _random = new Random();

    public GameService(DatabaseService databaseService, ILogger<GameService> logger)
    {
        _databaseService = databaseService;
        _logger = logger;
    }

    public async Task<Game> StartGame(int userId, decimal betAmount, string difficulty)
    {
        _logger.LogInformation($"Starting game for user {userId} with bet {betAmount} and difficulty {difficulty}");
        
        try
        {
            // Get user balance
            decimal userBalance = await GetUserBalance(userId);
            
            // Check if user has sufficient balance
            if (userBalance < betAmount)
            {
                _logger.LogWarning($"User {userId} has insufficient balance: {userBalance} < {betAmount}");
                throw new Exception("Insufficient balance");
            }

            // Get difficulty settings
            var settings = DifficultySettings.GetSettings();
            if (!settings.ContainsKey(difficulty))
            {
                _logger.LogWarning($"Invalid difficulty: {difficulty}");
                throw new Exception("Invalid difficulty");
            }
                
            var difficultySettings = settings[difficulty];
            
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync();

            // Start a transaction to ensure all operations are atomic
            MySqlTransaction transaction = await connection.BeginTransactionAsync();
            
            try
            {
                // Deduct bet amount from user's balance
                var updateBalanceCommand = new MySqlCommand(
                    @"UPDATE users 
                      SET balance = balance - @betAmount 
                      WHERE id = @userId",
                    connection, transaction);
                
                updateBalanceCommand.Parameters.AddWithValue("@betAmount", betAmount);
                updateBalanceCommand.Parameters.AddWithValue("@userId", userId);
                
                int rowsAffected = await updateBalanceCommand.ExecuteNonQueryAsync();
                
                if (rowsAffected == 0)
                {
                    _logger.LogError($"Failed to update balance for user {userId}");
                    throw new Exception("Failed to update user balance");
                }
                
                _logger.LogInformation($"Deducted {betAmount} from user {userId}'s balance");
                
                // Get updated balance after deduction
                decimal balanceAfter = await GetUserBalanceWithTransaction(userId, connection, transaction);
                _logger.LogInformation($"New balance for user {userId}: {balanceAfter}");
                
                // Create new game record
                var game = new Game
                {
                    UserId = userId,
                    BetAmount = betAmount,
                    Difficulty = difficulty,
                    StartingMultiplier = difficultySettings.StartingMultiplier,
                    FinalMultiplier = difficultySettings.StartingMultiplier,
                    LanesCompleted = 0,
                    IsComplete = false,
                    IsWon = false,
                    Payout = 0,
                    StartTime = DateTime.UtcNow
                };
                
                // Convert difficulty to lowercase to match database enum values
                string dbDifficulty = difficulty.ToLower();
                
                // Insert game into database - using missioncrossablegames table (lowercase)
                var insertGameCommand = new MySqlCommand(
                    @"INSERT INTO missioncrossablegames (difficulty, bet_amount, prize_multiplier, 
                      user_id, lanes_completed, is_complete, is_won, payout, start_time)
                      VALUES (@difficulty, @betAmount, @prizeMultiplier, 
                      @userId, @lanesCompleted, @isComplete, @isWon, @payout, @startTime);
                      SELECT LAST_INSERT_ID();",
                    connection, transaction);
                
                insertGameCommand.Parameters.AddWithValue("@userId", game.UserId);
                insertGameCommand.Parameters.AddWithValue("@difficulty", dbDifficulty);
                insertGameCommand.Parameters.AddWithValue("@betAmount", (float)game.BetAmount);
                insertGameCommand.Parameters.AddWithValue("@prizeMultiplier", (float)game.StartingMultiplier);
                insertGameCommand.Parameters.AddWithValue("@lanesCompleted", game.LanesCompleted);
                insertGameCommand.Parameters.AddWithValue("@isComplete", game.IsComplete);
                insertGameCommand.Parameters.AddWithValue("@isWon", game.IsWon);
                insertGameCommand.Parameters.AddWithValue("@payout", (float)game.Payout);
                insertGameCommand.Parameters.AddWithValue("@startTime", game.StartTime);
                
                // Execute and get the newly created game ID
                game.Id = Convert.ToInt32(await insertGameCommand.ExecuteScalarAsync());
                
                // First get the game number by counting existing games for this user
                var gameNumCommand = new MySqlCommand(
                    @"SELECT COUNT(*) FROM mcstats WHERE user_id = @userId",
                    connection, transaction);
                
                gameNumCommand.Parameters.AddWithValue("@userId", game.UserId);
                
                int gameNum = Convert.ToInt32(await gameNumCommand.ExecuteScalarAsync()) + 1;
                
                // Create MCStats record with the retrieved game number
                var mcStatsCommand = new MySqlCommand(
                    @"INSERT INTO mcstats (game_num, game_date, steps_done, bet_amount, winnings, result, user_id, mission_game_id)
                      VALUES (@gameNum, @gameDate, 0, @betAmount, 0, 'loss', @userId, @gameId)",
                    connection, transaction);
                
                mcStatsCommand.Parameters.AddWithValue("@gameNum", gameNum);
                mcStatsCommand.Parameters.AddWithValue("@userId", game.UserId);
                mcStatsCommand.Parameters.AddWithValue("@gameDate", DateTime.UtcNow.Date);
                mcStatsCommand.Parameters.AddWithValue("@betAmount", (float)game.BetAmount);
                mcStatsCommand.Parameters.AddWithValue("@gameId", game.Id);
                
                await mcStatsCommand.ExecuteNonQueryAsync();
                
                // Commit the transaction
                await transaction.CommitAsync();
                
                _logger.LogInformation($"Game started successfully: GameId={game.Id}");
                return game;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during game start transaction, rolling back");
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error starting game for user {userId}");
            throw;
        }
    }

    public async Task<GameMoveResult> MakeMove(int gameId)
    {
        _logger.LogInformation($"Making move for game {gameId}");
        
        try
        {
            // Get game from database
            var game = await GetGameById(gameId);
            
            if (game == null)
            {
                _logger.LogWarning($"Game not found: {gameId}");
                throw new Exception("Game not found");
            }
            
            if (game.IsComplete)
            {
                _logger.LogWarning($"Game already completed: {gameId}");
                throw new Exception("Game already completed");
            }
                
            var settings = DifficultySettings.GetSettings()[game.Difficulty];
            
            // Check if collision occurs based on difficulty probability
            bool collisionOccurred = _random.NextDouble() < settings.CollisionProbability;
            _logger.LogInformation($"Collision check: {collisionOccurred} (Probability: {settings.CollisionProbability})");
            
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync();
            
            if (collisionOccurred)
            {
                // Game over - player lost
                game.IsComplete = true;
                game.IsWon = false;
                game.Payout = 0;
                game.EndTime = DateTime.UtcNow;
                
                // Update game in database
                var updateCommand = new MySqlCommand(
                    @"UPDATE missioncrossablegames 
                      SET is_complete = @isComplete,
                          is_won = @isWon,
                          payout = @payout,
                          end_time = @endTime
                      WHERE id = @gameId",
                    connection);
                
                updateCommand.Parameters.AddWithValue("@isComplete", game.IsComplete);
                updateCommand.Parameters.AddWithValue("@isWon", game.IsWon);
                updateCommand.Parameters.AddWithValue("@payout", (float)game.Payout);
                updateCommand.Parameters.AddWithValue("@endTime", game.EndTime);
                updateCommand.Parameters.AddWithValue("@gameId", game.Id);
                
                await updateCommand.ExecuteNonQueryAsync();
                
                // Update MCStats
                var updateStatsCommand = new MySqlCommand(
                    @"UPDATE mcstats 
                      SET steps_done = @stepsCompleted,
                          result = 'loss'
                      WHERE mission_game_id = @gameId",
                    connection);
                
                updateStatsCommand.Parameters.AddWithValue("@stepsCompleted", game.LanesCompleted);
                updateStatsCommand.Parameters.AddWithValue("@gameId", game.Id);
                
                await updateStatsCommand.ExecuteNonQueryAsync();
                
                _logger.LogInformation($"Game ended with collision: GameId={gameId}");
                
                return new GameMoveResult 
                { 
                    Success = false, 
                    CollisionOccurred = true,
                    CurrentMultiplier = game.FinalMultiplier,
                    LanesCompleted = game.LanesCompleted,
                    GameOver = true
                };
            }
            else
            {
                // Successfully crossed lane, increase multiplier
                game.LanesCompleted++;
                
                // Calculate new multiplier based on difficulty and lanes crossed
                decimal multiplierIncrease = CalculateMultiplierIncrease(game.Difficulty, game.LanesCompleted);
                game.FinalMultiplier += multiplierIncrease;
                
                // Update game in database
                var updateCommand = new MySqlCommand(
                    @"UPDATE missioncrossablegames 
                      SET lanes_completed = @lanesCompleted,
                          prize_multiplier = @prizeMultiplier
                      WHERE id = @gameId",
                    connection);
                
                updateCommand.Parameters.AddWithValue("@lanesCompleted", game.LanesCompleted);
                updateCommand.Parameters.AddWithValue("@prizeMultiplier", (float)game.FinalMultiplier);
                updateCommand.Parameters.AddWithValue("@gameId", game.Id);
                
                await updateCommand.ExecuteNonQueryAsync();
                
                // Update MCStats steps_done
                var updateStatsCommand = new MySqlCommand(
                    @"UPDATE mcstats 
                      SET steps_done = @stepsCompleted
                      WHERE mission_game_id = @gameId",
                    connection);
                
                updateStatsCommand.Parameters.AddWithValue("@stepsCompleted", game.LanesCompleted);
                updateStatsCommand.Parameters.AddWithValue("@gameId", game.Id);
                
                await updateStatsCommand.ExecuteNonQueryAsync();
                
                _logger.LogInformation($"Move successful: GameId={gameId}, Lanes={game.LanesCompleted}, Multiplier={game.FinalMultiplier}");
                
                return new GameMoveResult 
                { 
                    Success = true, 
                    CollisionOccurred = false,
                    CurrentMultiplier = game.FinalMultiplier,
                    LanesCompleted = game.LanesCompleted,
                    GameOver = false
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error making move for game {gameId}");
            throw;
        }
    }

    public async Task<CashoutResult> Cashout(int gameId)
    {
        _logger.LogInformation($"Cashing out game {gameId}");
        
        try
        {
            // Get game from database
            var game = await GetGameById(gameId);
            
            if (game == null)
            {
                _logger.LogWarning($"Game not found: {gameId}");
                throw new Exception("Game not found");
            }
            
            if (game.IsComplete)
            {
                _logger.LogWarning($"Game already completed: {gameId}");
                throw new Exception("Game already completed");
            }
            
            // Calculate winnings based on lanes completed
            decimal winnings;
            
            // Check if player has made any moves
            if (game.LanesCompleted > 0)
            {
                // Apply multiplier only if player has moved forward
                winnings = game.BetAmount * game.FinalMultiplier;
                _logger.LogInformation($"Game {gameId} - Calculated winnings with multiplier: {winnings} (Bet: {game.BetAmount} × Multiplier: {game.FinalMultiplier})");
            }
            else
            {
                // Return only the original bet amount if no moves were made
                winnings = game.BetAmount;
                _logger.LogInformation($"Game {gameId} - No lanes completed, returning original bet amount: {winnings}");
            }
            
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync();
            
            // Start a transaction to ensure all operations are atomic
            MySqlTransaction transaction = await connection.BeginTransactionAsync();
            
            try
            {
                // Update user balance
                var updateBalanceCommand = new MySqlCommand(
                    @"UPDATE users 
                      SET balance = balance + @winnings 
                      WHERE id = @userId",
                    connection, transaction);
                
                updateBalanceCommand.Parameters.AddWithValue("@winnings", (float)winnings);
                updateBalanceCommand.Parameters.AddWithValue("@userId", game.UserId);
                
                int rowsAffected = await updateBalanceCommand.ExecuteNonQueryAsync();
                
                if (rowsAffected == 0)
                {
                    _logger.LogError($"Failed to update balance for user {game.UserId}");
                    throw new Exception("Failed to update user balance");
                }
                
                _logger.LogInformation($"Added {winnings} to user {game.UserId}'s balance");
                
                // Get updated balance
                decimal newBalance = await GetUserBalanceWithTransaction(game.UserId, connection, transaction);
                _logger.LogInformation($"New balance for user {game.UserId}: {newBalance}");
                
                // Update game record
                game.IsComplete = true;
                game.IsWon = true;
                game.Payout = winnings;
                game.EndTime = DateTime.UtcNow;
                
                // Update game in database
                var updateGameCommand = new MySqlCommand(
                    @"UPDATE missioncrossablegames 
                      SET is_complete = @isComplete,
                          is_won = @isWon,
                          payout = @payout,
                          end_time = @endTime
                      WHERE id = @gameId",
                    connection, transaction);
                
                updateGameCommand.Parameters.AddWithValue("@isComplete", game.IsComplete);
                updateGameCommand.Parameters.AddWithValue("@isWon", game.IsWon);
                updateGameCommand.Parameters.AddWithValue("@payout", (float)game.Payout);
                updateGameCommand.Parameters.AddWithValue("@endTime", game.EndTime);
                updateGameCommand.Parameters.AddWithValue("@gameId", game.Id);
                
                await updateGameCommand.ExecuteNonQueryAsync();
                
                // Update MCStats record
                var updateStatsCommand = new MySqlCommand(
                    @"UPDATE mcstats 
                      SET steps_done = @stepsCompleted,
                          winnings = @winnings,
                          result = 'win'
                      WHERE mission_game_id = @gameId",
                    connection, transaction);
                
                updateStatsCommand.Parameters.AddWithValue("@stepsCompleted", game.LanesCompleted);
                updateStatsCommand.Parameters.AddWithValue("@winnings", (float)winnings);
                updateStatsCommand.Parameters.AddWithValue("@gameId", game.Id);
                
                await updateStatsCommand.ExecuteNonQueryAsync();
                
                // Commit the transaction
                await transaction.CommitAsync();
                
                _logger.LogInformation($"Cashout successful: GameId={gameId}, Winnings={winnings}, New Balance={newBalance}");
                
                return new CashoutResult
                {
                    Success = true,
                    Winnings = winnings,
                    FinalMultiplier = game.FinalMultiplier,
                    NewBalance = newBalance
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cashout transaction, rolling back");
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error cashing out game {gameId}");
            throw;
        }
    }
    
    private decimal CalculateMultiplierIncrease(string difficulty, int lanesCompleted)
    {
        // Multiplier increase formula based on difficulty
        switch (difficulty)
        {
            case "Easy":
                return 0.10m;
            case "Medium":
                return 0.15m;
            case "Hard":
                return 0.20m;
            case "Daredevil":
                return 0.30m;
            default:
                return 0.10m;
        }
    }
    
    private async Task<Game?> GetGameById(int gameId)
    {
        using var connection = _databaseService.CreateConnection();
        await connection.OpenAsync();
        
        var command = new MySqlCommand(
            @"SELECT * FROM missioncrossablegames WHERE id = @gameId",
            connection);
        
        command.Parameters.AddWithValue("@gameId", gameId);
        
        using var reader = await command.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            // Map database field names to Game properties and convert lowercase enum values to capitalized
            string dbDifficulty = reader.GetString(reader.GetOrdinal("difficulty"));
            string capitalizedDifficulty = char.ToUpper(dbDifficulty[0]) + dbDifficulty.Substring(1);
            
            return new Game
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                UserId = reader.GetInt32(reader.GetOrdinal("user_id")),
                BetAmount = Convert.ToDecimal(reader.GetFloat(reader.GetOrdinal("bet_amount"))),
                Difficulty = capitalizedDifficulty,
                StartingMultiplier = Convert.ToDecimal(reader.GetFloat(reader.GetOrdinal("prize_multiplier"))),
                FinalMultiplier = Convert.ToDecimal(reader.GetFloat(reader.GetOrdinal("prize_multiplier"))),
                LanesCompleted = reader.GetInt32(reader.GetOrdinal("lanes_completed")),
                IsComplete = reader.GetBoolean(reader.GetOrdinal("is_complete")),
                IsWon = reader.GetBoolean(reader.GetOrdinal("is_won")),
                Payout = Convert.ToDecimal(reader.GetFloat(reader.GetOrdinal("payout"))),
                StartTime = reader.GetDateTime(reader.GetOrdinal("start_time")),
                EndTime = reader.IsDBNull(reader.GetOrdinal("end_time")) ? null : reader.GetDateTime(reader.GetOrdinal("end_time"))
            };
        }
        
        return null;
    }
    
    public async Task<decimal> GetUserBalance(int userId)
    {
        using var connection = _databaseService.CreateConnection();
        await connection.OpenAsync();
        
        var command = new MySqlCommand(
            "SELECT balance FROM users WHERE id = @userId",
            connection);
        
        command.Parameters.AddWithValue("@userId", userId);
        
        var result = await command.ExecuteScalarAsync();
        return result != DBNull.Value ? Convert.ToDecimal(result) : 0;
    }
    
    private async Task UpdateUserBalance(int userId, decimal amount)
    {
        using var connection = _databaseService.CreateConnection();
        await connection.OpenAsync();
        
        var command = new MySqlCommand(
            @"UPDATE users 
              SET balance = balance + @amount 
              WHERE id = @userId",
            connection);
        
        command.Parameters.AddWithValue("@amount", amount);
        command.Parameters.AddWithValue("@userId", userId);
        
        await command.ExecuteNonQueryAsync();
    }
    
    public async Task EnsureTablesExist()
    {
        using var connection = _databaseService.CreateConnection();
        await connection.OpenAsync();
        
        // Check if missioncrossablegames table exists and create it if it doesn't
        var createMissionCrossableGamesCommand = new MySqlCommand(@"
            CREATE TABLE IF NOT EXISTS missioncrossablegames (
                id INT AUTO_INCREMENT PRIMARY KEY,
                difficulty ENUM('easy', 'medium', 'hard', 'daredevil') DEFAULT NULL,
                bet_amount FLOAT DEFAULT NULL,
                prize_multiplier FLOAT DEFAULT NULL,
                user_id INT DEFAULT NULL,
                lanes_completed INT NOT NULL DEFAULT 0,
                is_complete TINYINT(1) NOT NULL DEFAULT 0,
                is_won TINYINT(1) NOT NULL DEFAULT 0,
                payout FLOAT NOT NULL DEFAULT 0,
                start_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                end_time DATETIME DEFAULT NULL,
                FOREIGN KEY (user_id) REFERENCES users(id)
            )", connection);
        
        await createMissionCrossableGamesCommand.ExecuteNonQueryAsync();
        
        // Check if mcstats table exists and create it if it doesn't
        var createMCStatsCommand = new MySqlCommand(@"
            CREATE TABLE IF NOT EXISTS mcstats (
                id INT AUTO_INCREMENT PRIMARY KEY,
                game_num INT DEFAULT NULL,
                game_date DATE DEFAULT NULL,
                steps_done INT DEFAULT NULL,
                bet_amount FLOAT DEFAULT NULL,
                winnings FLOAT DEFAULT NULL,
                result ENUM('win', 'loss') DEFAULT NULL,
                mission_game_id INT DEFAULT NULL,
                user_id INT DEFAULT NULL,
                FOREIGN KEY (user_id) REFERENCES users(id),
                FOREIGN KEY (mission_game_id) REFERENCES missioncrossablegames(id)
            )", connection);
        
        await createMCStatsCommand.ExecuteNonQueryAsync();
        
        _logger.LogInformation("Mission Crossable game tables created successfully");
    }

    // Helper method to get user balance within a transaction
    private async Task<decimal> GetUserBalanceWithTransaction(int userId, MySqlConnection connection, MySqlTransaction transaction)
    {
        var command = new MySqlCommand(
            "SELECT balance FROM users WHERE id = @userId",
            connection, transaction);
        
        command.Parameters.AddWithValue("@userId", userId);
        
        var result = await command.ExecuteScalarAsync();
        return result != DBNull.Value ? Convert.ToDecimal(result) : 0;
    }

    // Add this method to retrieve leaderboard data
    public async Task<List<LeaderboardEntry>> GetLeaderboard()
    {
        _logger.LogInformation("Fetching Mission Crossable leaderboard data");
        
        var result = new List<LeaderboardEntry>();
        
        try
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync();
            
            // Join missioncrossablegames with users to get usernames
            // Only include games that were completed (is_complete = true) and won (is_won = true)
            // Order by winnings (payout) in descending order
            var command = new MySqlCommand(
                @"SELECT u.username, g.bet_amount, g.payout as win_amount, g.prize_multiplier as max_multiplier 
                  FROM missioncrossablegames g
                  JOIN users u ON g.user_id = u.id
                  WHERE g.is_complete = true AND g.is_won = true
                  ORDER BY g.payout DESC
                  LIMIT 10",
                connection);
            
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                // Get column ordinals first
                int usernameOrdinal = reader.GetOrdinal("username");
                int betAmountOrdinal = reader.GetOrdinal("bet_amount");
                int winAmountOrdinal = reader.GetOrdinal("win_amount");
                int maxMultiplierOrdinal = reader.GetOrdinal("max_multiplier");
                
                result.Add(new LeaderboardEntry
                {
                    Username = reader.GetString(usernameOrdinal),
                    BetAmount = Convert.ToDecimal(reader.GetFloat(betAmountOrdinal)),
                    WinAmount = Convert.ToDecimal(reader.GetFloat(winAmountOrdinal)),
                    MaxMultiplier = Convert.ToDecimal(reader.GetFloat(maxMultiplierOrdinal))
                });
            }
            
            _logger.LogInformation($"Retrieved {result.Count} leaderboard entries");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving leaderboard data");
            throw;
        }
    }

    // Add the GetUserStats method to retrieve game statistics for a user
    public async Task<UserGameStats> GetUserStats(int userId)
    {
        _logger.LogInformation($"Retrieving game statistics for user {userId}");
        
        var stats = new UserGameStats();
        
        try
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync();
            
            // Get game statistics summary
            var summaryCommand = new MySqlCommand(
                @"SELECT 
                    COUNT(*) as total_games,
                    SUM(CASE WHEN is_won = 1 THEN 1 ELSE 0 END) as wins,
                    SUM(CASE WHEN is_won = 0 AND is_complete = 1 THEN 1 ELSE 0 END) as losses,
                    SUM(bet_amount) as total_bet,
                    MAX(bet_amount) as biggest_bet,
                    MAX(CASE WHEN is_won = 1 THEN payout ELSE 0 END) as biggest_win,
                    MIN(CASE WHEN is_won = 1 AND payout > 0 THEN payout ELSE NULL END) as smallest_win,
                    AVG(prize_multiplier) as avg_multiplier
                  FROM missioncrossablegames
                  WHERE user_id = @userId",
                connection);
            
            summaryCommand.Parameters.AddWithValue("@userId", userId);
            
            using var reader = await summaryCommand.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                // Get column ordinals first
                int totalGamesOrdinal = reader.GetOrdinal("total_games");
                int winsOrdinal = reader.GetOrdinal("wins");
                int lossesOrdinal = reader.GetOrdinal("losses");
                int totalBetOrdinal = reader.GetOrdinal("total_bet");
                int biggestBetOrdinal = reader.GetOrdinal("biggest_bet");
                int biggestWinOrdinal = reader.GetOrdinal("biggest_win");
                int smallestWinOrdinal = reader.GetOrdinal("smallest_win");
                int avgMultiplierOrdinal = reader.GetOrdinal("avg_multiplier");
                
                stats.TotalGames = reader.GetInt32(totalGamesOrdinal);
                stats.Wins = reader.GetInt32(winsOrdinal);
                stats.Losses = reader.GetInt32(lossesOrdinal);
                stats.TotalBet = Convert.ToDecimal(reader.GetFloat(totalBetOrdinal));
                stats.BiggestBet = Convert.ToDecimal(reader.GetFloat(biggestBetOrdinal));
                
                if (!reader.IsDBNull(biggestWinOrdinal))
                    stats.BiggestWin = Convert.ToDecimal(reader.GetFloat(biggestWinOrdinal));
                
                if (!reader.IsDBNull(smallestWinOrdinal))
                    stats.SmallestWin = Convert.ToDecimal(reader.GetFloat(smallestWinOrdinal));
                
                if (!reader.IsDBNull(avgMultiplierOrdinal))
                    stats.AverageMultiplier = Convert.ToDecimal(reader.GetFloat(avgMultiplierOrdinal));
                
                // Calculate win rate
                stats.WinRate = stats.TotalGames > 0 ? (decimal)stats.Wins / stats.TotalGames : 0;
            }
            
            reader.Close();
            
            // Get game history
            var historyCommand = new MySqlCommand(
                @"SELECT 
                    m.game_num,
                    m.game_date,
                    g.bet_amount,
                    m.winnings,
                    g.prize_multiplier,
                    m.result
                  FROM mcstats m
                  JOIN missioncrossablegames g ON m.mission_game_id = g.id
                  WHERE g.user_id = @userId
                  ORDER BY m.game_date DESC, m.game_num DESC
                  LIMIT 20",
                connection);
            
            historyCommand.Parameters.AddWithValue("@userId", userId);
            
            using var historyReader = await historyCommand.ExecuteReaderAsync();
            
            while (await historyReader.ReadAsync())
            {
                // Get column ordinals first
                int gameNumOrdinal = historyReader.GetOrdinal("game_num");
                int gameDateOrdinal = historyReader.GetOrdinal("game_date");
                int betAmountOrdinal = historyReader.GetOrdinal("bet_amount");
                int winningsOrdinal = historyReader.GetOrdinal("winnings");
                int prizeMultiplierOrdinal = historyReader.GetOrdinal("prize_multiplier");
                int resultOrdinal = historyReader.GetOrdinal("result");
                
                var gameItem = new GameHistoryItem
                {
                    GameNum = historyReader.GetInt32(gameNumOrdinal),
                    Date = historyReader.GetDateTime(gameDateOrdinal).ToString("yyyy-MM-dd"),
                    BetAmount = Convert.ToDecimal(historyReader.GetFloat(betAmountOrdinal)),
                    Multiplier = Convert.ToDecimal(historyReader.GetFloat(prizeMultiplierOrdinal)),
                    Result = historyReader.GetString(resultOrdinal)
                };
                
                if (!historyReader.IsDBNull(winningsOrdinal))
                    gameItem.WinAmount = Convert.ToDecimal(historyReader.GetFloat(winningsOrdinal));
                
                stats.GameHistory.Add(gameItem);
            }
            
            _logger.LogInformation($"Retrieved statistics for user {userId}: {stats.TotalGames} games, {stats.Wins} wins");
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving game statistics for user {userId}");
            throw;
        }
    }
} 