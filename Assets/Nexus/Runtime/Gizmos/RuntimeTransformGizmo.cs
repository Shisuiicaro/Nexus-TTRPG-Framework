using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Mirror;
using Nexus.Networking;

namespace Nexus
{
    public class RuntimeTransformGizmo : MonoBehaviour
    {
        public enum GizmoMode { Translate, Rotate, Scale }
        public static RuntimeTransformGizmo Instance { get; private set; }
        public GizmoMode Mode { get; private set; } = GizmoMode.Translate;
        public bool UseLocalSpace { get; set; } = true;
        public Transform Target { get; private set; }

        Canvas canvas;
        RectTransform canvasRect;
        RectTransform root;
        Image xLine;
        Image yLine;
        Image zLine;
        Image centerHandle;
        List<Image> rotateRing = new List<Image>();

        Camera cam;
        float axisLength = 120f;
        float axisThickness = 6f;
        float pickWidth = 14f;
        float centerSize = 18f;
        float ringRadius = 70f;
        float ringThickness = 4f;
        int ringSegments = 64;

        bool dragging;
        HandleType activeHandle;
        Vector2 lastMouse;
        Vector2 pressMouse;
        Vector3 startPos;
        Quaternion startRot;
        Vector3 startScale;
        float targetDepth;

        Rigidbody targetRb;
        bool wasKinematic;
        bool hadGravity;

        enum HandleType { None, MoveX, MoveY, MoveZ, MoveScreen, RotateView, ScaleX, ScaleY, ScaleZ, ScaleUniform }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SetupCanvas();
            BuildUI();
            SetVisible(false);
        }

        void SetupCanvas()
        {
            var go = new GameObject("RuntimeGizmoCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(go);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasRect = go.GetComponent<RectTransform>();
            var es = FindObjectOfType<EventSystem>();
            if (es == null)
            {
                var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                DontDestroyOnLoad(esGo);
            }
            var rootGo = new GameObject("GizmoRoot", typeof(RectTransform));
            rootGo.transform.SetParent(canvas.transform, false);
            root = rootGo.GetComponent<RectTransform>();
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.zero;
        }

        Image CreateLine(string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(axisLength, axisThickness);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        void BuildUI()
        {
            xLine = CreateLine("X", new Color(1, 0.2f, 0.2f, 0.95f));
            yLine = CreateLine("Y", new Color(0.2f, 1, 0.2f, 0.95f));
            zLine = CreateLine("Z", new Color(0.2f, 0.5f, 1f, 0.95f));
            var go = new GameObject("Center", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);
            centerHandle = go.GetComponent<Image>();
            centerHandle.color = new Color(1f, 0.95f, 0.2f, 0.95f);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(centerSize, centerSize);
            for (int i = 0; i < ringSegments; i++)
            {
                var seg = new GameObject("RingSeg" + i, typeof(RectTransform), typeof(Image));
                seg.transform.SetParent(root, false);
                var img = seg.GetComponent<Image>();
                img.color = new Color(1f, 0.95f, 0.2f, 0.8f);
                var srt = seg.GetComponent<RectTransform>();
                srt.anchorMin = new Vector2(0.5f, 0.5f);
                srt.anchorMax = new Vector2(0.5f, 0.5f);
                srt.pivot = new Vector2(0.5f, 0.5f);
                rotateRing.Add(img);
            }
        }

        public void SetTarget(Transform t)
        {
            Target = t;
            targetRb = null;
            if (Target != null)
            {
                var rb = Target.GetComponentInChildren<Rigidbody>();
                if (rb != null && (rb.transform == Target || rb.transform.IsChildOf(Target))) targetRb = rb;
            }
            dragging = false;
            SetVisible(Target != null);
        }

        void SetVisible(bool v)
        {
            if (xLine != null) xLine.gameObject.SetActive(v);
            if (yLine != null) yLine.gameObject.SetActive(v);
            if (zLine != null) zLine.gameObject.SetActive(v);
            if (centerHandle != null) centerHandle.gameObject.SetActive(v);
            for (int i = 0; i < rotateRing.Count; i++) rotateRing[i].gameObject.SetActive(v && Mode == GizmoMode.Rotate);
        }

        void Update()
        {
            if (Target == null) { SetVisible(false); return; }
            if (cam == null)
            {
                var mgr = TabletopManager.GetActive();
                if (mgr != null)
                {
                    var c = Camera.main;
                    cam = c;
                }
                else
                {
                    cam = Camera.main;
                }
                if (cam == null) return;
            }
            if (Input.GetKeyDown(KeyCode.W)) { Mode = GizmoMode.Translate; SetVisible(true); }
            if (Input.GetKeyDown(KeyCode.E)) { Mode = GizmoMode.Rotate; SetVisible(true); }
            if (Input.GetKeyDown(KeyCode.R)) { Mode = GizmoMode.Scale; SetVisible(true); }
            if (Input.GetKeyDown(KeyCode.Q)) { UseLocalSpace = !UseLocalSpace; }
            UpdateRootAndHandles();
            HandleInput();
        }

        void UpdateRootAndHandles()
        {
            Vector3 sp = cam.WorldToScreenPoint(Target.position);
            targetDepth = Mathf.Max(0.1f, Vector3.Dot(Target.position - cam.transform.position, cam.transform.forward));
            Vector2 lp;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out lp);
            root.anchoredPosition = lp;
            Vector2 x2d = Axis2D(AxisWorld(Vector3.right));
            Vector2 y2d = Axis2D(AxisWorld(Vector3.up));
            Vector2 z2d = Axis2D(AxisWorld(Vector3.forward));
            SetLine(xLine.rectTransform, x2d);
            SetLine(yLine.rectTransform, y2d);
            SetLine(zLine.rectTransform, z2d);
            centerHandle.rectTransform.sizeDelta = new Vector2(centerSize, centerSize);
            bool showRing = Mode == GizmoMode.Rotate;
            for (int i = 0; i < rotateRing.Count; i++) rotateRing[i].gameObject.SetActive(showRing);
            if (showRing)
            {
                float circ = 2f * Mathf.PI * ringRadius;
                float segLen = circ / ringSegments;
                for (int i = 0; i < ringSegments; i++)
                {
                    float t = (i / (float)ringSegments) * Mathf.PI * 2f;
                    var rt = rotateRing[i].rectTransform;
                    rt.sizeDelta = new Vector2(segLen, ringThickness);
                    rt.localEulerAngles = new Vector3(0, 0, t * Mathf.Rad2Deg);
                    rt.anchoredPosition = new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * ringRadius;
                }
            }
            xLine.gameObject.SetActive(Mode != GizmoMode.Rotate);
            yLine.gameObject.SetActive(Mode != GizmoMode.Rotate);
            zLine.gameObject.SetActive(Mode != GizmoMode.Rotate);
        }

        void SetLine(RectTransform rt, Vector2 axis2d)
        {
            float angle = Mathf.Atan2(axis2d.y, axis2d.x) * Mathf.Rad2Deg;
            rt.localEulerAngles = new Vector3(0, 0, angle);
            rt.sizeDelta = new Vector2(axisLength, axisThickness);
            rt.anchoredPosition = Vector2.zero;
        }

        Vector3 AxisWorld(Vector3 axis)
        {
            if (UseLocalSpace) return Target.TransformDirection(axis);
            if (axis == Vector3.right) return Vector3.right;
            if (axis == Vector3.up) return Vector3.up;
            return Vector3.forward;
        }

        Vector2 Axis2D(Vector3 axisWorld)
        {
            Vector3 p = Target.position;
            Vector3 p2 = p + axisWorld;
            Vector3 a = cam.WorldToScreenPoint(p);
            Vector3 b = cam.WorldToScreenPoint(p2);
            Vector2 v = (Vector2)(b - a);
            if (v.sqrMagnitude < 0.0001f) v = new Vector2(1, 0);
            return v.normalized;
        }

        void HandleInput()
        {
            Vector2 mouse = Input.mousePosition;
            if (!dragging)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    {
                        activeHandle = PickHandle(mouse);
                        if (activeHandle != HandleType.None)
                        {
                            BeginDrag();
                            lastMouse = mouse;
                            pressMouse = mouse;
                        }
                    }
                }
            }
            else
            {
                Vector2 delta = mouse - lastMouse;
                if (delta.sqrMagnitude > 0f)
                {
                    ApplyDrag(delta, mouse);
                    lastMouse = mouse;
                }
                if (Input.GetMouseButtonUp(0))
                {
                    EndDrag();
                }
            }
        }

        HandleType PickHandle(Vector2 mouse)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mouse, null, out local);
            Vector2 center = root.anchoredPosition;
            Vector2 m = local - center;
            if (Mode == GizmoMode.Translate)
            {
                if (DistanceToSegment(m, Vector2.zero, Axis2D(AxisWorld(Vector3.right)) * axisLength) <= pickWidth) return HandleType.MoveX;
                if (DistanceToSegment(m, Vector2.zero, Axis2D(AxisWorld(Vector3.up)) * axisLength) <= pickWidth) return HandleType.MoveY;
                if (DistanceToSegment(m, Vector2.zero, Axis2D(AxisWorld(Vector3.forward)) * axisLength) <= pickWidth) return HandleType.MoveZ;
                if (Mathf.Abs(m.x) <= centerSize && Mathf.Abs(m.y) <= centerSize) return HandleType.MoveScreen;
            }
            else if (Mode == GizmoMode.Scale)
            {
                if (DistanceToSegment(m, Vector2.zero, Axis2D(AxisWorld(Vector3.right)) * axisLength) <= pickWidth) return HandleType.ScaleX;
                if (DistanceToSegment(m, Vector2.zero, Axis2D(AxisWorld(Vector3.up)) * axisLength) <= pickWidth) return HandleType.ScaleY;
                if (DistanceToSegment(m, Vector2.zero, Axis2D(AxisWorld(Vector3.forward)) * axisLength) <= pickWidth) return HandleType.ScaleZ;
                if (Mathf.Abs(m.x) <= centerSize && Mathf.Abs(m.y) <= centerSize) return HandleType.ScaleUniform;
            }
            else if (Mode == GizmoMode.Rotate)
            {
                float d = m.magnitude;
                if (Mathf.Abs(d - ringRadius) <= Mathf.Max(pickWidth * 0.5f, ringThickness)) return HandleType.RotateView;
            }
            return HandleType.None;
        }

        float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Vector2.Dot(p - a, ab) / ab.sqrMagnitude;
            t = Mathf.Clamp01(t);
            Vector2 q = a + ab * t;
            return (p - q).magnitude;
        }

        void BeginDrag()
        {
            dragging = true;
            startPos = Target.position;
            startRot = Target.rotation;
            startScale = Target.localScale;
            if (targetRb != null)
            {
                wasKinematic = targetRb.isKinematic;
                hadGravity = targetRb.useGravity;
                targetRb.velocity = Vector3.zero;
                targetRb.angularVelocity = Vector3.zero;
                targetRb.isKinematic = true;
                targetRb.useGravity = false;
                var netToken = Target.GetComponentInParent<NetworkedToken>();
                if (netToken != null)
                {
                    netToken.SetLocalDragOwner(true);
                    netToken.CmdBeginDrag();
                }
                else
                {
                    var netMov = Target.GetComponentInParent<NetworkedMovable>();
                    if (netMov != null)
                    {
                        netMov.SetLocalDragOwner(true);
                        netMov.CmdBeginDrag();
                    }
                }
            }
        }

        void EndDrag()
        {
            dragging = false;
            if (targetRb != null)
            {
                targetRb.isKinematic = wasKinematic;
                targetRb.useGravity = hadGravity;
                targetRb.velocity = Vector3.zero;
                targetRb.angularVelocity = Vector3.zero;
                var id = Target.GetComponentInParent<NetworkIdentity>();
                bool networkActive = NetworkServer.active || NetworkClient.isConnected;
                bool identitySpawned = id != null && id.netId != 0 && (id.isClient || id.isServer);
                var netToken = Target.GetComponentInParent<NetworkedToken>();
                if (netToken != null && networkActive && identitySpawned)
                {
                    var rootT = id != null ? id.transform : Target;
                    netToken.CmdEndDragFinal(rootT.position, rootT.rotation);
                }
                else if (networkActive && identitySpawned)
                {
                    var netMov = Target.GetComponentInParent<NetworkedMovable>();
                    if (netMov != null)
                    {
                        var rootT = id != null ? id.transform : Target;
                        netMov.CmdEndDragFinal(rootT.position, rootT.rotation);
                    }
                }
            }
            var mgr = TabletopManager.GetActive();
            if (mgr != null)
            {
                if (activeHandle == HandleType.MoveX || activeHandle == HandleType.MoveY || activeHandle == HandleType.MoveZ || activeHandle == HandleType.MoveScreen)
                {
                    mgr.PushMoveUndo(Target.gameObject, startPos, startRot, Target.position, Target.rotation);
                }
                else if (activeHandle == HandleType.RotateView)
                {
                    mgr.PushRotateUndo(Target.gameObject, startRot, Target.rotation);
                }
            }
            activeHandle = HandleType.None;
        }

        void ApplyDrag(Vector2 delta, Vector2 mouse)
        {
            if (activeHandle == HandleType.MoveX) DoAxisTranslate(Vector3.right, delta);
            else if (activeHandle == HandleType.MoveY) DoAxisTranslate(Vector3.up, delta);
            else if (activeHandle == HandleType.MoveZ) DoAxisTranslate(Vector3.forward, delta);
            else if (activeHandle == HandleType.MoveScreen) DoScreenTranslate(delta);
            else if (activeHandle == HandleType.ScaleX) DoAxisScale(Vector3.right, delta);
            else if (activeHandle == HandleType.ScaleY) DoAxisScale(Vector3.up, delta);
            else if (activeHandle == HandleType.ScaleZ) DoAxisScale(Vector3.forward, delta);
            else if (activeHandle == HandleType.ScaleUniform) DoUniformScale(delta);
            else if (activeHandle == HandleType.RotateView) DoViewRotate(mouse);
        }

        void DoAxisTranslate(Vector3 axis, Vector2 deltaPix)
        {
            Vector2 dir2D = Axis2D(AxisWorld(axis));
            float delta = Vector2.Dot(deltaPix, dir2D);
            Vector3 perPix = WorldPerPixelAlongAxis(AxisWorld(axis), dir2D);
            Vector3 worldDelta = perPix * delta;
            if (targetRb != null) targetRb.MovePosition(Target.position + worldDelta);
            else Target.position += worldDelta;
        }

        void DoScreenTranslate(Vector2 deltaPix)
        {
            Vector3 a = cam.ScreenToWorldPoint(new Vector3(0, 0, targetDepth));
            Vector3 ax = cam.ScreenToWorldPoint(new Vector3(1, 0, targetDepth));
            Vector3 ay = cam.ScreenToWorldPoint(new Vector3(0, 1, targetDepth));
            Vector3 dx = (ax - a);
            Vector3 dy = (ay - a);
            Vector3 worldDelta = dx * deltaPix.x + dy * deltaPix.y;
            if (targetRb != null) targetRb.MovePosition(Target.position + worldDelta);
            else Target.position += worldDelta;
        }

        Vector3 WorldPerPixelAlongAxis(Vector3 axisWorld, Vector2 axis2D)
        {
            Vector3 p = Target.position;
            Vector3 a = cam.WorldToScreenPoint(p);
            Vector3 b = a + new Vector3(axis2D.x, axis2D.y, 0f);
            Vector3 p0 = cam.ScreenToWorldPoint(new Vector3(a.x, a.y, targetDepth));
            Vector3 p1 = cam.ScreenToWorldPoint(new Vector3(b.x, b.y, targetDepth));
            Vector3 d = p1 - p0;
            float m = Vector3.Dot(d, axisWorld.normalized);
            return axisWorld.normalized * m;
        }

        void DoAxisScale(Vector3 axis, Vector2 deltaPix)
        {
            Vector2 dir2D = Axis2D(AxisWorld(axis));
            float s = Vector2.Dot(deltaPix, dir2D) * 0.01f;
            Vector3 localAxis = UseLocalSpace ? axis : Target.InverseTransformDirection(axis);
            Vector3 scale = startScale;
            scale += Vector3.Scale(localAxis.normalized, new Vector3(Mathf.Sign(localAxis.x) * s, Mathf.Sign(localAxis.y) * s, Mathf.Sign(localAxis.z) * s));
            scale.x = Mathf.Max(0.01f, scale.x);
            scale.y = Mathf.Max(0.01f, scale.y);
            scale.z = Mathf.Max(0.01f, scale.z);
            Target.localScale = scale;
        }

        void DoUniformScale(Vector2 deltaPix)
        {
            float s = (deltaPix.x + deltaPix.y) * 0.005f;
            Vector3 scale = startScale * (1f + s);
            scale.x = Mathf.Max(0.01f, scale.x);
            scale.y = Mathf.Max(0.01f, scale.y);
            scale.z = Mathf.Max(0.01f, scale.z);
            Target.localScale = scale;
        }

        void DoViewRotate(Vector2 mouse)
        {
            Vector2 c = root.anchoredPosition;
            Vector2 a;
            Vector2 b;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, lastMouse, null, out a);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mouse, null, out b);
            a -= c;
            b -= c;
            float angA = Mathf.Atan2(a.y, a.x);
            float angB = Mathf.Atan2(b.y, b.x);
            float d = Mathf.DeltaAngle(angA * Mathf.Rad2Deg, angB * Mathf.Rad2Deg);
            Quaternion q = Quaternion.AngleAxis(d, cam.transform.forward);
            if (targetRb != null) targetRb.MoveRotation(q * Target.rotation);
            else Target.rotation = q * Target.rotation;
        }
    }
}
