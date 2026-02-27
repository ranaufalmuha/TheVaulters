using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ArenaCameraController : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform mainPlayerTarget;
    [SerializeField] private Transform secondaryTarget;

    [Header("Bias")]
    [SerializeField] private float mainPlayerWeight = 1.7f;
    [SerializeField] private Vector3 framingOffset = new Vector3(0f, 1.2f, 0f);

    [Header("Follow")]
    [SerializeField] private float positionSmoothTime = 0.18f;
    [SerializeField] private float zoomSmoothTime = 0.2f;

    [Header("Zoom")]
    [SerializeField] private float minOrthographicSize = 6.5f;
    [SerializeField] private float maxOrthographicSize = 10.5f;
    [SerializeField] private float soloFocusOrthographicSize = 6f;
    [SerializeField] private float horizontalPadding = 0.65f;
    [SerializeField] private float verticalPadding = 2.75f;

    [Header("World Limits")]
    [SerializeField] private Vector2 minPosition = new Vector2(-2f, 1.5f);
    [SerializeField] private Vector2 maxPosition = new Vector2(2f, 4f);

    private Camera cachedCamera;
    private Vector3 followVelocity;
    private float zoomVelocity;

    private void Awake()
    {
        cachedCamera = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        RefreshTargetsIfMissing();

        if (mainPlayerTarget == null)
        {
            return;
        }

        Vector3 desiredPosition = GetWeightedFocusPoint();
        float desiredSize = GetDesiredOrthographicSize();

        Vector3 currentPosition = transform.position;
        Vector3 nextPosition = Vector3.SmoothDamp(currentPosition, desiredPosition, ref followVelocity, positionSmoothTime);
        nextPosition.x = Mathf.Clamp(nextPosition.x, minPosition.x, maxPosition.x);
        nextPosition.y = Mathf.Clamp(nextPosition.y, minPosition.y, maxPosition.y);
        nextPosition.z = currentPosition.z;
        transform.position = nextPosition;

        if (cachedCamera != null && cachedCamera.orthographic)
        {
            cachedCamera.orthographicSize = Mathf.SmoothDamp(
                cachedCamera.orthographicSize,
                desiredSize,
                ref zoomVelocity,
                zoomSmoothTime);
        }
    }

    private void RefreshTargetsIfMissing()
    {
        if (mainPlayerTarget == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                mainPlayerTarget = playerObject.transform;
            }
        }

        if (secondaryTarget == null)
        {
            GameObject enemyObject = GameObject.FindGameObjectWithTag("Enemy");
            if (enemyObject != null)
            {
                secondaryTarget = enemyObject.transform;
            }
        }
    }

    private Vector3 GetWeightedFocusPoint()
    {
        bool mainAlive = IsTargetAlive(mainPlayerTarget);
        bool secondaryAlive = IsTargetAlive(secondaryTarget);

        if (mainAlive && !secondaryAlive)
        {
            return mainPlayerTarget.position + framingOffset;
        }

        if (!mainAlive && secondaryAlive)
        {
            return secondaryTarget.position + framingOffset;
        }

        if (secondaryTarget == null || !secondaryAlive)
        {
            return mainPlayerTarget.position + framingOffset;
        }

        float secondaryWeight = 1f;
        float totalWeight = Mathf.Max(0.01f, mainPlayerWeight + secondaryWeight);
        Vector3 weightedPosition =
            ((mainPlayerTarget.position * mainPlayerWeight) + (secondaryTarget.position * secondaryWeight)) / totalWeight;

        return weightedPosition + framingOffset;
    }

    private float GetDesiredOrthographicSize()
    {
        bool mainAlive = IsTargetAlive(mainPlayerTarget);
        bool secondaryAlive = IsTargetAlive(secondaryTarget);

        if (cachedCamera == null || !cachedCamera.orthographic)
        {
            return minOrthographicSize;
        }

        if (!mainAlive || !secondaryAlive || secondaryTarget == null)
        {
            return Mathf.Min(maxOrthographicSize, Mathf.Max(1f, soloFocusOrthographicSize));
        }

        float aspect = Mathf.Max(0.01f, cachedCamera.aspect);
        float horizontalDistance = Mathf.Abs(mainPlayerTarget.position.x - secondaryTarget.position.x);
        float verticalDistance = Mathf.Abs(mainPlayerTarget.position.y - secondaryTarget.position.y);

        float sizeFromWidth = (horizontalDistance * (1f + horizontalPadding)) / (2f * aspect);
        float sizeFromHeight = (verticalDistance + verticalPadding) * 0.5f;
        float desiredSize = Mathf.Max(minOrthographicSize, sizeFromWidth, sizeFromHeight);

        return Mathf.Clamp(desiredSize, minOrthographicSize, maxOrthographicSize);
    }

    private static bool IsTargetAlive(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        CharacterStock stock = target.GetComponent<CharacterStock>();
        if (stock == null)
        {
            return true;
        }

        return !stock.IsDead;
    }
}
