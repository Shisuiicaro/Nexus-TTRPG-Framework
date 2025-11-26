using UnityEngine;
using Mirror;
using Nexus.Networking;

public class DragObjectOnGround : MonoBehaviour
{
    [Header("Camadas")]
    public LayerMask groundLayer = 0; // se não definido, usa DefaultRaycastLayers

    [Header("Altura e suavização")]
    public float pivotOffsetToFeet = 1f;
    public float rayStartHeight = 2f;
    public float smoothSpeed = 10f;
    public float minGroundNormalY = 0.4f;
    public float maxRayDistance = 1000f;

    [Header("Rotação com scroll")]
    public float rotationSpeed = 200f;

    [Header("Debug")]
    public bool enableDebug = false;

    private Vector3 offset;
    private Camera mainCamera;
    private Camera playerCamera;
    private float lastGroundY;
    private bool hasGroundY = false;
    private TokenSetup tokenSetup;
    private NetworkedToken netToken;
    private Plane dragPlane;
    private bool dragPlaneActive = false;
    private float dragHeightOffset = 0f;
    private float lastNetworkSendTime = 0f;
    public float netSendInterval = 0.03f;

    void Start()
    {
        tokenSetup = GetComponent<TokenSetup>();
        if (Nexus.CameraManager.Instance != null)
            playerCamera = Nexus.CameraManager.Instance.MainCamera;
        if (playerCamera == null) playerCamera = Camera.main; // Fallback if CameraManager not available or camera not set
        netToken = GetComponent<NetworkedToken>();
    }

    private void Update()
    {
        if (Nexus.CameraManager.Instance != null)
            playerCamera = Nexus.CameraManager.Instance.MainCamera;

        if (playerCamera == null) playerCamera = Camera.main; // Fallback if CameraManager not available or camera not set
    }

    void OnMouseDown()
    {
        if (tokenSetup != null) tokenSetup.externalDragActive = true;
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        int mask = groundLayer.value == 0 ? Physics.DefaultRaycastLayers : groundLayer.value;
        RaycastHit[] hits = Physics.RaycastAll(ray, maxRayDistance, mask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        bool found = false;
        RaycastHit best = new RaycastHit();
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.collider == null) continue;
            var ht = h.collider.transform;
            if (ht == transform || ht.IsChildOf(transform)) continue; // ignora próprio collider
            if (h.normal.y < minGroundNormalY) continue; // ignora superfícies muito íngremes
            best = h; found = true; break;
        }
        Vector3 planePoint = found ? best.point : transform.position;
        Vector3 planeNormal = found && best.normal.y >= minGroundNormalY ? best.normal : Vector3.up;
        dragPlane = new Plane(planeNormal, planePoint);
        dragPlaneActive = true;
        // offset estável relativo ao ponto do plano
        if (dragPlane.Raycast(ray, out float d))
        {
            offset = transform.position - ray.GetPoint(d);
        }
        // memoriza diferença de altura inicial
        dragHeightOffset = transform.position.y - (planePoint.y + pivotOffsetToFeet);

        // Rede: inicia drag
        bool networkActive = NetworkServer.active || NetworkClient.isConnected;
        if (networkActive && netToken != null && netToken.netIdentity != null && netToken.netIdentity.netId != 0)
        {
            netToken.SetLocalDragOwner(true);
            netToken.CmdBeginDrag();
            lastNetworkSendTime = 0f;
        }
    }

    void OnMouseDrag()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!(dragPlaneActive && dragPlane.Raycast(ray, out float distance)))
        {
            // fallback simples
            Plane plane = new Plane(Vector3.up, transform.position);
            if (!plane.Raycast(ray, out distance)) return;
            dragPlane = plane;
            dragPlaneActive = true;
        }

        Vector3 targetPos = ray.GetPoint(distance) + offset;
        Vector3 rayStart = new Vector3(targetPos.x, transform.position.y + rayStartHeight, targetPos.z);

        int mask = groundLayer.value == 0 ? Physics.DefaultRaycastLayers : groundLayer.value;
        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, maxRayDistance, mask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        bool found = false;
        RaycastHit best = new RaycastHit();
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.collider == null) continue;
            var ht = h.collider.transform;
            if (ht == transform || ht.IsChildOf(transform)) continue;
            if (h.normal.y < minGroundNormalY) continue;
            best = h; found = true; break;
        }
        if (found)
        {
            float desiredY = best.point.y + pivotOffsetToFeet + dragHeightOffset;
            float k = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
            float smoothY = Mathf.Lerp(transform.position.y, desiredY, k);
            targetPos.y = smoothY;
            lastGroundY = best.point.y;
            hasGroundY = true;
            if (enableDebug)
            {
                Debug.DrawLine(rayStart, best.point, Color.green, 0.1f);
            }
        }
        else
        {
            if (hasGroundY)
                targetPos.y = lastGroundY + pivotOffsetToFeet + dragHeightOffset;
            if (enableDebug)
            {
                Debug.DrawLine(rayStart, rayStart + Vector3.down * 3f, Color.red, 0.1f);
            }
        }

        transform.position = targetPos;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            transform.Rotate(Vector3.up, scroll * rotationSpeed * Time.deltaTime, Space.World);
        }

        // Rede: envia atualização parcial durante o drag (throttle)
        bool networkActive = NetworkServer.active || NetworkClient.isConnected;
        if (networkActive && netToken != null && netToken.netIdentity != null && netToken.netIdentity.netId != 0)
        {
            if (Time.time - lastNetworkSendTime >= netSendInterval)
            {
                netToken.CmdUpdatePosition(transform.position);
                netToken.CmdUpdateRotation(transform.rotation);
                lastNetworkSendTime = Time.time;
            }
        }
    }

    void OnMouseUp()
    {
        if (tokenSetup != null) tokenSetup.externalDragActive = false;
        // Rede: finaliza drag e aplica pose
        bool networkActive = NetworkServer.active || NetworkClient.isConnected;
        if (networkActive && netToken != null && netToken.netIdentity != null && netToken.netIdentity.netId != 0)
        {
            netToken.CmdEndDragFinal(transform.position, transform.rotation);
            netToken.SetLocalDragOwner(false);
        }
        dragPlaneActive = false;
    }
}
