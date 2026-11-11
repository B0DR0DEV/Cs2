namespace StrikeZone.Game
{
    /// <summary>Accuracy counters for the training range HUD.</summary>
    public sealed class TrainingStats
    {
        public int Shots;
        public int Hits;
        public int Headshots;
        public int Kills;
        public int Damage;

        public float Accuracy
        {
            get { return Shots > 0 ? Hits / (float)Shots : 0f; }
        }

        public float HeadshotRate
        {
            get { return Hits > 0 ? Headshots / (float)Hits : 0f; }
        }

        public void Reset()
        {
            Shots = 0;
            Hits = 0;
            Headshots = 0;
            Kills = 0;
            Damage = 0;
        }
    }
}
