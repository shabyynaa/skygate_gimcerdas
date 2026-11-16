using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SkyGate.Core
{
    public class GameInputReader : MonoBehaviour
    {
        private static GameInputReader instance;
        private static bool applicationQuitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            instance = null;
            applicationQuitting = false;
        }

        public static bool HasInstance => instance != null;
        public static GameInputReader PeekInstance => instance;

        public static GameInputReader Instance
        {
            get
            {
                if (applicationQuitting)
                    return instance;

                if (instance == null && Application.isPlaying)
                {
                    instance = FindAnyObjectByType<GameInputReader>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("GameInputReader_Auto");
                        instance = go.AddComponent<GameInputReader>();
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        private void OnApplicationQuit()
        {
            applicationQuitting = true;
        }

        [Header("Input Asset (Optional - Auto-creates actions if null)")]
        [SerializeField] private InputActionAsset inputActionAsset;

        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool IsGameplayEnabled { get; private set; } = true;

        public event Action OnAttackPressed;
        public event Action OnDodgePressed;
        public event Action OnInteractPressed;
        public event Action OnCameraTogglePressed;
        public event Action OnDebugTogglePressed;
        public event Action OnToggleDebugPressed
        {
            add => OnDebugTogglePressed += value;
            remove => OnDebugTogglePressed -= value;
        }

        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction attackAction;
        private InputAction dodgeAction;
        private InputAction interactAction;
        private InputAction cameraToggleAction;
        private InputAction debugToggleAction;
        private bool ownsRuntimeActions;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            SetupActions();
        }

        private void SetupActions()
        {
            if (inputActionAsset != null)
            {
                var playerMap = inputActionAsset.FindActionMap("Player", false);
                if (playerMap != null)
                {
                    moveAction = playerMap.FindAction("Move", false);
                    lookAction = playerMap.FindAction("Look", false);
                    attackAction = playerMap.FindAction("Attack", false);
                    dodgeAction = playerMap.FindAction("Dodge", false);
                    interactAction = playerMap.FindAction("Interact", false);
                    cameraToggleAction = playerMap.FindAction("ToggleCamera", false);
                    debugToggleAction = playerMap.FindAction("ToggleDebug", false);
                }
            }

            if (moveAction == null)
            {
                ownsRuntimeActions = true;
                moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
                moveAction.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/w")
                    .With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a")
                    .With("Right", "<Keyboard>/d");
                moveAction.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/upArrow")
                    .With("Down", "<Keyboard>/downArrow")
                    .With("Left", "<Keyboard>/leftArrow")
                    .With("Right", "<Keyboard>/rightArrow");
                moveAction.AddBinding("<Gamepad>/leftStick");
            }

            if (lookAction == null)
            {
                lookAction = new InputAction("Look", InputActionType.Value, expectedControlType: "Vector2");
                lookAction.AddBinding("<Pointer>/delta");
                lookAction.AddBinding("<Gamepad>/rightStick");
            }

            if (attackAction == null)
            {
                attackAction = new InputAction("Attack", InputActionType.Button);
                attackAction.AddBinding("<Gamepad>/buttonWest");
            }

            if (dodgeAction == null)
            {
                dodgeAction = new InputAction("Dodge", InputActionType.Button);
                dodgeAction.AddBinding("<Gamepad>/buttonSouth");
            }

            if (interactAction == null)
            {
                interactAction = new InputAction("Interact", InputActionType.Button);
                interactAction.AddBinding("<Gamepad>/buttonNorth");
            }

            if (cameraToggleAction == null)
            {
                cameraToggleAction = new InputAction("ToggleCamera", InputActionType.Button);
                cameraToggleAction.AddBinding("<Gamepad>/select");
            }

            if (debugToggleAction == null)
            {
                debugToggleAction = new InputAction("ToggleDebug", InputActionType.Button);
                debugToggleAction.AddBinding("<Gamepad>/start");
            }

            attackAction.performed += HandleAttack;
            dodgeAction.performed += HandleDodge;
            interactAction.performed += HandleInteract;
            cameraToggleAction.performed += HandleCameraToggle;
            debugToggleAction.performed += HandleDebugToggle;
        }

        private void OnEnable()
        {
            EnableAllActions(true);
        }

        private void OnDisable()
        {
            EnableAllActions(false);
        }

        private void OnDestroy()
        {
            if (attackAction != null) attackAction.performed -= HandleAttack;
            if (dodgeAction != null) dodgeAction.performed -= HandleDodge;
            if (interactAction != null) interactAction.performed -= HandleInteract;
            if (cameraToggleAction != null) cameraToggleAction.performed -= HandleCameraToggle;
            if (debugToggleAction != null) debugToggleAction.performed -= HandleDebugToggle;

            if (ownsRuntimeActions)
            {
                moveAction?.Dispose();
                lookAction?.Dispose();
                attackAction?.Dispose();
                dodgeAction?.Dispose();
                interactAction?.Dispose();
                cameraToggleAction?.Dispose();
                debugToggleAction?.Dispose();
            }

            if (instance == this)
                instance = null;
        }

        private void EnableAllActions(bool enable)
        {
            if (enable)
            {
                if (inputActionAsset != null) inputActionAsset.Enable();
                moveAction?.Enable();
                lookAction?.Enable();
                attackAction?.Enable();
                dodgeAction?.Enable();
                interactAction?.Enable();
                cameraToggleAction?.Enable();
                debugToggleAction?.Enable();
            }
            else
            {
                moveAction?.Disable();
                lookAction?.Disable();
                attackAction?.Disable();
                dodgeAction?.Disable();
                interactAction?.Disable();
                cameraToggleAction?.Disable();
                debugToggleAction?.Disable();
            }
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (kb != null && kb.f1Key.wasPressedThisFrame)
            {
                OnDebugTogglePressed?.Invoke();
            }

            if (!IsGameplayEnabled)
            {
                MoveInput = Vector2.zero;
                LookInput = Vector2.zero;
                return;
            }

            // 1. Read WASD / Arrow Keys directly from New Input System Keyboard + InputAction fallback
            Vector2 move = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
            }
            if (move.sqrMagnitude < 0.01f && moveAction != null)
            {
                move = moveAction.ReadValue<Vector2>();
            }
            MoveInput = move.sqrMagnitude > 1f ? move.normalized : move;

            // 2. Read Camera Orbit (Hold Right Mouse Button + drag, or Q/C keys)
            Vector2 look = Vector2.zero;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                look = mouse.delta.ReadValue();
            }
            if (kb != null)
            {
                if (kb.qKey.isPressed) look.x -= 8f;
                if (kb.cKey.isPressed) look.x += 8f;
            }
            LookInput = look;

            // 3. Action Buttons (Attack, Dodge, Interact, Camera Toggle)
            if (kb != null)
            {
                if (kb.spaceKey.wasPressedThisFrame)
                    OnDodgePressed?.Invoke();
                if (kb.eKey.wasPressedThisFrame)
                    OnInteractPressed?.Invoke();
                if (kb.vKey.wasPressedThisFrame)
                    OnCameraTogglePressed?.Invoke();
                if (kb.jKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
                    OnAttackPressed?.Invoke();
            }

            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                if (!overUi)
                {
                    OnAttackPressed?.Invoke();
                }
            }
        }

        public void SetGameplayEnabled(bool enabled, bool lockCursorWhenEnabled = false)
        {
            IsGameplayEnabled = enabled;
            if (!enabled)
            {
                MoveInput = Vector2.zero;
                LookInput = Vector2.zero;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = lockCursorWhenEnabled ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !lockCursorWhenEnabled;
            }
        }

        // Simulation helpers for automated testing & PlayMode verification
        public void SimulateMove(Vector2 move) => MoveInput = move;
        public void SimulateAttack() => OnAttackPressed?.Invoke();
        public void SimulateDodge() => OnDodgePressed?.Invoke();
        public void SimulateInteract() => OnInteractPressed?.Invoke();
        public void SimulateDebugToggle() => OnDebugTogglePressed?.Invoke();

        private void HandleAttack(InputAction.CallbackContext ctx)
        {
            if (IsGameplayEnabled) OnAttackPressed?.Invoke();
        }

        private void HandleDodge(InputAction.CallbackContext ctx)
        {
            if (IsGameplayEnabled) OnDodgePressed?.Invoke();
        }

        private void HandleInteract(InputAction.CallbackContext ctx)
        {
            if (IsGameplayEnabled) OnInteractPressed?.Invoke();
        }

        private void HandleCameraToggle(InputAction.CallbackContext ctx)
        {
            if (IsGameplayEnabled) OnCameraTogglePressed?.Invoke();
        }

        private void HandleDebugToggle(InputAction.CallbackContext ctx)
        {
            OnDebugTogglePressed?.Invoke();
        }
    }
}
