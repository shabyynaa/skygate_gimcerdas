using UnityEngine;
using SkyGate.Core;
using SkyGate.EnemyAI;
using SkyGate.MathGate;
using SkyGate.World;

namespace SkyGate.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioManager : MonoBehaviour
    {
        [Header("SFX Clips (Assets/Audio)")]
        [SerializeField] private AudioClip sfxSwing;
        [SerializeField] private AudioClip sfxHitImpact;
        [SerializeField] private AudioClip sfxHeavyImpact;
        [SerializeField] private AudioClip sfxDodge;
        [SerializeField] private AudioClip sfxGateChoose;
        [SerializeField] private AudioClip sfxRoomClear;
        [SerializeField] private AudioClip sfxPlayerHurt;

        private AudioSource sfxSource;

        private void Awake()
        {
            sfxSource = GetComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
        }

        private void OnEnable()
        {
            GameEvents.OnEnemyDamaged += HandleEnemyDamaged;
            GameEvents.OnPlayerDamaged += HandlePlayerDamaged;
            GameEvents.OnPlayerDodgeSuccess += HandleDodgeSuccess;
            GameEvents.OnMathGateChosen += HandleGateChosen;
            GameEvents.OnRoomClearedWithTime += HandleRoomCleared;
        }

        private void OnDisable()
        {
            GameEvents.OnEnemyDamaged -= HandleEnemyDamaged;
            GameEvents.OnPlayerDamaged -= HandlePlayerDamaged;
            GameEvents.OnPlayerDodgeSuccess -= HandleDodgeSuccess;
            GameEvents.OnMathGateChosen -= HandleGateChosen;
            GameEvents.OnRoomClearedWithTime -= HandleRoomCleared;
        }

        private void PlayClip(AudioClip clip, float volume = 0.75f)
        {
            if (clip != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(clip, volume);
            }
        }

        private void HandleEnemyDamaged(EnemyBrain enemy, DamageInfo info, bool wasStunned)
        {
            PlayClip(info.isCritical || wasStunned ? sfxHeavyImpact : sfxHitImpact, 0.8f);
        }

        private void HandlePlayerDamaged(float amount, float remainingHP, float shield)
        {
            PlayClip(sfxPlayerHurt, 0.85f);
        }

        private void HandleDodgeSuccess()
        {
            PlayClip(sfxDodge, 0.65f);
        }

        private void HandleGateChosen(MathGateDoor door, bool isRisky)
        {
            PlayClip(sfxGateChoose, 0.85f);
        }

        private void HandleRoomCleared(IslandNode node, float clearTime)
        {
            PlayClip(sfxRoomClear, 0.9f);
        }
    }
}
