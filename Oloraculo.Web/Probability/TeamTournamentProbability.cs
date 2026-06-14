namespace Oloraculo.Web.Probability
{
    public class TeamTournamentProbability
    {
        public string TeamId { get; set; }
        public string Group { get; set; }
        public double WinGroup { get; set; }
        public double Qualify { get; set; }
        public double ReachRoundOf16 { get; set; }
        public double ReachQuarterFinal { get; set; }
        public double ReachSemiFinal { get; set; }
        public double ReachFinal { get; set; }
        public double WinTournament { get; set; }
        public double ExpectedGroupPoints { get; set; }


    }
}
