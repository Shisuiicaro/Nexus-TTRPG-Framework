using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class TokenSetup : MonoBehaviour
{
    [Header("Simple Physics Mode")]
    [SerializeField] private bool simplePhysicsMode = false;
    [SerializeField] private float simpleStabilizeSeconds = 1.0f;
    [Header("Physics Settings")]
    [SerializeField] private float mass = 1f;
    [SerializeField] private float drag = 1f;
    [SerializeField] private float angularDrag = 0.5f;
    
    [Header("Scale Settings")]
    [SerializeField] private float minScale = 0.10f;
    [SerializeField] private float maxScale = 4f;
    [SerializeField] private float scaleStep = 0.05f;
    [SerializeField] private float currentScale = 0.80f;
    
    [Header("Sprite Settings")]
    [SerializeField] private bool billboardToCamera = true;
    
    [Header("Directional 4-Way Billboard")]
    [SerializeField] private bool directional4WayBillboard = false;
    [SerializeField] private Sprite directionalFrontSprite;
    [SerializeField] private Sprite directionalBackSprite;
    
    [Header("Proximity Squash Settings")]
    [SerializeField] private bool squashNearCamera = true;
    [SerializeField] private float squashRange = 0.4f;
    [SerializeField] private float squashNearOffset = 0.05f;
    [SerializeField] private float minYScaleAtMaxSquash = 0.2f;
    [SerializeField] private float maxXScaleAtMaxSquash = 1.4f;
    [SerializeField] private float squashLerpSpeed = 10f;
    
    [Header("Ground Snapping")]
    [SerializeField] private bool groundSnap = true;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float snapRayHeight = 2.0f;
    [SerializeField] private float snapMaxDistance = 20f;
    [SerializeField] private float snapOffsetY = 0.0f;
    [SerializeField] private float minGroundNormalY = 0.4f;
    [SerializeField] private float snapLerpSpeed = 20f;
    [SerializeField] private float snapVelocityThreshold = 0.05f;
    [SerializeField] private float snapFootInset = 0.05f;
    [SerializeField] private bool snapOnStart = true;
    private enum SnapSampleMode { CenterPrefer, Median, Highest }
    [SerializeField] private SnapSampleMode snapMode = SnapSampleMode.CenterPrefer;
    [SerializeField] private float snapStepUpMax = 0.35f;
    [SerializeField] private float stepDownMinDrop = 0.35f;
    
    [Header("Token States")]
    [SerializeField] private Sprite defaultSprite;
    [SerializeField] private Sprite alternativeSprite1;
    [SerializeField] private Sprite alternativeSprite2;
    [SerializeField] private Sprite alternativeSprite3;
    [SerializeField] private Sprite alternativeSprite4;
    
    private Rigidbody rb;
    private BoxCollider boxCollider;
    private Camera mainCamera;

    private Transform spriteTransform;
    private SpriteRenderer spriteRenderer;
    private Material spriteMaterial;
    private Vector3 spriteBaseScale = Vector3.one;
    private bool simpleGrounded = false;
    private float simpleClampEndTime = -1f;
    private Vector3 lastSnapPos;
    private float lastSnapTime;
    private bool snapInit;
    public bool externalDragActive = false;
    
    // State tracking
    private int currentState = 1;
    private bool isCenterScreenOver = false;
    private Camera playerCamera;
    private float squashOffset;
    private Vector3 targetScale;
    
    private void Awake()
    {
        SetupComponents();
        
        // Default: dynamic with gravity
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (simplePhysicsMode)
        {
            EnforceSimplePhysics();
            simpleGrounded = false;
            simpleClampEndTime = Time.time + simpleStabilizeSeconds;
        }
    }

    private void FixedUpdate() { }
    
    private void Start()
    {
        if (Nexus.CameraManager.Instance != null)
        {
            mainCamera = Nexus.CameraManager.Instance.MainCamera;
        }
        else
        {
            if (Camera.main != null) mainCamera = Camera.main;
        }
        
        // Store default sprite if not assigned
        if (defaultSprite == null && spriteRenderer != null)
        {
            defaultSprite = spriteRenderer.sprite;
        }
        
        // Setup custom lit material for sprite
        SetupSpriteMaterial();
        
        // Apply initial state and scale
        ApplyState(currentState);
        ApplyScale(currentScale);
        if (spriteTransform != null) { spriteBaseScale = spriteTransform.localScale; }
        if (groundSnap && snapOnStart) SnapImmediate();
    }
    
    private void Update()
    {
        // Find local player camera if not found yet (for multiplayer)
        if (mainCamera == null)
        {
            if (Nexus.CameraManager.Instance != null)
                mainCamera = Nexus.CameraManager.Instance.MainCamera;
            else if (Camera.main != null)
                mainCamera = Camera.main;
        }
        
        // Only raycast to check focus when relevant keys are pressed
        bool stateKey =
            Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1) ||
            Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2) ||
            Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3) ||
            Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4) ||
            Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5) ||
            Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6);
        bool scaleKey =
            Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.KeypadPlus) ||
            Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus);
        
        if (stateKey || scaleKey)
        {
            CheckCenterScreenOver();
        }
        else
        {
            isCenterScreenOver = false;
        }
        
        // Handle state changes only when center screen is over token
        if (isCenterScreenOver)
        {
            // State changes
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                SetState(1);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                SetState(2);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                SetState(3);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
            {
                SetState(4);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))
            {
                SetState(5);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6))
            {
                SetState(6);
            }
            
            // Scale changes
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.KeypadPlus))
            {
                IncreaseScale();
            }
            else if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                DecreaseScale();
            }
        }
        ResolveUpwardPenetration();
    }
    
    private void LateUpdate()
    {
        // Billboard sprite to face camera
        if (billboardToCamera && spriteTransform != null && mainCamera != null)
        {
            Vector3 directionToCamera = mainCamera.transform.position - spriteTransform.position;
            directionToCamera.y = 0;
            
            if (directionToCamera.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);
                spriteTransform.rotation = targetRotation;
            }
        }
        if (directional4WayBillboard && spriteRenderer != null && mainCamera != null)
        {
            Vector3 toCam = mainCamera.transform.position - transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 0.001f)
            {
                Vector3 dir = toCam.normalized;
                bool front = Vector3.Dot(transform.forward, dir) >= 0f;
                bool right = Vector3.Dot(transform.right, dir) > 0f;
                Sprite target = front
                    ? (directionalFrontSprite != null ? directionalFrontSprite : (defaultSprite != null ? defaultSprite : spriteRenderer.sprite))
                    : (directionalBackSprite != null ? directionalBackSprite : (directionalFrontSprite != null ? directionalFrontSprite : (defaultSprite != null ? defaultSprite : spriteRenderer.sprite)));
                if (spriteRenderer.sprite != target) spriteRenderer.sprite = target;
                spriteRenderer.flipX = !right;
            }
        }
        if (squashNearCamera && spriteTransform != null && mainCamera != null)
        {
            Vector3 camPos = mainCamera.transform.position;
            Vector3 camFwd = mainCamera.transform.forward;
            Vector3 toSprite = spriteTransform.position - camPos;
            float depth = Vector3.Dot(toSprite, camFwd);
            float near = mainCamera.nearClipPlane + squashNearOffset;
            float t = 1f - Mathf.Clamp01((depth - near) / Mathf.Max(0.0001f, squashRange));
            if (t <= 0f)
            {
                Vector3 target = new Vector3(spriteBaseScale.x, spriteBaseScale.y, 1f);
                spriteTransform.localScale = Vector3.Lerp(spriteTransform.localScale, target, Time.deltaTime * squashLerpSpeed);
            }
            else
            {
                Vector3 vp3 = mainCamera.WorldToViewportPoint(spriteTransform.position);
                float ax = Mathf.Abs(vp3.x - 0.5f);
                float ay = Mathf.Abs(vp3.y - 0.5f);
                float sum = Mathf.Max(0.0001f, ax + ay);
                float horizWeight = ax / sum;
                float vertWeight = ay / sum;
                float targetX = Mathf.Lerp(spriteBaseScale.x, spriteBaseScale.x * maxXScaleAtMaxSquash, t * horizWeight);
                float targetY = Mathf.Lerp(spriteBaseScale.y, spriteBaseScale.y * minYScaleAtMaxSquash, t * vertWeight);
                spriteTransform.localScale = Vector3.Lerp(spriteTransform.localScale, new Vector3(targetX, targetY, 1f), Time.deltaTime * squashLerpSpeed);
            }
        }
        // Snap token to ground (only for tokens)
        if (groundSnap && !externalDragActive)
        {
            PerformGroundSnap();
        }
    }
    
    // Usa posição do mouse para detectar foco do token
    private void CheckCenterScreenOver()
    {
        if (mainCamera == null)
            return;
        
        // Raycast a partir da posição do mouse na tela
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f))
        {
            if (hit.collider != null)
            {
                var ts = hit.collider.GetComponentInParent<TokenSetup>();
                isCenterScreenOver = (ts != null && ts.gameObject == gameObject);
            }
            else
            {
                isCenterScreenOver = false;
            }
        }
        else
        {
            isCenterScreenOver = false;
        }
    }
    
    private void SetupSpriteMaterial()
    {
        if (spriteRenderer == null)
            return;
        
        // Try to find lit sprite shader
        Shader litShader = Shader.Find("Custom/SpriteLitBillboard");
        if (litShader != null)
        {
            spriteMaterial = new Material(litShader);
            spriteRenderer.material = spriteMaterial;
            spriteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            spriteRenderer.receiveShadows = true;
            Debug.Log($"Applied Custom/SpriteLitBillboard shader to {gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"Custom/SpriteLitBillboard shader not found! Falling back to default material on {gameObject.name}");
        }
    }
    
    #region State Management
    
    public void SetState(int state)
    {
        currentState = state;
        ApplyState(state);
    }
    
    private void ApplyState(int state)
    {
        if (spriteRenderer == null)
            return;
        
        switch (state)
        {
            case 1: // Default sprite, normal
                spriteRenderer.sprite = defaultSprite;
                spriteRenderer.flipX = false;
                break;
                
            case 2: // Default sprite, flipped
                spriteRenderer.sprite = defaultSprite;
                spriteRenderer.flipX = true;
                break;
                
            case 3: // Alternative sprite 1, normal
                if (alternativeSprite1 != null)
                    spriteRenderer.sprite = alternativeSprite1;
                else
                    Debug.LogWarning($"Alternative Sprite 1 not assigned on {gameObject.name}");
                spriteRenderer.flipX = false;
                break;
                
            case 4: // Alternative sprite 1, flipped
                if (alternativeSprite1 != null)
                    spriteRenderer.sprite = alternativeSprite1;
                else
                    Debug.LogWarning($"Alternative Sprite 1 not assigned on {gameObject.name}");
                spriteRenderer.flipX = true;
                break;
                
            case 5: // Alternative sprite 2, normal
                if (alternativeSprite2 != null)
                    spriteRenderer.sprite = alternativeSprite2;
                else
                    Debug.LogWarning($"Alternative Sprite 2 not assigned on {gameObject.name}");
                spriteRenderer.flipX = false;
                break;
                
            case 6: // Alternative sprite 2, flipped
                if (alternativeSprite2 != null)
                    spriteRenderer.sprite = alternativeSprite2;
                else
                    Debug.LogWarning($"Alternative Sprite 2 not assigned on {gameObject.name}");
                spriteRenderer.flipX = true;
                break;
        }
    }
    
    #endregion
    
    #region Scale Management
    
    public void IncreaseScale()
    {
        float newScale = Mathf.Min(currentScale + scaleStep, maxScale);
        ApplyScale(newScale);
    }
    
    public void DecreaseScale()
    {
        float newScale = Mathf.Max(currentScale - scaleStep, minScale);
        ApplyScale(newScale);
    }
    
    public void SetScale(float scale)
    {
        currentScale = Mathf.Clamp(scale, minScale, maxScale);
        ApplyScale(currentScale);
    }
    
    private void ApplyScale(float scale)
    {
        currentScale = scale;
        transform.localScale = Vector3.one * scale;
    }
    
    public float GetCurrentScale()
    {
        return currentScale;
    }
    
    #endregion
    
    private void SetupComponents()
    {
        // Find sprite renderer
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteTransform = spriteRenderer.transform;
            spriteTransform.localPosition = Vector3.zero;
            spriteTransform.localRotation = Quaternion.identity;
        }
        else
        {
            Debug.LogWarning($"No SpriteRenderer found on {gameObject.name} or its children!");
        }
        
        // Setup Rigidbody (optional)
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.mass = mass;
            rb.drag = drag;
            rb.angularDrag = angularDrag;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            // Freeze X and Z rotation to keep token upright
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            // Tokens rely on snapping, not physics
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        // Setup Box Collider - user configures manually
        boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            // Remove CapsuleCollider if exists
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                DestroyImmediate(capsule);
            }
            
            boxCollider = gameObject.AddComponent<BoxCollider>();
            boxCollider.size = new Vector3(1f, 0.2f, 1f);
            boxCollider.center = new Vector3(0, 0.1f, 0);
            
            Debug.Log($"Created BoxCollider on {gameObject.name}. Adjust size/position manually in Inspector.");
        }
        
        
    }
    
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;
        
        Rigidbody rbTemp = GetComponent<Rigidbody>();
        if (rbTemp != null)
        {
            rbTemp.mass = mass;
            rbTemp.drag = drag;
            rbTemp.angularDrag = angularDrag;
        }
        
        // Clamp scale
        currentScale = Mathf.Clamp(currentScale, minScale, maxScale);

        if (simplePhysicsMode)
        {
            EnforceSimplePhysics();
        }
    }

    private static readonly RaycastHit[] tempRayHits = new RaycastHit[32];
    private static readonly RaycastHit[] tempBoxHits = new RaycastHit[32];

    private void PerformGroundSnap()
    {
        if (!TryComputeTargetBottomY(out float targetBottomY, out Bounds bounds))
            return;

        float currentBottomY = bounds.min.y;
        float delta = targetBottomY - currentBottomY;
        if (Mathf.Abs(delta) > 0.0001f)
        {
            Vector3 targetPos = transform.position + new Vector3(0f, delta, 0f);
            // Drop instantly when stepping down big edges; smooth only when moving up
            if (delta < 0f)
            {
                transform.position = targetPos;
            }
            else
            {
                float k = 1f - Mathf.Exp(-snapLerpSpeed * Time.deltaTime);
                transform.position = Vector3.Lerp(transform.position, targetPos, k);
            }
        }
    }

    private void SnapImmediate()
    {
        if (!TryComputeTargetBottomY(out float targetBottomY, out Bounds bounds))
            return;
        float currentBottomY = bounds.min.y;
        float delta = targetBottomY - currentBottomY;
        if (Mathf.Abs(delta) > 0.0001f)
        {
            Vector3 p = transform.position;
            p.y += delta;
            transform.position = p;
        }
        ResolveUpwardPenetration();
    }

    public void ForceSnapImmediate()
    {
        SnapImmediate();
    }

    private bool TryComputeTargetBottomY(out float resultBottomY, out Bounds bounds)
    {
        if (boxCollider != null)
            bounds = boxCollider.bounds;
        else if (!TryGetCombinedBounds(out bounds))
        {
            resultBottomY = 0f;
            return false;
        }

        Vector3 c = bounds.center;
        float originY = bounds.max.y + snapRayHeight;
        float distance = snapRayHeight + snapMaxDistance + bounds.extents.y;

        // Compute both center and support candidates
        bool centerOK = RaycastGround(new Vector3(c.x, originY, c.z), distance, out RaycastHit centerHit);
        Vector3 halfExtents = new Vector3(
            Mathf.Max(0.001f, bounds.extents.x - snapFootInset),
            0.01f,
            Mathf.Max(0.001f, bounds.extents.z - snapFootInset)
        );
        bool supportOK = BoxcastGround(new Vector3(c.x, originY, c.z), halfExtents, distance, out RaycastHit supportHit);

        if (!centerOK && !supportOK)
        {
            resultBottomY = 0f;
            return false;
        }

        float currentBottomY = bounds.min.y;
        float chosenY;
        if (centerOK)
        {
            float centerY = centerHit.point.y;
            if (centerY < currentBottomY)
            {
                // Stepping down: follow center so we drop off edges immediately
                chosenY = centerY;
            }
            else
            {
                // Moving up or staying level: choose the safer (higher) support to avoid sinking on irregular ground
                float supportY = supportOK ? supportHit.point.y : centerY;
                chosenY = Mathf.Max(centerY, supportY);
            }
        }
        else
        {
            // No center hit: check corners for large step-down
            float px = Mathf.Max(0f, bounds.extents.x - snapFootInset);
            float pz = Mathf.Max(0f, bounds.extents.z - snapFootInset);
            Vector3[] corners = new Vector3[4]
            {
                new Vector3(c.x - px, originY, c.z - pz),
                new Vector3(c.x - px, originY, c.z + pz),
                new Vector3(c.x + px, originY, c.z - pz),
                new Vector3(c.x + px, originY, c.z + pz),
            };
            bool anyCorner = false;
            float minCornerY = float.PositiveInfinity;
            for (int i = 0; i < 4; i++)
            {
                if (RaycastGround(corners[i], distance, out RaycastHit ch))
                {
                    anyCorner = true;
                    if (ch.point.y < minCornerY) minCornerY = ch.point.y;
                }
            }
            if (anyCorner && (currentBottomY - minCornerY) >= stepDownMinDrop)
            {
                // Big edge: drop to the lower surface
                chosenY = minCornerY;
            }
            else
            {
                // Otherwise, stay supported by the highest surface under footprint
                chosenY = supportHit.point.y;
            }
        }

        resultBottomY = chosenY + snapOffsetY;
        return true;
    }

    private bool TryGetCombinedBounds(out Bounds outBounds)
    {
        var cols = GetComponentsInChildren<Collider>();
        if (cols == null || cols.Length == 0)
        {
            outBounds = new Bounds(transform.position, Vector3.zero);
            return false;
        }
        outBounds = cols[0].bounds;
        for (int i = 1; i < cols.Length; i++)
            outBounds.Encapsulate(cols[i].bounds);
        return true;
    }

    private bool RaycastGround(Vector3 origin, float distance, out RaycastHit bestHit)
    {
        bestHit = default;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, tempRayHits, distance, groundMask, QueryTriggerInteraction.Ignore);
        if (count == 0) return false;
        // Fallback to full list if buffer overflow is likely
        RaycastHit[] hits = tempRayHits;
        if (count >= tempRayHits.Length)
        {
            hits = Physics.RaycastAll(origin, Vector3.down, distance, groundMask, QueryTriggerInteraction.Ignore);
            count = hits.Length;
            if (count == 0) return false;
        }
        float best = float.PositiveInfinity;
        Transform self = transform;
        for (int i = 0; i < count; i++)
        {
            var h = hits[i];
            if (h.collider == null) continue;
            Transform ht = h.collider.transform;
            if (ht == null) continue;
            if (ht == self || ht.IsChildOf(self)) continue; // ignore own colliders
            if (h.normal.y < minGroundNormalY) continue; // skip vertical/near-vertical faces
            if (h.distance < best)
            {
                best = h.distance;
                bestHit = h;
            }
        }
        return best < float.PositiveInfinity;
    }

    private void ResolveUpwardPenetration()
    {
        if (boxCollider == null) return;
        // Use collider's true world box for overlap
        Vector3 half = Vector3.Scale(boxCollider.size * 0.5f, transform.lossyScale) * 0.98f;
        Vector3 worldCenter = transform.TransformPoint(boxCollider.center);
        for (int iter = 0; iter < 3; iter++)
        {
            Collider[] overlaps = Physics.OverlapBox(worldCenter, half, transform.rotation, groundMask, QueryTriggerInteraction.Ignore);
            bool pushed = false;
            for (int i = 0; i < overlaps.Length; i++)
            {
                var other = overlaps[i];
                if (other == null) continue;
                if (other.transform == transform || other.transform.IsChildOf(transform)) continue;
                // Compute minimal separation
                if (Physics.ComputePenetration(
                    boxCollider, transform.position, transform.rotation,
                    other, other.transform.position, other.transform.rotation,
                    out Vector3 dir, out float dist))
                {
                    Vector3 up = Vector3.Project(dir, Vector3.up);
                    if (up.y > 0.0001f && dist > 0.0001f)
                    {
                        transform.position += up.normalized * dist;
                        worldCenter = transform.TransformPoint(boxCollider.center);
                        pushed = true;
                    }
                }
            }
            if (!pushed) break;
        }
    }

    private bool BoxcastGround(Vector3 center, Vector3 halfExtents, float distance, out RaycastHit bestHit)
    {
        bestHit = default;
        int count = Physics.BoxCastNonAlloc(center, halfExtents, Vector3.down, tempBoxHits, transform.rotation, distance, groundMask, QueryTriggerInteraction.Ignore);
        if (count == 0) return false;
        // Fallback to full list if buffer overflow is likely
        RaycastHit[] hits = tempBoxHits;
        if (count >= tempBoxHits.Length)
        {
            hits = Physics.BoxCastAll(center, halfExtents, Vector3.down, transform.rotation, distance, groundMask, QueryTriggerInteraction.Ignore);
            count = hits.Length;
            if (count == 0) return false;
        }
        float best = float.PositiveInfinity;
        Transform self = transform;
        for (int i = 0; i < count; i++)
        {
            var h = hits[i];
            if (h.collider == null) continue;
            Transform ht = h.collider.transform;
            if (ht == null) continue;
            if (ht == self || ht.IsChildOf(self)) continue; // ignore own colliders
            if (h.normal.y < minGroundNormalY) continue; // skip vertical/near-vertical faces
            if (h.distance < best)
            {
                best = h.distance;
                bestHit = h;
            }
        }
        return best < float.PositiveInfinity;
    }

    private void EnforceSimplePhysics()
    {
        // Do not remove NetworkedToken here

        // Force cube-like rigidbody
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.drag = 0f;
            rb.angularDrag = 0.05f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.detectCollisions = true;
            rb.WakeUp();
        }

        // Ensure colliders are solid
        var cols = GetComponentsInChildren<Collider>();
        if (cols != null)
        {
            foreach (var c in cols)
            {
                c.isTrigger = false;
            }
        }
    }
    
    public void SetPhysicsMaterial(PhysicMaterial material)
    {
        if (boxCollider != null)
        {
            boxCollider.material = material;
        }
    }
    
    public void SetPhysicsEnabled(bool enabled)
    {
        if (rb != null)
        {
            rb.isKinematic = !enabled;
            rb.useGravity = enabled;
        }
    }
    
    public int GetCurrentState()
    {
        return currentState;
    }
    
    // Debug visualization
    private void OnDrawGizmosSelected()
    {
        // Draw collider bounds
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null)
        {
            Gizmos.color = isCenterScreenOver ? Color.yellow : Color.green;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(col.center, col.size);
        }
        
        // Draw sprite bounds
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(sr.bounds.center, sr.bounds.size);
        }
    }
    
    /// <summary>
    /// Finds the local player's camera for multiplayer support
    /// Each player will see billboards oriented to their own camera
    /// </summary>
    private void FindLocalPlayerCamera()
    {
        Camera[] cameras = Object.FindObjectsOfType<Camera>();
        
        // First pass: strictly prefer the local player's camera
        foreach (Camera cam in cameras)
        {
            if (!cam.enabled || !cam.gameObject.activeInHierarchy) continue;
            var networkPlayer = cam.GetComponentInParent<Nexus.Networking.NetworkPlayer>();
            if (networkPlayer != null && networkPlayer.isLocalPlayer)
            {
                mainCamera = cam;
                return;
            }
        }
        
        // Second pass: fallback to any enabled MainCamera
        foreach (Camera cam in cameras)
        {
            if (!cam.enabled || !cam.gameObject.activeInHierarchy) continue;
            if (cam.CompareTag("MainCamera"))
            {
                mainCamera = cam;
                return;
            }
        }
        
        // Final fallback: Camera.main
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }
}