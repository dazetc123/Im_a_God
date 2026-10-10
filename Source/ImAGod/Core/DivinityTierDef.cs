using Verse;

namespace ImAGod
{
    /// <summary>
    /// A rank of divinity. The god holds the highest tier whose follower and lifetime-faith
    /// requirements are both met.
    /// </summary>
    public class DivinityTierDef : Def
    {
        public int level;
        public int minFollowers;
        public float minLifetimeFaith;

        public float faithCapBase = 100f;
        public float faithCapPerFollower = 10f;

        /// <summary>Faith offered per day by a fully certain follower, without praying.</summary>
        public float passiveFaithPerFollowerPerDay = 1f;
    }
}
