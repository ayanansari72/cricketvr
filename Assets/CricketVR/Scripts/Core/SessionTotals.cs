using System.Collections.Generic;

namespace CricketVR
{
    /// <summary>Totals derived from finalized delivery records only (never accumulated separately).</summary>
    public readonly struct SessionTotals
    {
        public readonly int BallsFaced, PracticeRuns, BatContacts, Misses, Bowled;

        SessionTotals(int balls, int runs, int contacts, int misses, int bowled)
        {
            BallsFaced = balls;
            PracticeRuns = runs;
            BatContacts = contacts;
            Misses = misses;
            Bowled = bowled;
        }

        public static SessionTotals From(IReadOnlyList<DeliveryResult> results)
        {
            int runs = 0, contacts = 0, misses = 0, bowled = 0;
            foreach (var r in results)
            {
                runs += r.Runs;
                if (r.BatContact) contacts++;
                if (r.Miss) misses++;
                if (r.Bowled) bowled++;
            }
            return new SessionTotals(results.Count, runs, contacts, misses, bowled);
        }

        public override string ToString() =>
            $"Balls {BallsFaced} | practice runs {PracticeRuns} | contacts {BatContacts} | misses {Misses} | bowled {Bowled}";
    }
}
