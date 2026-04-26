using UnityEngine;
using UnityEngine.InputSystem;

/// Detects mouse clicks / touch taps via the new Input System and routes them to
/// any world-space object that implements <see cref="ITappable"/>.
/// Assign the ShooterEntity layer (and any other tappable layers) to shooterSlotLayer.
public class InputHandler : MonoBehaviour
{
    [SerializeField] private Camera gameCamera;
    [SerializeField] private LayerMask tappableLayer;

    private InputAction _tapAction;

    private void Awake()
    {
        if (gameCamera == null)
            gameCamera = Camera.main;

        _tapAction = new InputAction("Tap", InputActionType.Button, "<Pointer>/press");
        _tapAction.Enable();
    }

    private void OnEnable()
    {
        if (_tapAction != null) _tapAction.performed += OnTap;
    }

    private void OnDisable()
    {
        if (_tapAction != null) _tapAction.performed -= OnTap;
    }

    private void OnDestroy()
    {
        _tapAction?.Disable();
        _tapAction?.Dispose();
    }

    private void OnTap(InputAction.CallbackContext context)
    {
        if (Pointer.current == null) return;

        Vector2 screenPos = Pointer.current.position.ReadValue();
        Ray ray = gameCamera.ScreenPointToRay(screenPos);

        Debug.DrawRay(ray.origin, ray.direction * 100f, Color.cyan, 0.5f);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, tappableLayer))
        {
            ITappable tappable = hit.collider.GetComponentInParent<ITappable>();
            tappable?.OnTapped();
        }
    }
}