using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nexus.UI.MainMenu
{

    public class MainMenuBackgroundController : MonoBehaviour
    {
        [Header("Mouse Parallax Settings")]
        [Tooltip("Enable mouse-reactive parallax movement")]
        [SerializeField] private bool enableMouseParallax = true;
        
        [Tooltip("How smoothly the parallax follows the mouse (lower = smoother)")]
        [SerializeField] private float parallaxSmoothness = 3f;
        
        [Header("Parallax Intensities")]
        [Tooltip("Parallax intensity for Sky (Furthest Back)")]
        [SerializeField] private float skyIntensity = 2f;

        [Tooltip("Parallax intensity for Rocks")]
        [SerializeField] private float rocksIntensity = 10f;

        [Tooltip("Parallax intensity for Portal Cliff")]
        [SerializeField] private float portalCliffIntensity = 20f;

        [Tooltip("Parallax intensity for Portal Debris")]
        [SerializeField] private float portalDebrisIntensity = 20f;

        [Tooltip("Parallax intensity for Debris (Closest Front)")]
        [SerializeField] private float debrisIntensity = 40f;

        [Header("Floating Animation Settings")]
        [Tooltip("Vertical float amplitude for Portal Debris")]
        [SerializeField] private float portalDebrisFloatAmplitude = 15f;
        
        [Tooltip("Float speed for Portal Debris")]
        [SerializeField] private float portalDebrisFloatSpeed = 0.5f;

        [Tooltip("Vertical float amplitude for Debris")]
        [SerializeField] private float debrisFloatAmplitude = 20f;

        [Tooltip("Float speed for Debris")]
        [SerializeField] private float debrisFloatSpeed = 0.4f;

        [Header("Particle System Settings")]
        [Tooltip("Number of particles to generate")]
        [SerializeField] private int particleCount = 50;
        
        [Tooltip("Minimum particle speed")]
        [SerializeField] private float minParticleSpeed = 10f;
        
        [Tooltip("Maximum particle speed")]
        [SerializeField] private float maxParticleSpeed = 30f;
        
        [Tooltip("Minimum particle size")]
        [SerializeField] private float minParticleSize = 10f;
        
        [Tooltip("Maximum particle size")]
        [SerializeField] private float maxParticleSize = 30f;
        
        [Tooltip("Parallax intensity for Particles Layer")]
        [SerializeField] private float particlesIntensity = 15f;


        private UIDocument _doc;
        private VisualElement _root;
        
        private VisualElement _sky;
        private VisualElement _rocks;
        private VisualElement _particlesLayer;
        private VisualElement _portalCliff;
        private VisualElement _portalDebris;
        private VisualElement _debris;
        

        private class ParticleState
        {
            public VisualElement Element;
            public float Speed;
            public float OriginalOpacity;
            public float Phase; // For pulsing
            public float NormalizedY; // 0-1 (Top-Bottom)
        }
        
        private List<ParticleState> _particles;
        

        private Vector2 _targetOffset;
        private Vector2 _currentOffset;
        
        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            if (_doc == null)
            {
                Debug.LogError("[MainMenuBackgroundController] No UIDocument found on this GameObject.");
                return;
            }
            
            _root = _doc.rootVisualElement;
            

            _sky = _root.Q<VisualElement>("Sky");
            _rocks = _root.Q<VisualElement>("Rocks");
            _particlesLayer = _root.Q<VisualElement>("ParticlesLayer");
            _portalCliff = _root.Q<VisualElement>("PortalCliff");
            _portalDebris = _root.Q<VisualElement>("PortalDebris");
            _debris = _root.Q<VisualElement>("Debris");
            

            if (_particlesLayer != null)
            {
                InitializeParticles();
            }
            

            if (enableMouseParallax && _root != null)
            {
                _root.RegisterCallback<MouseMoveEvent>(OnMouseMove);
                _root.RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
            }
        }

        private void InitializeParticles()
        {
            if (_particlesLayer == null)
            {
                Debug.LogError("ParticlesLayer is null in InitializeParticles!");
                return;
            }

            Debug.Log($"Initializing {particleCount} particles...");
            _particles = new List<ParticleState>(particleCount);
            _particlesLayer.Clear();
            
            for (int i = 0; i < particleCount; i++)
            {
                var particle = new VisualElement();
                particle.AddToClassList("particle");
                

                float size = Random.Range(minParticleSize, maxParticleSize);
                particle.style.width = size;
                particle.style.height = size;
                

                float x = Random.Range(0f, 100f);
                float y = Random.Range(0f, 100f);
                particle.style.left = new Length(x, LengthUnit.Percent);
                particle.style.top = new Length(y, LengthUnit.Percent);
                

                float opacity = Random.Range(0.4f, 0.8f);
                particle.style.opacity = opacity;
                
                _particlesLayer.Add(particle);
                
                _particles.Add(new ParticleState
                {
                    Element = particle,
                    Speed = Random.Range(minParticleSpeed, maxParticleSpeed),
                    OriginalOpacity = opacity,
                    Phase = Random.Range(0f, Mathf.PI * 2),
                    NormalizedY = y / 100f
                });
            }
            Debug.Log($"Added {_particlesLayer.childCount} particles to layer.");
        }

        private void OnDisable()
        {

            if (_root != null)
            {
                _root.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
                _root.UnregisterCallback<MouseLeaveEvent>(OnMouseLeave);
            }
        }

        private void Update()
        {

            if (_particles != null)
            {
                UpdateParticles();
            }


            float portalDebrisY = Mathf.Sin(Time.time * portalDebrisFloatSpeed) * portalDebrisFloatAmplitude;
            float debrisY = Mathf.Sin(Time.time * debrisFloatSpeed + 1f) * debrisFloatAmplitude;

            if (!enableMouseParallax)
            {

                ApplyParallax(_portalDebris, Vector2.zero, 0f, 0f, portalDebrisY);
                ApplyParallax(_debris, Vector2.zero, 0f, 0f, debrisY);
                ApplyParallax(_particlesLayer, Vector2.zero, 0f);
                return;
            }
            

            _currentOffset = Vector2.Lerp(_currentOffset, _targetOffset, Time.deltaTime * parallaxSmoothness);
            


            ApplyParallax(_sky, _currentOffset, skyIntensity);
            ApplyParallax(_rocks, _currentOffset, rocksIntensity);
            ApplyParallax(_particlesLayer, _currentOffset, particlesIntensity);
            ApplyParallax(_portalCliff, _currentOffset, portalCliffIntensity);
            

            ApplyParallax(_portalDebris, _currentOffset, portalDebrisIntensity, 0f, portalDebrisY);
            ApplyParallax(_debris, _currentOffset, debrisIntensity, 0f, debrisY);
        }

        private void UpdateParticles()
        {
            float dt = Time.deltaTime;
            float height = _root.resolvedStyle.height;
            

            if (float.IsNaN(height) || height <= 0) height = 1080f; 
            
            for (int i = 0; i < _particles.Count; i++)
            {
                var state = _particles[i];
                


                float normalizedSpeed = state.Speed / height;
                state.NormalizedY -= normalizedSpeed * dt;
                

                if (state.NormalizedY < -0.1f)
                {
                    state.NormalizedY = 1.1f;

                    state.Element.style.left = new Length(Random.Range(0f, 100f), LengthUnit.Percent);
                }
                

                state.Element.style.top = new Length(state.NormalizedY * 100f, LengthUnit.Percent);
                

                float pulse = Mathf.Sin(Time.time * 2f + state.Phase) * 0.2f;
                state.Element.style.opacity = Mathf.Clamp01(state.OriginalOpacity + pulse);
            }
        }


        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (_root == null) return;
            

            Vector2 mousePos = evt.localMousePosition;
            Vector2 center = new Vector2(_root.resolvedStyle.width / 2f, _root.resolvedStyle.height / 2f);
            

            _targetOffset = new Vector2(
                (mousePos.x - center.x) / center.x,
                (mousePos.y - center.y) / center.y
            );
        }


        private void OnMouseLeave(MouseLeaveEvent evt)
        {
            _targetOffset = Vector2.zero;
        }


        private void ApplyParallax(VisualElement layer, Vector2 offset, float intensity, float extraX = 0f, float extraY = 0f)
        {
            if (layer == null) return;
            

            float x = (-offset.x * intensity) + extraX;
            float y = (-offset.y * intensity) + extraY;
            
            layer.style.translate = new Translate(x, y, 0);
        }
    }
}

