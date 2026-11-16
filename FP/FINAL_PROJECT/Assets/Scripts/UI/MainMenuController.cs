using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;
using SkyGate.Core;
using SkyGate.World;

namespace SkyGate.UI
{
    /// <summary>
    /// Controls the stylized wooden-signpost Main Menu, smooth entrance & modal transitions,
    /// persistent settings (volume, camera orbit, DDA, seed), credits popup, and Play launch transition.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        private const string PrefVolume = "SkyGate_MasterVolume";
        private const string PrefOrbit = "SkyGate_MenuOrbit";
        private const string PrefDda = "SkyGate_AdaptiveDDA";
        private const string PrefSeed = "SkyGate_RunSeed";

        [Header("Primary Signpost & Footer Buttons")]
        [SerializeField] private Button startRunButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button creditsButton;

        [Header("Animated UI Targets")]
        [SerializeField] private RectTransform crestRect;
        [SerializeField] private RectTransform flagRect;
        [SerializeField] private RectTransform signpostGroupRect;
        [SerializeField] private CanvasGroup screenFadeGroup;
        [SerializeField] private MainMenuShowcaseDiorama diorama;

        [Header("Settings Modal Controls")]
        [SerializeField] private GameObject settingsModalRoot;
        [SerializeField] private CanvasGroup settingsModalGroup;
        [SerializeField] private RectTransform settingsModalCard;
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private TMP_Text volumePercentText;
        [SerializeField] private Toggle orbitCameraToggle;
        [SerializeField] private Toggle adaptiveDdaToggle;
        [SerializeField] private TMP_InputField seedInputField;
        [SerializeField] private Button randomizeSeedButton;
        [SerializeField] private Button closeSettingsButton;

        [Header("Credits Modal Controls")]
        [SerializeField] private GameObject creditsModalRoot;
        [SerializeField] private CanvasGroup creditsModalGroup;
        [SerializeField] private RectTransform creditsModalCard;
        [SerializeField] private Button closeCreditsButton;

        [Header("Legacy / Extra Mode Launchers (Inside Settings)")]
        [SerializeField] private Button startRandomSeedButton;
        [SerializeField] private Button openCombatSandboxButton;
        [SerializeField] private Button openAssetShowcaseButton;

        [Header("UI Audio")]
        [SerializeField] private AudioSource uiAudioSource;
        [SerializeField] private AudioClip sfxModalOpen;
        [SerializeField] private AudioClip sfxModalClose;
        [SerializeField] private AudioClip sfxPlayStart;

        private Vector2 crestTargetPos;
        private Vector2 signpostTargetPos;
        private float crestBaseZ;
        private float flagBaseZ;
        private float introTimer;
        private bool introActive;
        private bool isTransitioning;

        private Coroutine activeModalAnim;

        private void Awake()
        {
            if (diorama == null)
                diorama = FindAnyObjectByType<MainMenuShowcaseDiorama>();

            LoadPersistedPreferences();

            if (startRunButton != null)
                startRunButton.onClick.AddListener(OnStartSeededRun);
            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnOpenSettings);
            if (quitButton != null)
                quitButton.onClick.AddListener(OnQuitGame);
            if (creditsButton != null)
                creditsButton.onClick.AddListener(OnOpenCredits);

            if (closeSettingsButton != null)
                closeSettingsButton.onClick.AddListener(OnCloseSettings);
            if (closeCreditsButton != null)
                closeCreditsButton.onClick.AddListener(OnCloseCredits);

            if (startRandomSeedButton != null)
                startRandomSeedButton.onClick.AddListener(OnStartRandomRun);
            if (randomizeSeedButton != null)
                randomizeSeedButton.onClick.AddListener(OnRandomizeSeedField);
            if (openCombatSandboxButton != null)
                openCombatSandboxButton.onClick.AddListener(OnOpenCombatSandbox);
            if (openAssetShowcaseButton != null)
                openAssetShowcaseButton.onClick.AddListener(OnOpenAssetShowcase);

            if (masterVolumeSlider != null)
                masterVolumeSlider.onValueChanged.AddListener(HandleVolumeChanged);
            if (orbitCameraToggle != null)
                orbitCameraToggle.onValueChanged.AddListener(HandleOrbitToggleChanged);
            if (adaptiveDdaToggle != null)
                adaptiveDdaToggle.onValueChanged.AddListener(HandleDdaToggleChanged);
            if (seedInputField != null)
                seedInputField.onEndEdit.AddListener(HandleSeedEdited);

            if (crestRect != null)
            {
                crestTargetPos = crestRect.anchoredPosition;
                crestBaseZ = crestRect.localEulerAngles.z;
                if (crestBaseZ > 180f) crestBaseZ -= 360f;
            }
            if (flagRect != null)
            {
                flagBaseZ = flagRect.localEulerAngles.z;
                if (flagBaseZ > 180f) flagBaseZ -= 360f;
            }
            if (signpostGroupRect != null)
            {
                signpostTargetPos = signpostGroupRect.anchoredPosition;
            }

            if (settingsModalRoot != null)
                settingsModalRoot.SetActive(false);
            if (creditsModalRoot != null)
                creditsModalRoot.SetActive(false);

            if (screenFadeGroup != null)
            {
                screenFadeGroup.alpha = 1f;
                screenFadeGroup.blocksRaycasts = false;
            }

            introTimer = 0f;
            introActive = true;
            ApplyIntroFrame(0f);
        }

        private void Start()
        {
            StartCoroutine(FadeFromWhiteRoutine());
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float t = Time.unscaledTime;

            if (introActive)
            {
                introTimer += dt;
                float norm = Mathf.Clamp01(introTimer / 0.65f);
                ApplyIntroFrame(norm);
                if (norm >= 1f)
                    introActive = false;
            }
            else if (crestRect != null)
            {
                // Gentle floating crest breath matching sky island breeze
                float breathY = Mathf.Sin(t * 1.45f) * 5.0f;
                float tiltZ = Mathf.Sin(t * 0.95f) * 0.9f;
                crestRect.anchoredPosition = crestTargetPos + new Vector2(0f, breathY);
                crestRect.localRotation = Quaternion.Euler(0f, 0f, crestBaseZ + tiltZ);
            }

            if (flagRect != null)
            {
                // Continuous heraldic pennant wave
                float waveZ = Mathf.Sin(t * 3.1f) * 3.2f + Mathf.Cos(t * 5.4f) * 1.1f;
                float waveXScale = 1f + Mathf.Sin(t * 4.2f) * 0.035f;
                flagRect.localRotation = Quaternion.Euler(0f, 0f, flagBaseZ + waveZ);
                flagRect.localScale = new Vector3(waveXScale, 1f, 1f);
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && !isTransitioning)
            {
                if (settingsModalRoot != null && settingsModalRoot.activeSelf)
                    OnCloseSettings();
                else if (creditsModalRoot != null && creditsModalRoot.activeSelf)
                    OnCloseCredits();
            }
        }

        private void LoadPersistedPreferences()
        {
            float vol = PlayerPrefs.GetFloat(PrefVolume, 0.85f);
            AudioListener.volume = vol;
            if (masterVolumeSlider != null)
                masterVolumeSlider.SetValueWithoutNotify(vol);
            UpdateVolumeText(vol);

            bool orbitOn = PlayerPrefs.GetInt(PrefOrbit, 1) == 1;
            if (orbitCameraToggle != null)
                orbitCameraToggle.SetIsOnWithoutNotify(orbitOn);
            if (diorama != null)
                diorama.SetOrbitSpeedMultiplier(orbitOn ? 1f : 0.16f);

            bool ddaOn = PlayerPrefs.GetInt(PrefDda, 1) == 1;
            if (adaptiveDdaToggle != null)
                adaptiveDdaToggle.SetIsOnWithoutNotify(ddaOn);

            string savedSeed = PlayerPrefs.GetString(PrefSeed, "12345");
            if (seedInputField != null)
                seedInputField.text = string.IsNullOrWhiteSpace(savedSeed) ? "12345" : savedSeed;
        }

        private void ApplyIntroFrame(float norm)
        {
            // Smooth cubic overshoot ease-out curve for playful cartoon entrance
            float inv = 1f - norm;
            float crestEase = 1f - inv * inv * inv;
            float postDelay = Mathf.Clamp01((norm - 0.12f) / 0.88f);
            float postInv = 1f - postDelay;
            float postEase = 1f - postInv * postInv * postInv;

            if (crestRect != null)
                crestRect.anchoredPosition = crestTargetPos + new Vector2(0f, Mathf.Lerp(150f, 0f, crestEase));

            if (signpostGroupRect != null)
                signpostGroupRect.anchoredPosition = signpostTargetPos + new Vector2(Mathf.Lerp(260f, 0f, postEase), Mathf.Lerp(-80f, 0f, postEase));
        }

        private IEnumerator FadeFromWhiteRoutine()
        {
            if (screenFadeGroup == null) yield break;
            float elapsed = 0f;
            const float dur = 0.38f;
            while (elapsed < dur)
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                screenFadeGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / dur);
                yield return null;
            }
            screenFadeGroup.alpha = 0f;
        }

        private void HandleVolumeChanged(float val)
        {
            AudioListener.volume = Mathf.Clamp01(val);
            PlayerPrefs.SetFloat(PrefVolume, AudioListener.volume);
            UpdateVolumeText(AudioListener.volume);
        }

        private void UpdateVolumeText(float val)
        {
            if (volumePercentText != null)
                volumePercentText.text = $"{Mathf.RoundToInt(val * 100f)}%";
        }

        private void HandleOrbitToggleChanged(bool isOn)
        {
            PlayerPrefs.SetInt(PrefOrbit, isOn ? 1 : 0);
            if (diorama != null)
                diorama.SetOrbitSpeedMultiplier(isOn ? 1f : 0.16f);
        }

        private void HandleDdaToggleChanged(bool isOn)
        {
            PlayerPrefs.SetInt(PrefDda, isOn ? 1 : 0);
        }

        private void HandleSeedEdited(string raw)
        {
            string trimmed = string.IsNullOrWhiteSpace(raw) ? "12345" : raw.Trim();
            if (seedInputField != null)
                seedInputField.text = trimmed;
            PlayerPrefs.SetString(PrefSeed, trimmed);
        }

        public void OnRandomizeSeedField()
        {
            string nextSeed = Random.Range(10000, 99999).ToString();
            if (seedInputField != null)
                seedInputField.text = nextSeed;
            PlayerPrefs.SetString(PrefSeed, nextSeed);
        }

        public void OnOpenSettings()
        {
            if (isTransitioning) return;
            CloseModalImmediate(creditsModalRoot);
            OpenModalAnimated(settingsModalRoot, settingsModalGroup, settingsModalCard);
        }

        public void OnCloseSettings()
        {
            PlayerPrefs.Save();
            CloseModalAnimated(settingsModalRoot, settingsModalGroup, settingsModalCard);
        }

        public void OnOpenCredits()
        {
            if (isTransitioning) return;
            CloseModalImmediate(settingsModalRoot);
            OpenModalAnimated(creditsModalRoot, creditsModalGroup, creditsModalCard);
        }

        public void OnCloseCredits()
        {
            CloseModalAnimated(creditsModalRoot, creditsModalGroup, creditsModalCard);
        }

        private void OpenModalAnimated(GameObject root, CanvasGroup group, RectTransform card)
        {
            if (root == null) return;
            PlayClip(sfxModalOpen, 0.7f);
            if (activeModalAnim != null) StopCoroutine(activeModalAnim);
            activeModalAnim = StartCoroutine(AnimateModalRoutine(root, group, card, true));
        }

        private void CloseModalAnimated(GameObject root, CanvasGroup group, RectTransform card)
        {
            if (root == null || !root.activeSelf) return;
            PlayClip(sfxModalClose, 0.65f);
            if (activeModalAnim != null) StopCoroutine(activeModalAnim);
            activeModalAnim = StartCoroutine(AnimateModalRoutine(root, group, card, false));
        }

        private static void CloseModalImmediate(GameObject root)
        {
            if (root != null && root.activeSelf)
                root.SetActive(false);
        }

        private IEnumerator AnimateModalRoutine(GameObject root, CanvasGroup group, RectTransform card, bool opening)
        {
            if (opening)
                root.SetActive(true);

            float elapsed = 0f;
            float dur = opening ? 0.24f : 0.16f;
            while (elapsed < dur)
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                float u = Mathf.Clamp01(elapsed / dur);
                float smooth = opening ? 1f - Mathf.Pow(1f - u, 3f) : 1f - (u * u);
                float scale = opening ? Mathf.Lerp(0.84f, 1.0f, smooth) : Mathf.Lerp(0.88f, 1.0f, smooth);

                if (group != null) group.alpha = smooth;
                if (card != null) card.localScale = Vector3.one * scale;
                yield return null;
            }

            if (group != null) group.alpha = opening ? 1f : 0f;
            if (card != null) card.localScale = Vector3.one;
            if (!opening) root.SetActive(false);
            activeModalAnim = null;
        }

        public void OnStartSeededRun()
        {
            if (isTransitioning) return;

            int parsedSeed = 12345;
            if (seedInputField != null && !string.IsNullOrWhiteSpace(seedInputField.text))
            {
                string raw = seedInputField.text.Trim();
                if (!int.TryParse(raw, out parsedSeed))
                    parsedSeed = Mathf.Abs(raw.GetHashCode() % 90000) + 10000;
            }

            GameManager.RequestedSeed = parsedSeed;
            GameManager.RequestedAdaptiveMode = adaptiveDdaToggle == null || adaptiveDdaToggle.isOn;
            GameManager.UseRequestedConfig = true;
            PlayerPrefs.Save();

            StartCoroutine(TransitionAndLoadRoutine("Gameplay", true));
        }

        public void OnStartRandomRun()
        {
            if (isTransitioning) return;

            GameManager.RequestedSeed = Random.Range(10000, 99999);
            GameManager.RequestedAdaptiveMode = adaptiveDdaToggle == null || adaptiveDdaToggle.isOn;
            GameManager.UseRequestedConfig = true;
            PlayerPrefs.Save();

            StartCoroutine(TransitionAndLoadRoutine("Gameplay", true));
        }

        public void OnOpenCombatSandbox()
        {
            if (isTransitioning) return;
            StartCoroutine(TransitionAndLoadRoutine("CombatSandbox", true));
        }

        public void OnOpenAssetShowcase()
        {
            if (isTransitioning) return;
            StartCoroutine(TransitionAndLoadRoutine("AssetShowcase", true));
        }

        public void OnQuitGame()
        {
            if (isTransitioning) return;
            PlayerPrefs.Save();
            StartCoroutine(QuitRoutine());
        }

        private IEnumerator TransitionAndLoadRoutine(string sceneName, bool zoomCamera)
        {
            isTransitioning = true;
            PlayClip(sfxPlayStart, 0.85f);
            if (zoomCamera && diorama != null)
                diorama.BeginPlayTransition();

            float elapsed = 0f;
            const float dur = 0.52f;
            if (screenFadeGroup != null)
                screenFadeGroup.blocksRaycasts = true;

            while (elapsed < dur)
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                if (screenFadeGroup != null)
                    screenFadeGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / dur));
                yield return null;
            }

            SceneManager.LoadScene(sceneName);
        }

        private IEnumerator QuitRoutine()
        {
            isTransitioning = true;
            PlayClip(sfxModalClose, 0.8f);
            float elapsed = 0f;
            const float dur = 0.35f;
            if (screenFadeGroup != null)
                screenFadeGroup.blocksRaycasts = true;

            while (elapsed < dur)
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                if (screenFadeGroup != null)
                    screenFadeGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / dur));
                yield return null;
            }

            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void PlayClip(AudioClip clip, float volume)
        {
            if (uiAudioSource != null && clip != null)
                uiAudioSource.PlayOneShot(clip, volume);
        }
    }
}
