namespace Oloraculo.Web.Probability
{
    public readonly record struct OutcomeProbabilities(double HomeWin, double Draw, double AwayWin)
    {
        public static OutcomeProbabilities Uniform => new(1.0 / 3.0, 1.0 / 3.0, 1.0 / 3.0);
        public double Total => HomeWin + Draw + AwayWin;
        public string TopPick => HomeWin >= Draw && HomeWin >= AwayWin ? "Home"
            : Draw >= HomeWin && Draw >= AwayWin ? "Draw" : "Away";
        public bool IsValid =>
            HomeWin >= 0 &&
            Draw >= 0 &&
            AwayWin >= 0 &&
            Total > 0 &&
            !double.IsNaN(Total) &&
            !double.IsInfinity(Total);

        public OutcomeProbabilities Normalize()
        {
            var total = Total;
            if (total <= 0 || double.IsNaN(total) || double.IsInfinity(total))
            {
                return Uniform;
            }

            return new OutcomeProbabilities(HomeWin / total, Draw / total, AwayWin / total);
        }
    }
}
