using System;
using UnityEngine;
using Cinemachine;

public class CameraRigController : MonoBehaviour
{
    [SerializeField] Transform followProxy;
    [SerializeField] Transform lookProxy;
    [SerializeField] GameObject gameplayVCam;
    [SerializeField] Camera targetCamera;

    [Header("Movement")]
    [SerializeField] float positionSmoothTime = 0.25f;
    [SerializeField] float maxMoveSpeed = 100f;

    [Header("Dynamic FOV")]
    [SerializeField] bool dynamicFOV = true;
    [SerializeField] float idleFOV = 60f;
    [SerializeField] float maxFOV = 75f;
    [SerializeField] float speedForMaxFOV = 8f;
    [SerializeField] float fovSmoothTime = 0.2f;

    [Header("Wiggle")]
    [SerializeField] bool enableWiggle = true;
    [SerializeField] float wiggleAmplitude = 0.05f;
    [SerializeField] float wiggleFrequency = 2f;
    [SerializeField] int wiggleSeed = 0;

    Transform currentToken;
    Vector3 tokenFollowOffset = new Vector3(0f, 3f, -6f);
    Vector3 tokenLookOffset = new Vector3(0f, 1.5f, 0f);
    bool followEnabled;
    bool hasStaticFocus;
    Vector3 staticFocusTarget;

    Vector3 desiredPosition;
    Vector3 moveVelocity;
    Vector3 lastProxyPos;
    float currentSpeed;

    float currentFOV;
    float fovVelocity;

    float overrideTimer;
    float overrideDuration;
    float overrideStartFOV;
    float overrideTargetFOV;
    AnimationCurve overrideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    CinemachineVirtualCamera vcam;

    void Awake()
    {
        if (!targetCamera) targetCamera = Camera.main;
        AutoFindReferences();
        if (followProxy) desiredPosition = followProxy.position;
        currentFOV = idleFOV;
        TryCacheVcam();
    }

    void OnValidate()
    {
        if (idleFOV < 1f) idleFOV = 1f;
        if (maxFOV < idleFOV) maxFOV = idleFOV;
        if (speedForMaxFOV < 0.01f) speedForMaxFOV = 0.01f;
    }

    void LateUpdate()
    {
        if (!followProxy) return;

        Vector3 baseTarget = followProxy.position;
        if (currentToken && followEnabled)
        {
            baseTarget = currentToken.position + tokenFollowOffset;
        }
        else if (currentToken && !followEnabled && hasStaticFocus)
        {
            baseTarget = staticFocusTarget;
        }

        Vector3 wiggle = Vector3.zero;
        if (enableWiggle)
        {
            float t = Time.time * wiggleFrequency;
            float ax = Mathf.Sin(t + wiggleSeed * 0.73f);
            float ay = Mathf.Cos(t * 1.31f + wiggleSeed * 1.17f);
            float az = Mathf.Sin(t * 0.91f + wiggleSeed * 2.03f);
            wiggle = new Vector3(ax, ay, az) * wiggleAmplitude;
        }

        Vector3 targetPos = baseTarget + wiggle;
        Vector3 newPos = Vector3.SmoothDamp(followProxy.position, targetPos, ref moveVelocity, positionSmoothTime, maxMoveSpeed, Time.deltaTime);
        followProxy.position = newPos;

        if (lookProxy)
        {
            Vector3 lookTarget = currentToken ? currentToken.position + tokenLookOffset : baseTarget;
            lookProxy.position = Vector3.Lerp(lookProxy.position, lookTarget, 1f - Mathf.Exp(-10f * Time.deltaTime));
        }

        currentSpeed = Time.deltaTime > 0f ? (followProxy.position - lastProxyPos).magnitude / Time.deltaTime : 0f;
        lastProxyPos = followProxy.position;

        UpdateFOV();
    }

    void UpdateFOV()
    {
        float targetFov = idleFOV;
        if (overrideTimer > 0f)
        {
            overrideTimer += Time.deltaTime;
            float t = Mathf.Clamp01(overrideTimer / overrideDuration);
            float k = overrideCurve != null ? overrideCurve.Evaluate(t) : t;
            targetFov = Mathf.LerpUnclamped(overrideStartFOV, overrideTargetFOV, k);
            if (overrideTimer >= overrideDuration) overrideTimer = 0f;
        }
        else if (dynamicFOV)
        {
            float t = Mathf.Clamp01(currentSpeed / speedForMaxFOV);
            targetFov = Mathf.Lerp(idleFOV, maxFOV, t);
        }
        currentFOV = Mathf.SmoothDamp(currentFOV, targetFov, ref fovVelocity, fovSmoothTime);
        ApplyFOV(currentFOV);
    }

    void ApplyFOV(float fov)
    {
        if (vcam != null)
        {
            var lens = vcam.m_Lens;
            lens.FieldOfView = fov;
            vcam.m_Lens = lens;
        }
        else if (targetCamera)
        {
            targetCamera.fieldOfView = fov;
        }
    }

    void TryCacheVcam()
    {
        vcam = null;
        if (!gameplayVCam) return;
        vcam = gameplayVCam.GetComponent<CinemachineVirtualCamera>();
        if (vcam == null)
        {
            vcam = gameplayVCam.GetComponentInChildren<CinemachineVirtualCamera>(true);
        }
    }

    void AutoFindReferences()
    {
        if (!followProxy)
        {
            var t = transform.Find("FollowProxy");
            if (t) followProxy = t;
        }
        if (!lookProxy)
        {
            var t = transform.Find("LookProxy");
            if (t) lookProxy = t;
        }
        if (!gameplayVCam)
        {
            var c = GetComponentInChildren<CinemachineVirtualCamera>(true);
            if (c) gameplayVCam = c.gameObject;
        }
    }

    public void FocusOnToken(CameraTokenTarget token, bool follow)
    {
        if (token == null) return;
        currentToken = token.transform;
        tokenFollowOffset = token.followOffset;
        tokenLookOffset = token.lookOffset;
        followEnabled = follow;
        if (currentToken && !followEnabled)
        {
            staticFocusTarget = currentToken.position + tokenFollowOffset;
            hasStaticFocus = true;
        }
        else
        {
            hasStaticFocus = false;
        }
    }

    public void ClearFocus()
    {
        currentToken = null;
        followEnabled = false;
        hasStaticFocus = false;
    }

    public void SetFollow(bool enabled)
    {
        followEnabled = enabled;
        if (enabled)
        {
            hasStaticFocus = false;
        }
    }

    public bool IsFollowing()
    {
        return followEnabled && currentToken != null;
    }

    public void OverrideFOV(float fov, float duration)
    {
        if (duration <= 0f)
        {
            currentFOV = fov;
            ApplyFOV(currentFOV);
            overrideTimer = 0f;
            return;
        }
        overrideStartFOV = currentFOV;
        overrideTargetFOV = fov;
        overrideDuration = duration;
        overrideTimer = 0.0001f;
    }

    public float GetCurrentSpeed()
    {
        return currentSpeed;
    }
}
