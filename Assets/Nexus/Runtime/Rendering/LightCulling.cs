using UnityEngine;

/*Script to disable lighting and shadows 
when moving away at a set distance
MODIFICADO: Encontra a câmera automaticamente*/
namespace Nexus
{
    public class LightCulling : MonoBehaviour
    {
        [Header("Camera Settings")]
        [Tooltip("Nome exato da câmera (deixe vazio para buscar automaticamente)")]
        [SerializeField] private string cameraName = "Controller Camera";
        
        [Tooltip("Se não encontrar por nome, usar tag (deixe vazio para não usar)")]
        [SerializeField] private string cameraTag = "MainCamera";
        
        [Header("Culling Distances")]
        [SerializeField] private float shadowCullingDistance = 15f;
        [SerializeField] private float lightCullingDistance = 30f;
        
        [Header("Shadow Settings")]
        public bool enableShadows = false;
        
        [Header("Performance")]
        [Tooltip("Atualizar a cada X frames (1 = todo frame, 5 = a cada 5 frames)")]
        [Range(1, 10)]
        [SerializeField] private int updateFrequency = 3;
        
        [Header("Debug")]
        [Tooltip("Mostrar warnings se não encontrar a câmera?")]
        [SerializeField] private bool showDebugWarnings = false;
        
        // Cache
        private Light _light;
        private int frameCounter;
        private float randomOffset;

        private void Awake()
        {
            _light = GetComponent<Light>();
            
            // Offset aleatório para não atualizar todas as 146 luzes no mesmo frame
            randomOffset = Random.Range(0, updateFrequency);
            frameCounter = Mathf.RoundToInt(randomOffset);
        }

        private void Start()
        {
            // No initialization needed for camera
        }

        private void Update()
        {
            // Otimização: não atualiza todo frame
            frameCounter++;
            if (frameCounter < updateFrequency)
                return;
            
            frameCounter = 0;
            
            GameObject targetCam = null;
            if (Nexus.CameraManager.Instance != null && Nexus.CameraManager.Instance.MainCamera != null)
            {
                targetCam = Nexus.CameraManager.Instance.MainCamera.gameObject;
            }
            
            // Se a câmera não foi encontrada, desabilita a luz e retorna
            if (targetCam == null)
            {
                _light.enabled = false;
                return;
            }

            // Calcula a distância entre a câmera e a luz
            float cameraDistance = Vector3.Distance(targetCam.transform.position, transform.position);

            // Gerenciamento de sombras
            if (cameraDistance <= shadowCullingDistance && enableShadows)
            {
                _light.shadows = LightShadows.Soft;
            }
            else
            {
                _light.shadows = LightShadows.None;
            }

            // Gerenciamento de luz
            if (cameraDistance <= lightCullingDistance)
            {
                _light.enabled = true;
            }
            else
            {
                _light.enabled = false;
            }
        }

        // Opcional: Desenha gizmos no editor para visualizar as distâncias
        private void OnDrawGizmosSelected()
        {
            // Esfera amarela = distância de sombra
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, shadowCullingDistance);

            // Esfera vermelha = distância de luz
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, lightCullingDistance);
        }
        
        // Legacy methods removed or deprecated
        public static void SetPlayerCamera(GameObject camera) { }
        public static void ResetCameraCache() { }
    }
}