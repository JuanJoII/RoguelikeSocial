using UnityEngine;

/// <summary>
/// Abstracción de input. Este script recibe datos del joystick virtual
/// y los expone limpiamente al PlayerController.
///
/// USO CON JOYSTICK VIRTUAL:
/// Tu script de joystick llama a:
///   PlayerInputHandler.Instance.SetMoveInput(joystick.Direction);
///   PlayerInputHandler.Instance.SetLookInput(joystick.Direction);
///
/// USO EN EDITOR (teclado para testing):
/// WASD mueve, flechas rotan. Así puedes probar sin joystick.
/// </summary>
public class PlayerInputHandler : MonoBehaviour
{
    public static PlayerInputHandler Instance { get; private set; }

    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool DashPressed { get; private set; }
    public bool SwitchBulletPressed { get; private set; }

    [Header("Testing en Editor")]
    [SerializeField] private bool useKeyboardFallback = true;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (!useKeyboardFallback) return;

        // Fallback de teclado para probar sin joystick en el editor
        MoveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        // Flechas para rotación
        float lookX = Input.GetAxisRaw("Fire1") - Input.GetAxisRaw("Fire2"); // placeholder
        Vector2 keyboardLook = Vector2.zero;
        if (Input.GetKey(KeyCode.LeftArrow))  keyboardLook.x = -1f;
        if (Input.GetKey(KeyCode.RightArrow)) keyboardLook.x =  1f;
        if (Input.GetKey(KeyCode.UpArrow))    keyboardLook.y =  1f;
        if (Input.GetKey(KeyCode.DownArrow))  keyboardLook.y = -1f;
        if (keyboardLook != Vector2.zero) LookInput = keyboardLook;

        DashPressed = Input.GetKeyDown(KeyCode.Space);
        SwitchBulletPressed = Input.GetKeyDown(KeyCode.Q);
    }

    // ── API para joysticks virtuales ─────────────────────────────────────
    public void SetMoveInput(Vector2 input) => MoveInput = input;
    public void SetLookInput(Vector2 input) => LookInput = input;
    public void SetDashPressed(bool value) => DashPressed = value;
    public void SetSwitchBulletPressed(bool value) => SwitchBulletPressed = value;
}