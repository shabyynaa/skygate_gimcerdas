using System.Collections.Generic;
using UnityEngine;

namespace SkyGate.MathGate
{
    [CreateAssetMenu(fileName = "MG_Preset", menuName = "SkyGate/MathGate Preset")]
    public class MathGateSO : ScriptableObject
    {
        [Header("Safe Gate Options (Gate A)")]
        public List<MathGateOptionData> safeOptions = new List<MathGateOptionData>();

        [Header("High-Risk Gate Options (Gate B)")]
        public List<MathGateOptionData> riskyOptions = new List<MathGateOptionData>();
    }
}
