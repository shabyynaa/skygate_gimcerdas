using UnityEngine;
using SkyGate.Core;
using SkyGate.MathGate;

namespace SkyGate.DDA
{
    public class PlayerModelManager : MonoBehaviour
    {
        [SerializeField] private PlayerModel model = new PlayerModel();
        [SerializeField] private float targetRoomClearTime = 35.0f;

        public PlayerModel Model => model;

        private void OnEnable()
        {
            GameEvents.OnMathGateChosen += HandleMathGateChosen;
        }

        private void OnDisable()
        {
            GameEvents.OnMathGateChosen -= HandleMathGateChosen;
        }

        public void ResetModel()
        {
            model.Reset();
        }

        public PlayerModel ProcessRoomSummary(RoomTelemetrySummary summary)
        {
            model.EvaluateRoom(summary, targetRoomClearTime);
            return model;
        }

        private void HandleMathGateChosen(MathGateDoor door, bool isRisky)
        {
            model.RecordGateChoice(isRisky);
        }
    }
}
