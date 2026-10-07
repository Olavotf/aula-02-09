using UnityEngine;

public class GunAimAtCameraCenter : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The camera used for aiming. If null, Camera.main will be used.")]
    [SerializeField] private Camera aimCamera;

    [Header("Rotation Settings")]
    [Tooltip("Maximum distance for the aim raycast.")]
    [SerializeField] private float maxAimDistance = 1000f;

    [Tooltip("Layers to include in the aim raycast.")]
    [SerializeField] private LayerMask aimLayerMask = ~0;

    [Tooltip("How fast the gun rotates towards the target. Set to 0 for instant rotation.")]
    [SerializeField] private float rotationSpeed = 0f;

    [Tooltip("Optional offset if the gun's forward axis doesn't match its barrel direction.")]
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;

    private void Awake()
    {
        if (aimCamera == null)
            aimCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (aimCamera == null) return;

        // Get the center point of the camera view
        Vector3 aimPoint = GetCameraCenterAimPoint();

        // Rotate the gun to look at that point
        RotateTowards(aimPoint);
    }

    private Vector3 GetCameraCenterAimPoint()
    {
        // Ray from the exact center of the camera (viewport 0.5, 0.5)
        Ray centerRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        // Try to hit something in the world
        if (Physics.Raycast(centerRay, out RaycastHit hit, maxAimDistance, aimLayerMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        // If nothing hit, aim at a point far along the ray
        return centerRay.origin + centerRay.direction * maxAimDistance;
    }

    private void RotateTowards(Vector3 targetPoint)
    {
        Vector3 direction = targetPoint - transform.position;

        // Avoid zero-length direction
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(rotationOffset);

        if (rotationSpeed <= 0f)
        {
            transform.rotation = targetRotation;
        }
        else
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }
}