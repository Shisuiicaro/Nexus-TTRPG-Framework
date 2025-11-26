using UnityEngine;
using Mirror;
using Nexus;
using Cinemachine;
using Tenkoku.Core;

namespace Nexus.Networking
{
    /// <summary>
    /// Network player controller - each player has their own camera and can see all synchronized objects
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour
    {
        [Header("Player Components")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private TabletopManager tabletopManager;

        [Header("Player Info")]
        [SyncVar] public string playerName = "Player";
        [SyncVar] public Color playerColor = Color.white;

        private void Awake()
        {
            // Get or create components
            if (playerCamera == null)
                playerCamera = GetComponentInChildren<Camera>();
            
            if (playerController == null)
                playerController = GetComponent<PlayerController>();
            
            if (tabletopManager == null)
                tabletopManager = GetComponent<TabletopManager>();
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
            
            // Enable camera and controls only for local player
            if (playerCamera != null)
            {
                // Disable any existing MainCamera in the scene (e.g. the lobby camera)
                if (Camera.main != null && Camera.main != playerCamera)
                {
                    Camera.main.gameObject.SetActive(false);
                }

                playerCamera.enabled = true;
                playerCamera.tag = "MainCamera";
                
                // Disable audio listener on non-local cameras
                AudioListener listener = playerCamera.GetComponent<AudioListener>();
                if (listener != null)
                    listener.enabled = true;

                playerCamera.clearFlags = CameraClearFlags.Skybox;
                playerCamera.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
                playerCamera.cullingMask = ~0;
                playerCamera.rect = new Rect(0f, 0f, 1f, 1f);
                playerCamera.targetTexture = null;
                playerCamera.targetDisplay = 0;

#if false
                var urpType = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
                if (urpType != null)
                {
                    var urpData = playerCamera.GetComponent(urpType);
                    if (urpData != null)
                    {
                        var renderTypeProp = urpType.GetProperty("renderType");
                        if (renderTypeProp != null)
                        {
                            renderTypeProp.SetValue(urpData, 0, null);
                        }
                        var stackProp = urpType.GetProperty("cameraStack");
                        if (stackProp != null)
                        {
                            var stack = stackProp.GetValue(urpData, null) as System.Collections.IList;
                            stack?.Clear();
                        }
                        // Try to set default renderer (index 0)
                        var methods = urpType.GetMethods();
                        foreach (var m in methods)
                        {
                            if (m.Name == "SetRenderer" && m.GetParameters().Length == 1)
                            {
                                try { m.Invoke(urpData, new object[] { 0 }); } catch { }
                                break;
                            }
                        }
                        // Disable post-processing and ensure volume mask includes everything
                        var ppProp = urpType.GetProperty("renderPostProcessing");
                        if (ppProp != null)
                        {
                            try { ppProp.SetValue(urpData, false, null); } catch { }
                        }
                        var volMaskProp = urpType.GetProperty("volumeLayerMask");
                        if (volMaskProp != null)
                        {
                            try { volMaskProp.SetValue(urpData, (LayerMask)(~0), null); } catch { }
                        }
                    }
                }
#endif

                // Ensure our camera renders on top
                playerCamera.depth = 100f;

                // Register with CameraManager
                if (Nexus.CameraManager.Instance != null)
                {
                    Nexus.CameraManager.Instance.RegisterCamera(playerCamera);
                }



                var allCameras = Object.FindObjectsOfType<Camera>();
                Debug.Log($"Cameras after local init: count={allCameras.Length}");
                foreach (var cam in allCameras)
                {
                    var owner = cam.GetComponentInParent<NetworkPlayer>();
                    if (cam != playerCamera)
                    {
                        // Disable all cameras that are not the local player's camera
                        if (owner == null || !owner.isLocalPlayer)
                        {
                            cam.enabled = false;
                            var al = cam.GetComponent<AudioListener>();
                            if (al != null) al.enabled = false;
                        }
                    }
                    Debug.Log($" - cam='{cam.name}' enabled={cam.enabled} depth={cam.depth} clear={cam.clearFlags} tag={cam.tag} targetTex={(cam.targetTexture!=null?cam.targetTexture.name:"null")} owner={(owner!=null?(owner.isLocalPlayer?"local":"remote"):"none")} ");
                }

                Debug.Log($"Local camera setup -> enabled={playerCamera.enabled} clear={playerCamera.clearFlags} bg={playerCamera.backgroundColor} mask={playerCamera.cullingMask} rect={playerCamera.rect} targetTexture={(playerCamera.targetTexture != null ? playerCamera.targetTexture.name : "null")} display={playerCamera.targetDisplay}");
            }

            // Bind Tenkoku sky system to this local player's camera
            var tenModules = Object.FindObjectsOfType<TenkokuModule>();
            foreach (var ten in tenModules)
            {
                if (ten == null) continue;
                if (playerCamera != null)
                {
                    ten.mainCamera = playerCamera.transform;
                    ten.manualCamera = playerCamera.transform;
                    ten.useCamera = playerCamera.transform;
                    ten.useCameraCam = playerCamera;
                }
                // Ensure persistent binding even if cameras change later
                if (ten.GetComponent<Nexus.TenkokuCameraBinder>() == null)
                {
                    ten.gameObject.AddComponent<Nexus.TenkokuCameraBinder>();
                }
            }

            if (playerController != null)
            {
                playerController.enabled = true;
            }

            if (tabletopManager != null)
            {
                tabletopManager.enabled = true;
            }

            // Set random player color
            CmdSetPlayerColor(new Color(
                UnityEngine.Random.Range(0.3f, 1f),
                UnityEngine.Random.Range(0.3f, 1f),
                UnityEngine.Random.Range(0.3f, 1f)
            ));

            Debug.Log("Local player initialized");

            // Bind the shared TabletopManager (if present) to this local player
            var sceneManager = Object.FindObjectOfType<Nexus.TabletopManager>();
            if (sceneManager != null)
            {
                sceneManager.BindLocalPlayer(this);
            }

            // Optional: configure a Cinemachine vcam to follow the player pivot
            var vcam = GetComponentInChildren<CinemachineVirtualCamera>(true);
            if (vcam != null)
            {
                if (vcam.Follow == null)
                {
                    var followTarget = playerController != null ? playerController.transform : transform;
                    vcam.Follow = followTarget;
                }
                // Leave LookAt null and use Aim=Do Nothing in the prefab so the camera orientation follows PlayerController
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Disable camera and controls for remote players
            if (!isLocalPlayer)
            {
                if (playerCamera != null)
                {
                    playerCamera.enabled = false;
                    
                    // Disable audio listener on non-local cameras
                    AudioListener listener = playerCamera.GetComponent<AudioListener>();
                    if (listener != null)
                        listener.enabled = false;
                }

                if (playerController != null)
                {
                    playerController.enabled = false;
                }

                if (tabletopManager != null)
                {
                    tabletopManager.enabled = false;
                }
            }
        }

        [Command]
        private void CmdSetPlayerColor(Color color)
        {
            playerColor = color;
        }

        [Command]
        public void CmdSetPlayerName(string name)
        {
            playerName = name;
        }

        // Allow clients to request the server to spawn a token by prefab name
        [Command]
        public void CmdSpawnTokenByName(string prefabName, Vector3 position)
        {
            Nexus.Networking.NetworkSpawner.Instance.SpawnTokenAtPosition(prefabName, position);
        }

        // Allow clients to request the server to move a token by netId
        [Command]
        public void CmdMoveToken(uint netId, Vector3 position)
        {
            if (Mirror.NetworkServer.spawned.TryGetValue(netId, out Mirror.NetworkIdentity identity))
            {
                var token = identity.GetComponent<Nexus.Networking.NetworkedToken>();
                if (token != null)
                {
                    token.ServerMoveTo(position);
                }
                else
                {
                    // Fallback: move transform on server and mirror to all clients
                    identity.transform.position = position;
                    RpcMoveToken(netId, position);
                }
            }
        }

        [ClientRpc]
        private void RpcMoveToken(uint netId, Vector3 position)
        {
            if (Mirror.NetworkClient.spawned.TryGetValue(netId, out Mirror.NetworkIdentity identity))
            {
                identity.transform.position = position;
            }
        }

        // Preferred: pass NetworkIdentity directly so Mirror resolves the correct server object
        [Command]
        public void CmdMoveTokenIdentity(Mirror.NetworkIdentity identity, Vector3 position)
        {
            if (identity == null) return;
            var token = identity.GetComponent<Nexus.Networking.NetworkedToken>();
            if (token != null)
            {
                token.ServerMoveTo(position);
            }
            else
            {
                identity.transform.position = position;
                RpcMoveToken(identity.netId, position);
            }
        }

        private void OnDisable()
        {
            if (isLocalPlayer && playerCamera != null && Nexus.CameraManager.Instance != null)
            {
                Nexus.CameraManager.Instance.UnregisterCamera(playerCamera);
            }
        }
    }
}
