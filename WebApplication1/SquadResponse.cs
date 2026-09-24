namespace WebApplication1.Models
{
    public class SquadResponse
    {
        public int MatchId { get; set; }
        public List<TeamSquad> Teams { get; set; } = new();
    }

    public class TeamSquad
    {
        public int TeamId { get; set; }
        public string TeamName { get; set; } = "";
        public string TeamSName { get; set; } = "";
        public List<Player> Players { get; set; } = new();
    }

    public class Player
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string FullName { get; set; } = "";
        public string NickName { get; set; } = "";
        public bool Captain { get; set; }
        public string Role { get; set; } = "";
        public bool Keeper { get; set; }
        public string BattingStyle { get; set; } = "";
        public string BowlingStyle { get; set; } = "";
        public string ProfileUrl { get; set; } = "";
    }
}