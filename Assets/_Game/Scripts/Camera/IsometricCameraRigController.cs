using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace MaterialCityTycoon.Camera
{
    /// <summary>
    /// Moves and rotates a camera rig on the XZ plane and changes the orthographic size
    /// of its child camera through the Unity Input System.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IsometricCameraRigController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UnityEngine.Camera targetCamera;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float movementSpeed = 15f;
        [SerializeField] private Vector2 xLimits = new(-50f, 50f);
        [SerializeField] private Vector2 zLimits = new(-50f, 50f);

        [Header("Rotation")]
        [SerializeField, Min(0f)] private float rotationSpeed = 90f;

        [Header("Orthographic Zoom")]
        [SerializeField, Min(0f)] private float zoomSpeed = 0.02f;
        [SerializeField, Min(0.01f)] private float minimumOrthographicSize = 5f;
        [SerializeField, Min(0.01f)] private float maximumOrthographicSize = 30f;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponentInChildren<UnityEngine.Camera>();
            }

            if (targetCamera == null)
            {
                Debug.LogError("IsometricCameraRigController requires a child Camera.", this);
                enabled = false;
                return;
            }

            if (!targetCamera.orthographic)
            {
                Debug.LogWarning("IsometricCameraRigController expects an orthographic camera.", targetCamera);
            }
        }

        private void Update()
        {
            HandleRotation();
            HandleMovement();
            HandleZoom();
        }

        private void HandleMovement()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || targetCamera == null)
            {
                return;
            }

            float horizontalInput = ReadAxis(keyboard.dKey, keyboard.aKey);
            float verticalInput = ReadAxis(keyboard.wKey, keyboard.sKey);
            Vector3 input = new(horizontalInput, 0f, verticalInput);

            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            Vector3 forward = Vector3.ProjectOnPlane(targetCamera.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 movement = (right * input.x + forward * input.z) * movementSpeed * Time.deltaTime;

            Vector3 position = transform.position + movement;
            position.x = Mathf.Clamp(position.x, xLimits.x, xLimits.y);
            position.z = Mathf.Clamp(position.z, zLimits.x, zLimits.y);
            transform.position = position;
        }

        private void HandleRotation()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            float rotationInput = ReadAxis(keyboard.eKey, keyboard.qKey);
            transform.Rotate(Vector3.up, rotationInput * rotationSpeed * Time.deltaTime, Space.World);
        }

        private void HandleZoom()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || targetCamera == null)
            {
                return;
            }

            float scrollDelta = mouse.scroll.ReadValue().y;
            if (Mathf.Approximately(scrollDelta, 0f))
            {
                return;
            }

            targetCamera.orthographicSize = Mathf.Clamp(
                targetCamera.orthographicSize - scrollDelta * zoomSpeed,
                minimumOrthographicSize,
                maximumOrthographicSize);
        }

        private void OnValidate()
        {
            movementSpeed = Mathf.Max(0f, movementSpeed);
            rotationSpeed = Mathf.Max(0f, rotationSpeed);
            zoomSpeed = Mathf.Max(0f, zoomSpeed);
            minimumOrthographicSize = Mathf.Max(0.01f, minimumOrthographicSize);
            maximumOrthographicSize = Mathf.Max(minimumOrthographicSize, maximumOrthographicSize);

            if (xLimits.x > xLimits.y)
            {
                (xLimits.x, xLimits.y) = (xLimits.y, xLimits.x);
            }

            if (zLimits.x > zLimits.y)
            {
                (zLimits.x, zLimits.y) = (zLimits.y, zLimits.x);
            }
        }

        private static float ReadAxis(KeyControl positiveKey, KeyControl negativeKey)
        {
            return (positiveKey.isPressed ? 1f : 0f) - (negativeKey.isPressed ? 1f : 0f);
        }
    }
}
