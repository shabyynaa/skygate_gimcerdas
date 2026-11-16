namespace SkyGate.World
{
    public interface IRoomChallenge
    {
        string ObjectiveDescription { get; }
        bool IsCompleted { get; }
        void InitializeChallenge(IslandNode island, RoomController controller);
        void TickChallenge();
        void ResetChallenge();
    }
}
