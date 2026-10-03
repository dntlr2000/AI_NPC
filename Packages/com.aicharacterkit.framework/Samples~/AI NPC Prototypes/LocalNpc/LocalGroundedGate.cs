using UnityEngine;

namespace AiCharacterKit.Samples.Local
{
    /// <summary>One consumer-owned gate and alarm shared by both sample guards; dialogue memory stays per NPC.</summary>
    public sealed class LocalGroundedGate : MonoBehaviour
    {
        public bool GateOpen;
        public bool Alarm;
    }
}
