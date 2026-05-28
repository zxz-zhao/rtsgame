using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Conservative runtime fallback for projects where WASD/arrow camera input works
/// except A. It mirrors the left-arrow movement only when A produced no leftward
/// camera displacement in the current frame.
/// </summary>
[DefaultExecutionOrder(10000)]
public sealed class CameraAKeyLeftAlias : MonoBehaviour
{
    const float MinObservedSpeed = 0.05f;
    const float PositionEpsilon = 0.0001f;

    public float FallbackSpeed = 12f;
    public bool IgnoreWhenAltHeld = true;
    public bool IgnoreWhenUiInputSelected = true;

    Camera targetCamera;
    Vector3 lastPosition;
    Vector3 learnedLeftDirection;
    float learnedSpeed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (FindObjectOfType<CameraAKeyLeftAlias>() != null)
            return;

        var go = new GameObject("CameraAKeyLeftAlias");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<CameraAKeyLeftAlias>();
    }

    void LateUpdate()
    {
        if (!ResolveCamera())
            return;

        Vector3 currentPosition = targetCamera.transform.position;
        Vector3 frameDelta = currentPosition - lastPosition;
        Vector3 leftDirection = GetLeftDirection();

        LearnFromWorkingKeys(frameDelta, leftDirection);

        if (ShouldApplyAKeyFallback(frameDelta, leftDirection))
        {
            float speed = learnedSpeed > MinObservedSpeed ? learnedSpeed : FallbackSpeed;
            targetCamera.transform.position += leftDirection * speed * Time.deltaTime;
            currentPosition = targetCamera.transform.position;
        }

        lastPosition = currentPosition;
    }

    bool ResolveCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
            camera = FindObjectOfType<Camera>();
        if (camera == null)
            return false;

        if (targetCamera != camera)
        {
            targetCamera = camera;
            lastPosition = targetCamera.transform.position;
        }

        return true;
    }

    Vector3 GetLeftDirection()
    {
        if (learnedLeftDirection.sqrMagnitude > 0.1f)
            return learnedLeftDirection.normalized;

        Vector3 left = -Vector3.ProjectOnPlane(targetCamera.transform.right, Vector3.up);
        if (left.sqrMagnitude < 0.0001f)
            left = Vector3.left;

        return left.normalized;
    }

    void LearnFromWorkingKeys(Vector3 frameDelta, Vector3 leftDirection)
    {
        if (Time.deltaTime <= 0f || frameDelta.sqrMagnitude <= PositionEpsilon)
            return;

        bool onlyLeftArrow = Input.GetKey(KeyCode.LeftArrow)
            && !Input.GetKey(KeyCode.A)
            && !Input.GetKey(KeyCode.RightArrow)
            && !Input.GetKey(KeyCode.D)
            && !Input.GetKey(KeyCode.UpArrow)
            && !Input.GetKey(KeyCode.W)
            && !Input.GetKey(KeyCode.DownArrow)
            && !Input.GetKey(KeyCode.S);

        bool onlyRight = (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D))
            && !Input.GetKey(KeyCode.A)
            && !Input.GetKey(KeyCode.LeftArrow)
            && !Input.GetKey(KeyCode.UpArrow)
            && !Input.GetKey(KeyCode.W)
            && !Input.GetKey(KeyCode.DownArrow)
            && !Input.GetKey(KeyCode.S);

        if (!onlyLeftArrow && !onlyRight)
            return;

        Vector3 horizontalDelta = Vector3.ProjectOnPlane(frameDelta, Vector3.up);
        float speed = horizontalDelta.magnitude / Time.deltaTime;
        if (speed <= MinObservedSpeed)
            return;

        learnedSpeed = speed;
        learnedLeftDirection = onlyLeftArrow
            ? horizontalDelta.normalized
            : -horizontalDelta.normalized;
    }

    bool ShouldApplyAKeyFallback(Vector3 frameDelta, Vector3 leftDirection)
    {
        if (!Input.GetKey(KeyCode.A))
            return false;
        if (Input.GetKey(KeyCode.LeftArrow))
            return false;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            return false;
        if (IgnoreWhenAltHeld && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))
            return false;
        if (IgnoreWhenUiInputSelected && IsTextInputSelected())
            return false;

        float existingLeftMotion = Vector3.Dot(Vector3.ProjectOnPlane(frameDelta, Vector3.up), leftDirection);
        return existingLeftMotion < MinObservedSpeed * Time.deltaTime;
    }

    bool IsTextInputSelected()
    {
        if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)
            return false;

        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected.GetComponent<InputField>() != null)
            return true;

        return selected.GetComponent("TMP_InputField") != null;
    }
}
