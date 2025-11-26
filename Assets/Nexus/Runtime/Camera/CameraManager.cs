using UnityEngine;

namespace Nexus
{
    /// <summary>
    /// Centralized access point for the active player camera.
    /// Eliminates the need for expensive FindObjectsOfType calls.
    /// </summary>
    public class CameraManager : MonoBehaviour
    {
        public static CameraManager Instance { get; private set; }

        [SerializeField] private Camera fallbackCamera;

        private Camera _activeCamera;

        public Camera MainCamera
        {
            get
            {
                if (_activeCamera != null && _activeCamera.isActiveAndEnabled)
                    return _activeCamera;
                
                if (fallbackCamera != null && fallbackCamera.isActiveAndEnabled)
                    return fallbackCamera;

                if (Camera.main != null)
                    return Camera.main;

                return null;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void RegisterCamera(Camera cam)
        {
            if (cam == null) return;
            _activeCamera = cam;
            Debug.Log($"[CameraManager] Registered active camera: {cam.name}");
        }

        public void UnregisterCamera(Camera cam)
        {
            if (_activeCamera == cam)
            {
                _activeCamera = null;
                Debug.Log($"[CameraManager] Unregistered camera: {cam.name}");
            }
        }
    }
}
