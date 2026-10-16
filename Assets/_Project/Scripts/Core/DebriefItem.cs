namespace CyberQuest.Core
{
    /// <summary>One debrief entry per reached situation (CLAUDE.md 6).</summary>
    public class DebriefItem
    {
        public string Situation;
        public string WhatHappened;
        public string WhyItMattered;
        public string NextTime;
        /// <summary>0 = risky, 1 = partly right, 2 = handled well. Drives the colour on the ending screen.</summary>
        public int Grade;

        public DebriefItem(string situation, int grade, string whatHappened, string whyItMattered, string nextTime)
        {
            Situation = situation;
            Grade = grade;
            WhatHappened = whatHappened;
            WhyItMattered = whyItMattered;
            NextTime = nextTime;
        }
    }
}
