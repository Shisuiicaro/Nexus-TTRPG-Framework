using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Linq;

/// <summary>
/// Manages dynamic scene loading/unloading.
/// Attach to a GameObject in MainScene and mark as DontDestroyOnLoad.
/// </summary>
public class SceneManager : MonoBehaviour
{
    public static SceneManager Instance { get; private set; }
    
    [Header("Configuration")]
    public SceneConfig config;
    
    [Header("Keyboard Shortcuts")]
    public KeyCode nextSceneKey = KeyCode.N;
    public KeyCode previousSceneKey = KeyCode.P;
    public KeyCode reloadSceneKey = KeyCode.R;
    
    [Header("Debug")]
    public bool showDebugLogs = true;
    
    // State
    private int currentIndex = -1;
    private bool isLoading = false;
    
    // Properties
    public bool IsLoading => isLoading;
    public int CurrentIndex => currentIndex;
    public string CurrentSceneName => currentIndex >= 0 ? config.scenes[currentIndex].sceneName : "";
    public Sprite CurrentThumbnail => currentIndex >= 0 ? config.scenes[currentIndex].thumbnail : null;
    public int TotalScenes => config.scenes.Count;
    
    void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // If not assigned via Inspector, try to load user config from Resources
        if (config == null)
        {
            var loaded = Resources.Load<SceneConfig>("SceneConfig");
            if (loaded != null)
            {
                config = loaded;
            }
        }
        
        // Validate config
        if (config == null || !config.IsValid())
        {
            Debug.LogError("[SceneManager] Invalid configuration!");
            enabled = false;
            return;
        }
        
        Log("Scene Manager initialized");
    }
    
    void Start()
    {
        if (config.autoLoadFirstScene && config.scenes.Count > 0)
        {
            LoadSceneByIndex(0);
        }
    }
    
    void Update()
    {
        if (Input.GetKeyDown(nextSceneKey)) LoadNext();
        if (Input.GetKeyDown(previousSceneKey)) LoadPrevious();
        if (Input.GetKeyDown(reloadSceneKey)) Reload();
    }
    
    public void LoadNext()
    {
        if (isLoading) return;
        int nextIndex = (currentIndex + 1) % config.scenes.Count;
        LoadSceneByIndex(nextIndex);
    }
    
    public void LoadPrevious()
    {
        if (isLoading) return;
        int prevIndex = currentIndex - 1;
        if (prevIndex < 0) prevIndex = config.scenes.Count - 1;
        LoadSceneByIndex(prevIndex);
    }
    
    public void Reload()
    {
        if (isLoading || currentIndex < 0) return;
        LoadSceneByIndex(currentIndex);
    }
    
    public void LoadSceneByIndex(int index)
    {
        if (isLoading || index < 0 || index >= config.scenes.Count) return;
        StartCoroutine(LoadSceneAsync(index));
    }
    
    public void LoadSceneByName(string sceneName)
    {
        int index = config.GetSceneIndex(sceneName);
        if (index >= 0) LoadSceneByIndex(index);
    }
    
    public void UnloadAll()
    {
        if (isLoading || currentIndex < 0) return;
        StartCoroutine(UnloadCurrentScene());
    }
    
    private IEnumerator LoadSceneAsync(int index)
    {
        isLoading = true;
        float startTime = Time.time;
        
        string sceneName = config.scenes[index].sceneName;
        Log($"Loading scene: {sceneName}");
        
        // Unload current scene
        if (currentIndex >= 0)
        {
            string currentScene = config.scenes[currentIndex].sceneName;
            Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(currentScene);
            
            if (scene.isLoaded)
            {
                AsyncOperation unloadOp = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(currentScene);
                if (unloadOp != null)
                {
                    while (!unloadOp.isDone) yield return null;
                }
            }
        }
        
        // Load new scene
        AsyncOperation loadOp = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        
        if (loadOp == null)
        {
            Debug.LogError($"[SceneManager] Failed to load: {sceneName}");
            isLoading = false;
            yield break;
        }
        
        while (!loadOp.isDone) yield return null;
        
        var loadedScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName);
        if (loadedScene.IsValid())
        {
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(loadedScene);
        }
        
        // Minimum load time for smooth transitions
        float elapsed = Time.time - startTime;
        if (elapsed < config.minimumLoadTime)
        {
            yield return new WaitForSeconds(config.minimumLoadTime - elapsed);
        }
        
        currentIndex = index;
        isLoading = false;
        
        Log($"Scene loaded: {sceneName}");
        LogPostLoadDiagnostics();
        
        // Reset light culling cache if exists
        ResetLightCullingCache();
        Resources.UnloadUnusedAssets();
    }
    
    private IEnumerator UnloadCurrentScene()
    {
        isLoading = true;
        
        if (currentIndex >= 0)
        {
            string sceneName = config.scenes[currentIndex].sceneName;
            Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName);
            
            if (scene.isLoaded)
            {
                AsyncOperation unloadOp = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(sceneName);
                if (unloadOp != null)
                {
                    while (!unloadOp.isDone) yield return null;
                }
            }
        }
        
        currentIndex = -1;
        isLoading = false;
        
        Log("All scenes unloaded");
        Resources.UnloadUnusedAssets();
    }
    
    private void ResetLightCullingCache()
    {
        Nexus.LightCulling.ResetCameraCache();
    }
    
    private void Log(string message)
    {
        if (showDebugLogs) Debug.Log($"[SceneManager] {message}");
    }
    
    private void LogPostLoadDiagnostics()
    {
        if (!showDebugLogs) return;
        int camCount = 0;
        int audioListenerCount = 0;
        int lightEnabledCount = 0;
        var cams = Object.FindObjectsOfType<Camera>(true);
        for (int i = 0; i < cams.Length; i++) if (cams[i] != null && cams[i].enabled) camCount++;
        var als = Object.FindObjectsOfType<AudioListener>(true);
        for (int i = 0; i < als.Length; i++) if (als[i] != null && als[i].enabled) audioListenerCount++;
        var lights = Object.FindObjectsOfType<Light>(true);
        for (int i = 0; i < lights.Length; i++) if (lights[i] != null && lights[i].enabled) lightEnabledCount++;
        var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Debug.Log($"[SceneManager] Diagnostics: ActiveScene='{active.name}', Enabled Cameras={camCount}, AudioListeners={audioListenerCount}, Lights={lightEnabledCount}");
    }
}