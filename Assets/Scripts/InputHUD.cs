using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Shows which device / control scheme is active, the matching control hints and the live input values.
/// Everything it needs is assigned in the Inspector; the Buttons call the public methods through OnClick.
/// </summary>
public class InputHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] PlayerController player;
    [SerializeField] PlayerInput playerInput;

    [Header("Labels")]
    [SerializeField] Text deviceText;
    [SerializeField] Text hintsText;
    [SerializeField] Text valuesText;
    [SerializeField] Text cameraButtonLabel;
    [SerializeField] Text touchButtonLabel;

    [Header("Touch Controls")]
    [SerializeField] GameObject touchControls;

    const string KeyboardHints =
        "WASD  Move\nMouse  Look\nShift  Run\nSpace  Jump\nC  Crouch\nLMB  Punch    RMB  Kick\nV  Camera    Esc  Free cursor";
    const string GamepadHints =
        "Left stick  Move\nRight stick  Look\nL-Stick click  Run\nA  Jump    B  Crouch\nX  Punch    RB  Kick\nR-Stick click  Camera";
    const string TouchHints =
        "Left stick  Move\nRight stick  Look\nButtons on the right:\nJump, Punch, Kick, Crouch, Run, Camera";

    bool touchOn;

    void OnEnable()
    {
        playerInput.onControlsChanged += OnControlsChanged;
        player.CameraModeChanged += OnCameraModeChanged;
    }

    void OnDisable()
    {
        playerInput.onControlsChanged -= OnControlsChanged;
        player.CameraModeChanged -= OnCameraModeChanged;
    }

    void Start()
    {
        // Phones start with touch controls on; on PC they are opt-in from the HUD button
        SetTouchControls(Application.isMobilePlatform);
        OnCameraModeChanged(player.Mode);
        OnControlsChanged(playerInput);
    }

    void Update()
    {
        valuesText.text =
            $"Move  {player.MoveInput.x:0.00}, {player.MoveInput.y:0.00}\n" +
            $"Look  {player.LookInput.x:0.0}, {player.LookInput.y:0.0}\n" +
            $"Grounded {player.IsGrounded}   Run {player.IsRunning}   Crouch {player.IsCrouching}";
    }

    // ---- UI Button callbacks ----

    public void ToggleTouchControls() => SetTouchControls(!touchOn);

    // ---- Events ----

    void SetTouchControls(bool on)
    {
        touchOn = on;
        touchControls.SetActive(on);
        player.SetTouchMode(on);
        touchButtonLabel.text = on ? "Touch Controls: ON" : "Touch Controls: OFF";
        OnControlsChanged(playerInput);
    }

    void OnControlsChanged(PlayerInput input)
    {
        string scheme = input.currentControlScheme;
        bool gamepad = scheme == PlayerController.GamepadScheme;

        if (touchOn && gamepad)
        {
            deviceText.text = "Device: Touch (on-screen controls)";
            hintsText.text = TouchHints;
        }
        else if (gamepad)
        {
            deviceText.text = "Device: Gamepad";
            hintsText.text = GamepadHints;
        }
        else
        {
            deviceText.text = "Device: Keyboard & Mouse";
            hintsText.text = KeyboardHints;
        }
    }

    void OnCameraModeChanged(PlayerController.CameraMode mode)
    {
        cameraButtonLabel.text = mode == PlayerController.CameraMode.FirstPerson ? "Camera: First Person" : "Camera: Third Person";
    }
}
