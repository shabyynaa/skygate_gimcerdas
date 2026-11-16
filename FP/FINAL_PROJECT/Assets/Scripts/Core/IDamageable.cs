namespace SkyGate.Core
{
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(DamageInfo info);
    }

    public interface IInteractable
    {
        string InteractionPrompt { get; }
        bool CanInteract { get; }
        void Interact(UnityEngine.GameObject interactor);
    }

    [System.Serializable]
    public struct DifficultyContext
    {
        public float multiplier;
        public float difficultyMultiplier
        {
            get => multiplier;
            set => multiplier = value;
        }
        public float hpMultiplier;
        public float damageMultiplier;
        public float aggressionMultiplier;
        public float speedMultiplier;
        public float extraEnemyBias;
        public float healOrbChance;
        public float safeGateBias;
        public float aggressiveScore;
        public float defensiveScore;
        public float riskTakerScore;
        public float confidence;

        public static DifficultyContext Default => new DifficultyContext
        {
            multiplier = 1.0f,
            hpMultiplier = 1.0f,
            damageMultiplier = 1.0f,
            aggressionMultiplier = 1.0f,
            speedMultiplier = 1.0f,
            extraEnemyBias = 0f,
            healOrbChance = 0.25f,
            safeGateBias = 0f,
            aggressiveScore = 0.33f,
            defensiveScore = 0.33f,
            riskTakerScore = 0.33f,
            confidence = 0.25f
        };
    }

    public interface IDifficultyScalable
    {
        void ApplyDifficulty(DifficultyContext ctx);
    }
}
