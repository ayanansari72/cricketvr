using System;
using UnityEngine;
using FD = CricketVR.FieldDimensions;

namespace CricketVR
{
    public enum WallSide { Front, Left, Right, Rear }

    /// <summary>
    /// Practice scoring zones (plan.md §6, PC-01). Declared in priority order: when several terminal events
    /// happen in the same physics step, the lowest value wins.
    /// </summary>
    public enum ScoringZone { None, FrontUpper, FrontMiddle, FrontLow, SideNet, RearNet, OverFront, OverSide, OverRear }

    [Serializable]
    public struct ScoringRules
    {
        [Tooltip("Front-net hits below this height (m) are in the low zone.")]
        public float frontLowMaxHeight;
        [Tooltip("Front-net hits from frontLowMaxHeight up to below this height (m) are in the middle zone; above, the upper zone.")]
        public float frontMiddleMaxHeight;
        public int frontLowRuns;
        public int frontMiddleRuns;
        public int frontUpperRuns;
        public int sideNetRuns;
        public int rearNetRuns;
        public int overFrontRuns;
        public int overSideRuns;
        public int overRearRuns;

        public static ScoringRules Default => new ScoringRules
        {
            frontLowMaxHeight = 1f,
            frontMiddleMaxHeight = 3f,
            frontLowRuns = 2,
            frontMiddleRuns = 4,
            frontUpperRuns = 6,
            sideNetRuns = 1,
            rearNetRuns = 1,
            overFrontRuns = 6,
            overSideRuns = 1,
            overRearRuns = 1,
        };
    }

    /// <summary>Pure zone and run decisions. Runs apply only after a bat contact; the caller enforces that.</summary>
    public static class PracticeScoring
    {
        /// <summary>Zone for a ball meeting a net wall at <paramref name="height"/> metres. Height bounds are half-open.</summary>
        public static ScoringZone NetZone(WallSide wall, float height, in ScoringRules rules)
        {
            switch (wall)
            {
                case WallSide.Front:
                    if (height < rules.frontLowMaxHeight) return ScoringZone.FrontLow;
                    if (height < rules.frontMiddleMaxHeight) return ScoringZone.FrontMiddle;
                    return ScoringZone.FrontUpper;
                case WallSide.Rear:
                    return ScoringZone.RearNet;
                default:
                    return ScoringZone.SideNet;
            }
        }

        public static ScoringZone ExitZone(WallSide wall) =>
            wall == WallSide.Front ? ScoringZone.OverFront : wall == WallSide.Rear ? ScoringZone.OverRear : ScoringZone.OverSide;

        public static int Runs(ScoringZone zone, in ScoringRules rules)
        {
            switch (zone)
            {
                case ScoringZone.FrontLow: return rules.frontLowRuns;
                case ScoringZone.FrontMiddle: return rules.frontMiddleRuns;
                case ScoringZone.FrontUpper: return rules.frontUpperRuns;
                case ScoringZone.SideNet: return rules.sideNetRuns;
                case ScoringZone.RearNet: return rules.rearNetRuns;
                case ScoringZone.OverFront: return rules.overFrontRuns;
                case ScoringZone.OverSide: return rules.overSideRuns;
                case ScoringZone.OverRear: return rules.overRearRuns;
                default: return 0;
            }
        }

        /// <summary>
        /// Which wall the ball went over, if its centre is more than <paramref name="margin"/> outside the enclosure.
        /// The walls use CCD, so a ball outside them has cleared one. At a corner: Front, then Side, then Rear.
        /// </summary>
        public static WallSide? ExitSide(Vector3 ballCentre, float margin)
        {
            if (ballCentre.z > FD.NetFrontZ + margin) return WallSide.Front;
            if (ballCentre.x > FD.NetHalfWidth + margin) return WallSide.Right;
            if (ballCentre.x < -FD.NetHalfWidth - margin) return WallSide.Left;
            if (ballCentre.z < FD.NetRearZ - margin) return WallSide.Rear;
            return null;
        }

        public static string ZoneLabel(ScoringZone zone)
        {
            switch (zone)
            {
                case ScoringZone.FrontLow: return "front net, low";
                case ScoringZone.FrontMiddle: return "front net, middle";
                case ScoringZone.FrontUpper: return "front net, high";
                case ScoringZone.SideNet: return "side net";
                case ScoringZone.RearNet: return "back net";
                case ScoringZone.OverFront: return "over the front net";
                case ScoringZone.OverSide: return "over a side net";
                case ScoringZone.OverRear: return "over the back net";
                default: return "no zone";
            }
        }
    }
}
