using UnityEngine;

namespace CricketVR
{
    /// <summary>
    /// Marks a net boundary collider and which wall it is. Practice zones are regions of these walls, classified
    /// from the contact point by <see cref="PracticeScoring.NetZone"/> (plan.md §6).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class NetWall : MonoBehaviour
    {
        public WallSide side;
    }
}
