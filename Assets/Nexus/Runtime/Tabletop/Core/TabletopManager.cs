using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace Nexus
{
    public class TabletopManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera mainCamera;

        [Header("Movement Settings")]
        [SerializeField] private float baseDistance = 5f;
        [SerializeField] private float scrollSpeed = 2f;
        [SerializeField] private float rotationSpeed = 100f;
        [SerializeField] private float pickupForce = 10f;
        [SerializeField] private float moveSmoothness = 25f;
        [SerializeField] private float groundSnapOffset = 0.02f;
        [SerializeField] private LayerMask moveSurfaceMask = ~0; // all layers by default
        [SerializeField] private float minForwardDistance = 1.5f;
        [SerializeField] private float maxForwardDistance = 6f;
        [SerializeField] private float maxGroundProbeDistance = 25f;
        [SerializeField] private float maxMouseRayDistance = 30f;
        [SerializeField, Range(0f,1f)] private float minGroundNormalY = 0.4f;
        [SerializeField] private float yMaxStepPerSecond = 8f;
        [SerializeField] private float heightScrollSpeed = 4f;
        [SerializeField] private float heightKeySpeed = 2f;
        [SerializeField] private float minHeightOffset = -5f;
        [SerializeField] private float maxHeightOffset = 5f;

        private Transform selectedObject;
        private Rigidbody selectedRigidbody;

        private bool isDragging = false;
        private bool isLocked = false;
        private float currentDistance;
        private Quaternion targetRotation;
        private bool wasKinematic;
        private bool hadGravity;
        public bool InputLocked { get; set; } = false;

        private Stack<UndoAction> undoStack = new();
        private GameObject copiedObject;
        
        // Network throttling
        private float lastNetworkMoveTime = 0f;
        private float networkMoveInterval = 0.02f;
        private Vector3 lastValidSurfacePoint = Vector3.zero;
        private float pivotToBottomOffset = 0f;
        private float tokenHalfHeight = 0f;
        private Plane dragPlane;
        private bool dragPlaneActive = false;
        private float dragHeightOffset = 0f;

        private void Start()
        {
            if (mainCamera == null && Nexus.CameraManager.Instance != null)
                mainCamera = Nexus.CameraManager.Instance.MainCamera;
        }

        private Nexus.Networking.NetworkPlayer boundLocalPlayer;
        public void BindLocalPlayer(Nexus.Networking.NetworkPlayer player) { boundLocalPlayer = player; }

        private void Update()
        {
            // Always ensure we have the correct camera from CameraManager
            if (Nexus.CameraManager.Instance != null)
            {
                mainCamera = Nexus.CameraManager.Instance.MainCamera;
            }
            
            if (mainCamera == null) return;

            HandleSelection();
            HandleObjectManipulation();
            if (isDragging) MoveSelectedObject();
            HandleUndo();
            HandleCopyPaste();
            HandleDelete();
        }


        // ===============================
        // ======== SELECTION ============
        // ===============================
        private void HandleSelection()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                RaycastHit[] hits = Physics.RaycastAll(ray, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                Transform bestTarget = null;
                bool bestIsToken = false;
                bool bestIsMovable = false;
                RaycastHit bestHit = new RaycastHit();

                // Prefer tokens first
                for (int i = 0; i < hits.Length && bestTarget == null; i++)
                {
                    var token = hits[i].transform.GetComponentInParent<Nexus.Networking.NetworkedToken>();
                    if (token != null)
                    {
                        bestTarget = token.transform;
                        bestIsToken = true;
                        bestHit = hits[i];
                        break;
                    }
                }
                // Then Movable parents
                if (bestTarget == null)
                {
                    for (int i = 0; i < hits.Length && bestTarget == null; i++)
                    {
                        Transform walker = hits[i].transform;
                        while (walker != null)
                        {
                            if (walker.CompareTag("Movable"))
                            {
                                bestTarget = walker;
                                bestIsMovable = true;
                                bestHit = hits[i];
                                break;
                            }
                            walker = walker.parent;
                        }
                    }
                }

                if (bestTarget != null)
                {
                    if (selectedObject != bestTarget)
                    {
                        Deselect();
                        Select(bestTarget);
                        SaveStateAndStartDragging(bestHit);
                    }
                    else
                    {
                        SaveStateAndStartDragging(bestHit);
                    }
                    Debug.Log($"Selected: {bestTarget.name} (Token: {bestIsToken}, Movable: {bestIsMovable})");
                }
                else
                {
                    Deselect();
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                if (isDragging)
                {
                    StopDragging();
                }
            }
        }

        // ===============================
        // ======= MOVEMENT / ROTATE =====
        // ===============================
        private void HandleObjectManipulation()
        {
            if (selectedObject == null)
                return;

            if (Input.GetKeyDown(KeyCode.L))
            {
                var netToken = selectedObject != null ? selectedObject.GetComponentInParent<Nexus.Networking.NetworkedToken>() : null;
                if (netToken != null)
                {
                    netToken.CmdSetLocked(!netToken.IsLocked);
                }
            }

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            
            // Distance adjustment (scroll sem modificadores)
            if (!Input.GetKey(KeyCode.R) && Mathf.Abs(scroll) > 0.001f)
            {
                currentDistance = Mathf.Clamp(currentDistance - scroll * scrollSpeed, 1f, 50f);
            }

            // Rotation (R + Scroll)
            if (Input.GetKey(KeyCode.R) && Mathf.Abs(scroll) > 0.001f)
            {
                Quaternion oldRotation = selectedObject.rotation;
                float rotAmount = scroll * rotationSpeed;
                targetRotation *= Quaternion.Euler(Vector3.up * rotAmount);
                
                undoStack.Push(new UndoAction
                {
                    actionType = UndoActionType.Rotate,
                    targetObject = selectedObject.gameObject,
                    oldRotation = oldRotation,
                    newRotation = targetRotation
                });
            }
        }

        private void MoveSelectedObject()
        {
            // Free 3D movement: mouse position at current distance from camera
            Vector3 mouseScreenPos = new Vector3(Input.mousePosition.x, Input.mousePosition.y, currentDistance);
            Vector3 targetPos = mainCamera.ScreenToWorldPoint(mouseScreenPos);
            // Preserve current Y so TokenSetup handles vertical snap to ground exclusively
            if (selectedObject != null)
                targetPos.y = selectedObject.position.y;
            
            if (selectedRigidbody != null)
            {
                // Direct position update for kinematic rigidbody
                selectedRigidbody.MovePosition(targetPos);
                selectedRigidbody.MoveRotation(targetRotation);
            }
            else if (selectedObject != null)
            {
                selectedObject.position = targetPos;
                selectedObject.rotation = targetRotation;
            }
        }

        // ===============================
        // ========= UNDO (CTRL+Z) =======
        // ===============================
        private void HandleUndo()
        {
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Z))
            {
                if (undoStack.Count > 0)
                {
                    UndoAction action = undoStack.Pop();
                    action.Undo();
                }
            }
        }

        // ===============================
        // ===== COPY/PASTE (CTRL+C/V) ===
        // ===============================
        private void HandleCopyPaste()
        {
            // Copy (Ctrl+C)
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.C))
            {
                if (selectedObject != null)
                {
                    copiedObject = selectedObject.gameObject;
                    Debug.Log("Copied: " + copiedObject.name);
                }
            }

            // Paste (Ctrl+V)
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.V))
            {
                if (copiedObject != null)
                {
                    // Check if this is a networked token
                    var networkedToken = copiedObject.GetComponent<Nexus.Networking.NetworkedToken>();
                    if (networkedToken != null && (Mirror.NetworkServer.active || Mirror.NetworkClient.isConnected))
                    {
                        // Calculate spawn position on client side using local camera
                        Vector3 spawnPos = FindValidSpawnPosition();
                        
                        // Use client->server command via local NetworkPlayer
                        var localPlayerIdentity = Mirror.NetworkClient.localPlayer;
                        var localPlayer = localPlayerIdentity != null
                            ? localPlayerIdentity.GetComponent<Nexus.Networking.NetworkPlayer>()
                            : FindLocalNetworkPlayer();
                        if (localPlayer != null)
                        {
                            string prefabName = SanitizePrefabName(copiedObject.name);
                            localPlayer.CmdSpawnTokenByName(prefabName, spawnPos);
                            Debug.Log("Network spawned: " + prefabName);
                        }
                    }
                    else
                    {
                        // Original local spawning for non-networked objects
                        Vector3 spawnPos = FindValidSpawnPosition();
                        GameObject newObj = Instantiate(copiedObject, spawnPos, copiedObject.transform.rotation);
                        
                        undoStack.Push(new UndoAction
                        {
                            actionType = UndoActionType.Create,
                            targetObject = newObj
                        });
                        
                        Debug.Log("Pasted: " + newObj.name);
                    }
                }
            }
        }

        private Vector3 FindValidSpawnPosition()
        {
            // Simple spawn: 3 units in front of camera
            return mainCamera.transform.position + mainCamera.transform.forward * 3f;
        }

        // ===============================
        // ======== DELETE (DEL) =========
        // ===============================
        private void HandleDelete()
        {
            if (Input.GetKeyDown(KeyCode.Delete))
            {
                if (selectedObject != null)
                {
                    GameObject objToDelete = selectedObject.gameObject;
                    Vector3 oldPos = objToDelete.transform.position;
                    Quaternion oldRot = objToDelete.transform.rotation;
                    
                    undoStack.Push(new UndoAction
                    {
                        actionType = UndoActionType.Delete,
                        targetObject = objToDelete,
                        oldPosition = oldPos,
                        oldRotation = oldRot
                    });
                    
                    Deselect();
                    objToDelete.SetActive(false);
                }
            }
        }

        // ===============================
        // ========= UTILITIES ===========
        // ===============================
        private void Select(Transform obj)
        {
            // Prefer the transform that actually holds the Rigidbody
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb == null) rb = obj.GetComponentInParent<Rigidbody>();
            if (rb == null) rb = obj.GetComponentInChildren<Rigidbody>();

            if (rb != null)
            {
                selectedRigidbody = rb;
                selectedObject = rb.transform;
                targetRotation = rb.transform.rotation;
            }
            else
            {
                selectedObject = obj;
                selectedRigidbody = null;
                targetRotation = obj.rotation;
            }
        }

        private void Deselect()
        {
            if (isDragging)
                StopDragging();
            
            selectedObject = null;
            selectedRigidbody = null;
            isDragging = false;
            isLocked = false;
        }

        private void SaveStateAndStartDragging(RaycastHit hit)
        {
            isDragging = true;
            
            Vector3 oldPos = selectedObject.position;
            Quaternion oldRot = selectedObject.rotation;
            
            // Use camera-forward depth so ScreenToWorldPoint doesn't jump to camera height
            currentDistance = Mathf.Max(0.5f, Vector3.Dot(selectedObject.position - mainCamera.transform.position, mainCamera.transform.forward));
            pivotToBottomOffset = ComputePivotToBottom(selectedObject);
            tokenHalfHeight = ComputeHalfHeight(selectedObject);
            // Build a stable drag plane from initial hit
            Vector3 planeNormal = hit.normal.y >= minGroundNormalY ? hit.normal : Vector3.up;
            dragPlane = new Plane(planeNormal, hit.point);
            dragPlaneActive = true;
            // Initialize free height offset so there is no jump when starting the drag
            dragHeightOffset = selectedObject.position.y - (hit.point.y + tokenHalfHeight + groundSnapOffset);
            
            if (selectedRigidbody != null)
            {
                var identity = selectedObject.GetComponentInParent<Mirror.NetworkIdentity>();
                bool networkActive = Mirror.NetworkServer.active || Mirror.NetworkClient.isConnected;
                bool identitySpawned = identity != null && identity.netId != 0 && (identity.isClient || identity.isServer);
                wasKinematic = selectedRigidbody.isKinematic;
                hadGravity = selectedRigidbody.useGravity;
                
                // Make rigidbody kinematic during drag for precise control
                selectedRigidbody.velocity = Vector3.zero;
                selectedRigidbody.angularVelocity = Vector3.zero;
                selectedRigidbody.isKinematic = true;
                selectedRigidbody.useGravity = false;
                
                if (identitySpawned && networkActive)
                {
                    var netToken = selectedObject.GetComponentInParent<Nexus.Networking.NetworkedToken>();
                    if (netToken != null)
                    {
                        if (netToken.netIdentity == null)
                        {
                            Debug.LogWarning($"Skip CmdBeginDrag on {netToken.name} because NetworkedToken.netIdentity is null (object not fully spawned?)");
                        }
                        else
                        {
                            netToken.SetLocalDragOwner(true);
                            netToken.CmdBeginDrag();
                        }
                    }
                    else
                    {
                        var netMovable = selectedObject.GetComponentInParent<Nexus.Networking.NetworkedMovable>();
                        if (netMovable != null)
                        {
                            if (netMovable.netIdentity == null)
                            {
                                Debug.LogWarning($"Skip CmdBeginDrag on {netMovable.name} because NetworkedMovable.netIdentity is null (object not fully spawned?)");
                            }
                            else
                            {
                                netMovable.SetLocalDragOwner(true);
                                netMovable.CmdBeginDrag();
                            }
                        }
                    }
                }
            }
            
            undoStack.Push(new UndoAction
            {
                actionType = UndoActionType.Move,
                targetObject = selectedObject.gameObject,
                oldPosition = oldPos,
                oldRotation = oldRot
            });
        }

        private void StopDragging()
        {
            isDragging = false;

            if (selectedRigidbody != null)
            {
                selectedRigidbody.isKinematic = wasKinematic;
                selectedRigidbody.useGravity = hadGravity;
                selectedRigidbody.velocity = Vector3.zero;
                selectedRigidbody.angularVelocity = Vector3.zero;
            }

            var netToken = selectedObject != null ? selectedObject.GetComponentInParent<Nexus.Networking.NetworkedToken>() : null;
            bool networkActive = Mirror.NetworkServer.active || Mirror.NetworkClient.isConnected;
            var identity = selectedObject != null ? selectedObject.GetComponentInParent<Mirror.NetworkIdentity>() : null;
            bool identitySpawned = identity != null && identity.netId != 0 && (identity.isClient || identity.isServer);
            if (netToken != null && netToken.netIdentity == null)
            {
                Debug.LogWarning($"Skip CmdEndDragFinal on {netToken.name} because NetworkedToken.netIdentity is null (object not fully spawned?)");
            }
            else if (netToken != null && networkActive && identitySpawned)
            {
                var root = identity != null ? identity.transform : selectedObject;
                netToken.CmdEndDragFinal(root.position, root.rotation);
            }
            else if (networkActive && identitySpawned)
            {
                var netMovable = selectedObject != null ? selectedObject.GetComponentInParent<Nexus.Networking.NetworkedMovable>() : null;
                if (netMovable != null)
                {
                    var root = identity != null ? identity.transform : selectedObject;
                    netMovable.CmdEndDragFinal(root.position, root.rotation);
                }
            }
            dragPlaneActive = false;
            dragHeightOffset = 0f;
        }



        private Nexus.Networking.NetworkPlayer FindLocalNetworkPlayer()
        {
            var players = Object.FindObjectsOfType<Nexus.Networking.NetworkPlayer>();
            foreach (var p in players)
            {
                if (p.isLocalPlayer) return p;
            }
            return null;
        }

        private string SanitizePrefabName(string instanceName)
        {
            if (string.IsNullOrEmpty(instanceName)) return instanceName;
            const string cloneSuffix = "(Clone)";
            if (instanceName.EndsWith(cloneSuffix))
            {
                return instanceName.Substring(0, instanceName.Length - cloneSuffix.Length).Trim();
            }
            return instanceName;
        }

        private float ComputePivotToBottom(Transform root)
        {
            var cols = root.GetComponentsInChildren<Collider>();
            if (cols == null || cols.Length == 0) return 0f;
            Bounds b = cols[0].bounds;
            for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
            // Return bottomY - pivotY (usually negative when pivot is above bottom)
            return b.min.y - root.position.y;
        }

        private float ComputeHalfHeight(Transform root)
        {
            var cols = root.GetComponentsInChildren<Collider>();
            if (cols == null || cols.Length == 0) return 0f;
            Bounds b = cols[0].bounds;
            for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
            return b.extents.y;
        }
    }

    // ===============================
    // ======= UNDO SYSTEM ===========
    // ===============================
    public enum UndoActionType
    {
        Move,
        Rotate,
        Create,
        Delete
    }

    public class UndoAction
    {
        public UndoActionType actionType;
        public GameObject targetObject;
        public Vector3 oldPosition;
        public Vector3 newPosition;
        public Quaternion oldRotation;
        public Quaternion newRotation;

        public void Undo()
        {
            if (targetObject == null)
                return;

            switch (actionType)
            {
                case UndoActionType.Move:
                    targetObject.transform.position = oldPosition;
                    targetObject.transform.rotation = oldRotation;
                    break;

                case UndoActionType.Rotate:
                    targetObject.transform.rotation = oldRotation;
                    break;

                case UndoActionType.Create:
                    Object.Destroy(targetObject);
                    break;

                case UndoActionType.Delete:
                    targetObject.SetActive(true);
                    targetObject.transform.position = oldPosition;
                    targetObject.transform.rotation = oldRotation;
                    break;
            }
        }
    }
}