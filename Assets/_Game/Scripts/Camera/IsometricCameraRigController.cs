using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Serialization;

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
        [FormerlySerializedAs("zoomSpeed")]
        [SerializeField, Min(0f)] private float zoomSensitivity = 0.05f;
        [SerializeField, Min(0.001f)] private float zoomSmoothTime = 0.15f;
        [SerializeField, Min(0.01f)] private float minimumOrthographicSize = 5f;
        [SerializeField, Min(0.01f)] private float maximumOrthographicSize = 30f;

        [Header("Tilt With Zoom")]
        [SerializeField] private bool tiltWithZoom = true;
        [SerializeField, Range(1f, 89f)] private float nearZoomPitch = 35f;
        [SerializeField, Range(1f, 89f)] private float farZoomPitch = 60f;

        private float targetOrthographicSize;
        private float zoomVelocity;
        private Vector3 initialCameraLocalPosition;
        private Quaternion initialCameraLocalRotation;
        private float cameraOrbitDistance;

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

            initialCameraLocalPosition = targetCamera.transform.localPosition;
            initialCameraLocalRotation = targetCamera.transform.localRotation;
            cameraOrbitDistance = Mathf.Max(0.01f, initialCameraLocalPosition.magnitude);

            targetOrthographicSize = Mathf.Clamp(
                targetCamera.orthographicSize,
                minimumOrthographicSize,
                maximumOrthographicSize);
            targetCamera.orthographicSize = targetOrthographicSize;
            ApplyTiltWithZoom();
        }

        private void Update()
        {
            HandleRotation();
            HandleMovement();
            HandleZoom();
            ApplyTiltWithZoom();
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
            if (targetCamera == null)
            {
                return;
            }

            if (mouse != null)
            {
                float scrollDelta = mouse.scroll.ReadValue().y;
                if (!Mathf.Approximately(scrollDelta, 0f))
                {
                    targetOrthographicSize = Mathf.Clamp(
                        targetOrthographicSize - scrollDelta * zoomSensitivity,
                        minimumOrthographicSize,
                        maximumOrthographicSize);
                }
            }

            targetOrthographicSize = Mathf.Clamp(
                targetOrthographicSize,
                minimumOrthographicSize,
                maximumOrthographicSize);

            targetCamera.orthographicSize = Mathf.Clamp(
                Mathf.SmoothDamp(
                    targetCamera.orthographicSize,
                    targetOrthographicSize,
                    ref zoomVelocity,
                    zoomSmoothTime),
                minimumOrthographicSize,
                maximumOrthographicSize);
        }

        private void ApplyTiltWithZoom()
        {
            if (!tiltWithZoom)
            {
                targetCamera.transform.localPosition = initialCameraLocalPosition;
                targetCamera.transform.localRotation = initialCameraLocalRotation;
                return;
            }

            float zoomProgress = Mathf.InverseLerp(
                minimumOrthographicSize,
                maximumOrthographicSize,
                targetCamera.orthographicSize);
            float pitch = Mathf.Lerp(nearZoomPitch, farZoomPitch, zoomProgress);
            Vector3 preservedEulerAngles = initialCameraLocalRotation.eulerAngles;
            Quaternion localRotation = Quaternion.Euler(
                pitch,
                preservedEulerAngles.y,
                preservedEulerAngles.z);

            targetCamera.transform.localRotation = localRotation;
            targetCamera.transform.localPosition = -(localRotation * Vector3.forward) * cameraOrbitDistance;
        }

        private void OnValidate()
        {
            movementSpeed = Mathf.Max(0f, movementSpeed);
            rotationSpeed = Mathf.Max(0f, rotationSpeed);
            zoomSensitivity = Mathf.Max(0f, zoomSensitivity);
            zoomSmoothTime = Mathf.Max(0.001f, zoomSmoothTime);
            minimumOrthographicSize = Mathf.Max(0.01f, minimumOrthographicSize);
            maximumOrthographicSize = Mathf.Max(minimumOrthographicSize, maximumOrthographicSize);
            nearZoomPitch = Mathf.Clamp(nearZoomPitch, 1f, 89f);
            farZoomPitch = Mathf.Clamp(farZoomPitch, 1f, 89f);

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
