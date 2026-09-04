using System.Collections.Generic;
using UnityEngine;

public sealed class HelloAndroid : MonoBehaviour
{
    private const int WaterWidth = 112;
    private const int WaterHeight = 192;
    private const int BubbleCount = 26;
    private const int RainDropCount = 7;
    private const int MoteCount = 26;
    private const int RayCount = 4;
    private const int MaxPellets = 8;
    private const float PelletLifetime = 9f;
    private const float PelletSinkSpeed = 0.075f;
    private const float PelletRadiusWorld = 0.045f;
    private const float PelletEatRadius = 0.055f;
    private const float PelletChaseRadius = 0.40f;
    private const float FleeRadius = 0.18f;
    private const float FleeDuration = 0.9f;
    private const float EmperorFullCooldown = 7f;
    private const float SimpleFishFullCooldown = 5f;
    private const float PauseButtonSize = 52f;
    private const float SimulationSpeed = 42f;
    private const int FishLayer = 8;
    private const float FishCameraSize = 1.55f;
    private const float FishBaseSizeRatio = 0.22f;
    private const string EmperorPrefabResource = "EmperorAngelfish_swim1";
    private const float FishCruiseSpeed = 0.07f;
    private const float EmperorLengthFraction = 0.44f;
    private const float EmperorHeightFraction = 0.40f;
    private const float FishCameraFov = 17.6f;
    private const float FishCameraDistance = 10f; // camera sits at z = -FishCameraDistance, looking toward +z
    private const float FishTurnYawSpeed = 165f;
    private const float FishBankStrength = 0.32f;
    private const float FishBankMax = 30f;
    private const float EmperorDepthFar = -3.2f;
    private const float EmperorDepthNear = 4.2f;
    private const float SimpleFishTurnMargin = 0.12f;
    private const int FishSeparationIterations = 8;
    private const float FishSeparationPadding = 0.012f;
    private static readonly string[] SimpleFishResources = { "fish01", "fish02", "fish03" };
    private static readonly Quaternion[] SimpleFishBaseRotations =
    {
        Quaternion.Euler(0f, -90f, 0f),
        Quaternion.Euler(0f, -90f, 0f),
        Quaternion.Euler(0f, -90f, 0f)
    };
    private static readonly float[] SimpleFishLengthFractions = { 0.30f, 0.34f, 0.26f };
    private static readonly float[] SimpleFishBaseYs = { 0.22f, 0.50f, 0.78f };
    private static readonly float[] SimpleFishSpeedFactors = { 1.15f, 0.85f, 1.3f };
    private static readonly string[] HeadMarkerTokens = { "Head", "Eye" };
    private static readonly string[] TailMarkerTokens = { "TailFin" };
    private static readonly string[] DorsalMarkerTokens = { "FinDorsal" };
    private static readonly string[] VentralMarkerTokens = { "FinPelvic", "FinAnal" };
    private const float SimulationDamping = 0.985f;
    private const float RainSurface = 0.78f;
    private const float WaterTextureScale = 1.0f;

    private struct Bubble
    {
        public Vector2 position;
        public float size;
        public float phase;
        public float speed;
    }

    private struct RainDrop
    {
        public Vector2 position;
        public float speed;
        public float phase;
    }

    private struct Mote
    {
        public Vector2 position;
        public float phase;
        public float size;
        public float drift;
    }

    private struct Fish
    {
        public Vector2 position;
        public float baseY;
        public float speed;
        public float size;
        public float phase;
        public float direction;
        public float targetDirection;
        public float depth;
        public float targetDepth;
        public float nextDepthTime;
        public float nextTurnTime;
        public float glideTime;
        public float nextGlideTime;
        public float yaw;
    }

    private sealed class SimpleFish
    {
        public GameObject root;
        public GameObject pivot;
        public GameObject model;
        public MeshRenderer meshRenderer;
        public Vector3 localForward;
        public Vector3 localUp;
        public Vector2 position;
        public float baseY;
        public float speedFactor;
        public float phase;
        public float direction;
        public float targetDirection;
        public float depth;
        public float targetDepth;
        public float nextDepthTime;
        public float nextTurnTime;
        public float glideTime;
        public float nextGlideTime;
        public float yaw;
        public float fitScale;
        public float worldHalfLength;
        public float worldHalfHeight;
        public FoodPellet targetPellet;
        public float fedCooldown;
        public float fleeTime;
        public Vector2 fleeFrom;
        public Vector2 schoolOffset;
        public float depthOffset;
    }

    private sealed class FoodPellet
    {
        public GameObject body;
        public Vector2 position;
        public float age;
        public bool alive;
    }

    private Texture2D waterTexture;
    private Texture2D glowTexture;
    private RenderTexture fishRenderTexture;
    private RenderTexture fishRenderTextureBack;
    private Camera fishCamera;
    private GameObject fishRoot;
    private GameObject fishPivot;
    private GameObject fishModel;
    private Animator fishAnimator;
    private SkinnedMeshRenderer fishRenderer;
    private Vector3 fishLocalForward;
    private Vector3 fishLocalUp;
    private float fishLocalLength;
    private float fishLocalHeight;
    private float fishFitScale = 1f;
    private float fishCalibrateTimer;
    private bool fishCalibrated;
    private Light fishLight;
    private readonly List<SimpleFish> simpleFish = new List<SimpleFish>();
    private GameObject simpleFishParent;
    private Color[] waterPixels;
    private float[] heights;
    private float[] velocities;
    private Bubble[] bubbles;
    private RainDrop[] rainDrops;
    private Mote[] motes;
    private readonly List<FoodPellet> foodPellets = new List<FoodPellet>();
    private GameObject pelletsParent;
    private FoodPellet emperorTarget;
    private float emperorFedCooldown;
    private float emperorFleeTime;
    private Vector2 emperorFleeFrom;
    private bool appPaused;
    private float eatFlashTime;
    private Vector2 eatFlashPosition;
    private Fish fish;
    private Vector2Int fishRenderSize;
    private AudioSource audioSource;
    private AudioClip rippleClip;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle footerStyle;
    private Vector2 lastTouchPosition = new Vector2(-1f, -1f);
    private Vector2 smoothedTilt;
    private float elapsed;
    private float nextRainTime;
    private float rippleFlash;
    private bool initialized;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        Screen.orientation = ScreenOrientation.Portrait;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = false;
        Screen.autorotateToLandscapeRight = false;
        Input.compensateSensors = true;
    }

    private void Start()
    {
        InitializeSimulation();
    }

    private void Update()
    {
        EnsurePortraitScreen();
        if (!initialized)
        {
            InitializeSimulation();
        }

        HandlePauseInput();

        if (appPaused)
        {
            return;
        }

        var deltaTime = Mathf.Min(Time.deltaTime, 0.033f);
        elapsed += deltaTime;
        rippleFlash = Mathf.MoveTowards(rippleFlash, 0f, deltaTime * 2.4f);
        eatFlashTime = Mathf.MoveTowards(eatFlashTime, 0f, deltaTime * 1.6f);

        var tilt = new Vector2(Input.acceleration.x, -Input.acceleration.y);
        smoothedTilt = Vector2.Lerp(smoothedTilt, tilt, 1f - Mathf.Exp(-4.5f * deltaTime));
        SimulateWater(deltaTime);
        UpdateRain(deltaTime);
        UpdateBubbles(deltaTime);
        UpdateMotes(deltaTime);
        UpdateFoodPellets(deltaTime);
        UpdateFish(deltaTime);
        EnsureFishScene();
        UpdateFishModel();
        UpdateSimpleFishMotion(deltaTime);
        ApplyEmperorSeparation();
        UpdateSimpleFishModel();
        HandleTouchRipples();
        RenderWaterTexture();
    }

    private void LateUpdate()
    {
        if (!initialized || appPaused)
        {
            return;
        }

        // Render after Animator and all fish transforms have been updated. The camera
        // stays disabled so it cannot write the shared texture while OnGUI samples it.
        RenderFish();
    }

    private void InitializeSimulation()
    {
        if (initialized)
        {
            return;
        }

        waterTexture = new Texture2D(WaterWidth, WaterHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        glowTexture = CreateGlowTexture(96);
        waterPixels = new Color[WaterWidth * WaterHeight];
        heights = new float[WaterWidth * WaterHeight];
        velocities = new float[WaterWidth * WaterHeight];
        bubbles = new Bubble[BubbleCount];
        rainDrops = new RainDrop[RainDropCount];

        for (var i = 0; i < BubbleCount; i++)
        {
            bubbles[i] = new Bubble
            {
                position = new Vector2(UnityEngine.Random.Range(0.05f, 0.95f),
                    UnityEngine.Random.Range(0.04f, 0.72f)),
                size = UnityEngine.Random.Range(0.004f, 0.014f),
                phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
                speed = UnityEngine.Random.Range(0.025f, 0.075f)
            };
        }

        for (var i = 0; i < RainDropCount; i++)
        {
            rainDrops[i] = CreateRainDrop(i, true);
        }

        motes = new Mote[MoteCount];
        for (var i = 0; i < MoteCount; i++)
        {
            motes[i] = new Mote
            {
                position = new Vector2(UnityEngine.Random.Range(0.03f, 0.97f),
                    UnityEngine.Random.Range(0f, 1f)),
                phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
                size = UnityEngine.Random.Range(5f, 16f),
                drift = UnityEngine.Random.Range(0.003f, 0.013f)
            };
        }

        fish = new Fish
        {
            position = new Vector2(0.18f, 0.43f),
            baseY = 0.43f,
            speed = 0.075f,
            size = 0.22f,
            phase = 0.8f,
            direction = 1f,
            targetDirection = 1f,
            depth = 0.55f,
            targetDepth = UnityEngine.Random.Range(0.3f, 0.85f),
            nextDepthTime = UnityEngine.Random.Range(4f, 8f),
            nextTurnTime = UnityEngine.Random.Range(3f, 7f),
            nextGlideTime = UnityEngine.Random.Range(5f, 9f),
            yaw = 0f
        };

        // A gentle opening ripple makes the simulation visibly alive immediately.
        AddRipple(0.50f, 0.48f, 0.42f);
        AddRipple(0.27f, 0.34f, 0.24f);
        AddRipple(0.76f, 0.62f, 0.20f);
        EnsureAudio();
        EnsureFishScene();
        RenderWaterTexture();
        UpdateFishModel();
        RenderFish();
        initialized = true;
    }

    private void SimulateWater(float deltaTime)
    {
        var tiltForceX = smoothedTilt.x * 0.012f;
        var tiltForceY = smoothedTilt.y * 0.012f;
        var step = deltaTime * SimulationSpeed;

        for (var y = 1; y < WaterHeight - 1; y++)
        {
            for (var x = 1; x < WaterWidth - 1; x++)
            {
                var index = y * WaterWidth + x;
                var average = (heights[index - 1] + heights[index + 1] +
                               heights[index - WaterWidth] + heights[index + WaterWidth]) * 0.25f;
                var restoringForce = average - heights[index];
                var flowForce = tiltForceX * ((float)x / WaterWidth - 0.5f) +
                                tiltForceY * ((float)y / WaterHeight - 0.5f);
                velocities[index] += (restoringForce * step + flowForce * step) * 0.55f;
                velocities[index] *= Mathf.Pow(SimulationDamping, deltaTime * 60f);
            }
        }

        for (var y = 1; y < WaterHeight - 1; y++)
        {
            for (var x = 1; x < WaterWidth - 1; x++)
            {
                var index = y * WaterWidth + x;
                heights[index] += velocities[index] * deltaTime;
                heights[index] = Mathf.Clamp(heights[index], -1.2f, 1.2f);
            }
        }

        // Absorb energy at the rim so waves do not reflect forever from the texture border.
        for (var x = 0; x < WaterWidth; x++)
        {
            DampenCell(x, 0);
            DampenCell(x, WaterHeight - 1);
        }

        for (var y = 0; y < WaterHeight; y++)
        {
            DampenCell(0, y);
            DampenCell(WaterWidth - 1, y);
        }
    }

    private void DampenCell(int x, int y)
    {
        var index = y * WaterWidth + x;
        heights[index] *= 0.84f;
        velocities[index] *= 0.72f;
    }

    private void UpdateRain(float deltaTime)
    {
        nextRainTime -= deltaTime;
        if (nextRainTime <= 0f)
        {
            nextRainTime = UnityEngine.Random.Range(0.55f, 1.25f);
            var index = UnityEngine.Random.Range(0, RainDropCount);
            rainDrops[index] = CreateRainDrop(index, false);
        }

        for (var i = 0; i < rainDrops.Length; i++)
        {
            var drop = rainDrops[i];
            drop.position.y -= drop.speed * deltaTime;
            drop.phase += deltaTime * 8f;

            if (drop.position.y <= RainSurface)
            {
                AddRipple(drop.position.x, RainSurface, 0.17f);
                PlayRippleSound();
                drop = CreateRainDrop(i, false);
            }

            rainDrops[i] = drop;
        }
    }

    private RainDrop CreateRainDrop(int index, bool stagger)
    {
        return new RainDrop
        {
            position = new Vector2(UnityEngine.Random.Range(0.08f, 0.92f),
                stagger ? UnityEngine.Random.Range(RainSurface, 1.12f) : 1.08f + index * 0.025f),
            speed = UnityEngine.Random.Range(0.24f, 0.42f),
            phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f)
        };
    }

    private void UpdateMotes(float deltaTime)
    {
        if (motes == null)
        {
            return;
        }

        for (var i = 0; i < motes.Length; i++)
        {
            var mote = motes[i];
            mote.position.y -= mote.drift * deltaTime;
            mote.position.x += Mathf.Sin(elapsed * 0.3f + mote.phase) * deltaTime * 0.0022f;

            if (mote.position.y < -0.04f)
            {
                mote.position = new Vector2(UnityEngine.Random.Range(0.03f, 0.97f), 1.04f);
                mote.size = UnityEngine.Random.Range(5f, 16f);
            }

            mote.position.x = Mathf.Clamp(mote.position.x, 0.01f, 0.99f);
            motes[i] = mote;
        }
    }

    private void UpdateBubbles(float deltaTime)
    {
        for (var i = 0; i < bubbles.Length; i++)
        {
            var bubble = bubbles[i];
            bubble.position.y += bubble.speed * deltaTime;
            bubble.position.x += Mathf.Sin(elapsed * 1.6f + bubble.phase) * deltaTime * 0.006f;
            bubble.phase += deltaTime * 0.35f;

            if (bubble.position.y > 0.76f)
            {
                bubble.position = new Vector2(UnityEngine.Random.Range(0.05f, 0.95f),
                    UnityEngine.Random.Range(0.02f, 0.16f));
                bubble.size = UnityEngine.Random.Range(0.004f, 0.014f);
            }

            bubble.position.x = Mathf.Clamp01(bubble.position.x);
            bubbles[i] = bubble;
        }
    }

    private void HandleTapAction(Vector2 normalized)
    {
        // One tap both drops a food pellet and startles nearby fish.
        SpawnFoodPellet(normalized);
        TriggerTapReaction(normalized);
    }

    private void EnsurePelletsParent()
    {
        if (pelletsParent != null)
        {
            return;
        }

        pelletsParent = new GameObject("AquaFlow_FoodPellets");
        pelletsParent.transform.position = Vector3.zero;
        pelletsParent.transform.localScale = Vector3.one;
    }

    private static Mesh pelletMesh;

    private void SpawnFoodPellet(Vector2 position)
    {
        if (foodPellets.Count >= MaxPellets)
        {
            return;
        }

        EnsurePelletsParent();
        var pellet = new FoodPellet
        {
            position = position,
            alive = true
        };

        // Built-in primitives need the SphereCollider class, which IL2CPP strips on
        // Android, so the pellet sphere is built by hand instead.
        if (pelletMesh == null)
        {
            pelletMesh = BuildSphereMesh(PelletRadiusWorld, 10, 12);
        }

        var body = new GameObject("AquaFlow_Food");
        body.layer = FishLayer;
        body.transform.SetParent(pelletsParent.transform, false);
        body.transform.localScale = Vector3.one;
        body.AddComponent<MeshFilter>().sharedMesh = pelletMesh;

        var renderer = body.AddComponent<MeshRenderer>();
        var material = new Material(Shader.Find("Standard"));
        material.color = new Color(1f, 0.62f, 0.28f);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", new Color(1f, 0.55f, 0.2f));
        renderer.sharedMaterial = material;
        pellet.body = body;

        PositionPelletBody(pellet);
        foodPellets.Add(pellet);
    }

    private static Mesh BuildSphereMesh(float radius, int rings, int segments)
    {
        var mesh = new Mesh();
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();

        for (var lat = 0; lat <= rings; lat++)
        {
            var theta = Mathf.PI * lat / rings;
            var sinTheta = Mathf.Sin(theta);
            var cosTheta = Mathf.Cos(theta);
            for (var lon = 0; lon <= segments; lon++)
            {
                var phi = 2f * Mathf.PI * lon / segments;
                var vertex = new Vector3(sinTheta * Mathf.Cos(phi), cosTheta, sinTheta * Mathf.Sin(phi)) * radius;
                vertices.Add(vertex);
                normals.Add(vertex.normalized);
            }
        }

        for (var lat = 0; lat < rings; lat++)
        {
            for (var lon = 0; lon < segments; lon++)
            {
                var a = lat * (segments + 1) + lon;
                var b = a + segments + 1;
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(a + 1);
                triangles.Add(a + 1);
                triangles.Add(b);
                triangles.Add(b + 1);
            }
        }

        mesh.vertices = vertices.ToArray();
        mesh.normals = normals.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void PositionPelletBody(FoodPellet pellet)
    {
        if (pellet.body == null)
        {
            return;
        }

        pellet.body.transform.position = FishSpaceToWorld(pellet.position, 0.32f);
    }

    private float FishViewScale(float depth)
    {
        // The fish camera is perspective, so the visible area grows with distance
        // from the camera (reference plane z = 0 sits exactly FishCameraDistance away).
        var z = Mathf.Lerp(EmperorDepthFar, EmperorDepthNear, depth);
        return (z + FishCameraDistance) / FishCameraDistance;
    }

    private Vector3 FishSpaceToWorld(Vector2 fishPosition, float depth)
    {
        if (fishRenderSize.x <= 0)
        {
            return Vector3.zero;
        }

        var scale = FishViewScale(depth);
        var aspect = (float)fishRenderSize.x / Mathf.Max(1, fishRenderSize.y);
        var worldWidth = FishCameraSize * 2f * aspect * scale;
        var worldHeight = FishCameraSize * 2f * scale;
        var z = Mathf.Lerp(EmperorDepthFar, EmperorDepthNear, depth);
        return new Vector3(
            (fishPosition.x - 0.5f) * worldWidth,
            (0.5f - fishPosition.y) * worldHeight,
            z);
    }

    // Places a fish so that its body always stays inside the perspective frustum at
    // its current depth. Because the visible area shrinks when the fish swims far
    // away, the normalized position alone is not enough: without this inset clamp a
    // far fish near the left/right edge would be sliced by the frustum border.
    private void PlaceFishAt(Transform target, Vector2 fishPosition, float depth,
        float halfLengthWorld, ref float direction, ref float targetDirection, bool bounceAtWall)
    {
        if (fishRenderSize.x <= 0 || target == null)
        {
            return;
        }

        var scale = FishViewScale(depth);
        var aspect = (float)fishRenderSize.x / Mathf.Max(1, fishRenderSize.y);
        var worldWidth = FishCameraSize * 2f * aspect * scale;
        var worldHeight = FishCameraSize * 2f * scale;
        var rawX = (fishPosition.x - 0.5f) * worldWidth;
        var rawY = (0.5f - fishPosition.y) * worldHeight;

        var halfHeightWorld = halfLengthWorld * 0.42f;
        // Small hairline pad beyond the body half-extent so fish never even touch the border.
        var maxX = Mathf.Max(0f, worldWidth * 0.5f - halfLengthWorld - worldWidth * 0.02f);
        var maxY = Mathf.Max(0f, worldHeight * 0.5f - halfHeightWorld - worldHeight * 0.02f);
        var x = Mathf.Clamp(rawX, -maxX, maxX);
        var y = Mathf.Clamp(rawY, -maxY, maxY);

        // Soft wall: if the body pressed the edge while free-swimming, turn around
        // instead of hovering. While chasing food or fleeing a tap the fish may keep
        // pressing the wall, so it must not bounce back and forth every frame.
        if (bounceAtWall && Mathf.Abs(rawX - x) > 0.0001f &&
            ((direction > 0f && rawX > 0f) || (direction < 0f && rawX < 0f)))
        {
            targetDirection = -direction;
            direction = targetDirection;
        }

        var z = Mathf.Lerp(EmperorDepthFar, EmperorDepthNear, depth);
        target.position = new Vector3(x, y, z);
    }

    private void UpdateFoodPellets(float deltaTime)
    {
        for (var i = foodPellets.Count - 1; i >= 0; i--)
        {
            var pellet = foodPellets[i];
            if (!pellet.alive)
            {
                if (pellet.body != null)
                {
                    Destroy(pellet.body);
                }
                foodPellets.RemoveAt(i);
                continue;
            }

            pellet.age += deltaTime;
            // Gentle sway while it sinks slowly toward the floor.
            pellet.position.x = Mathf.Clamp(
                pellet.position.x + Mathf.Sin(elapsed * 1.9f + pellet.age * 3f) * deltaTime * 0.004f,
                0.02f, 0.98f);
            if (pellet.position.y < 0.94f)
            {
                pellet.position.y = Mathf.Min(pellet.position.y + PelletSinkSpeed * deltaTime, 0.94f);
            }

            if (pellet.age > PelletLifetime)
            {
                pellet.alive = false;
            }
            else
            {
                PositionPelletBody(pellet);
            }
        }
    }

    private FoodPellet FindNearestPellet(Vector2 from, float radius)
    {
        FoodPellet best = null;
        var bestDistance = radius;
        for (var i = 0; i < foodPellets.Count; i++)
        {
            var pellet = foodPellets[i];
            if (!pellet.alive)
            {
                continue;
            }

            var distance = Vector2.Distance(from, pellet.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = pellet;
            }
        }

        return best;
    }

    private void EatPellet(FoodPellet pellet)
    {
        pellet.alive = false;
        if (pellet.body != null)
        {
            Destroy(pellet.body);
        }
        eatFlashTime = 0.45f;
        eatFlashPosition = pellet.position;
    }

    private void TriggerTapReaction(Vector2 tap)
    {
        if (Vector2.Distance(fish.position, tap) < FleeRadius && emperorFleeTime <= 0f)
        {
            emperorFleeTime = FleeDuration;
            emperorFleeFrom = tap;
            emperorTarget = null;
        }

        for (var i = 0; i < simpleFish.Count; i++)
        {
            var fish = simpleFish[i];
            if (fish.fleeTime <= 0f && Vector2.Distance(fish.position, tap) < FleeRadius)
            {
                fish.fleeTime = FleeDuration;
                fish.fleeFrom = tap;
                fish.targetPellet = null;
            }
        }
    }

    private static Vector2 ClampFishPosition(Vector2 position)
    {
        position.x = Mathf.Clamp(position.x, 0.04f, 0.96f);
        position.y = Mathf.Clamp(position.y, 0.04f, 0.96f);
        return position;
    }

    private void UpdateFish(float deltaTime)
    {
        var currentFlow = smoothedTilt.x * 0.018f;
        var cruise = FishCruiseSpeed * (0.72f + 0.4f * (0.5f + 0.5f * Mathf.Sin(elapsed * 0.62f + fish.phase * 1.7f)));

        // Occasional mid-water heading change and glides, like a fish cruising.
        fish.nextTurnTime -= deltaTime;
        if (fish.nextTurnTime <= 0f)
        {
            fish.nextTurnTime = UnityEngine.Random.Range(3.4f, 7.6f);
            if (UnityEngine.Random.value < 0.5f)
            {
                fish.targetDirection = -fish.targetDirection;
            }
        }

        fish.nextGlideTime -= deltaTime;
        if (fish.nextGlideTime <= 0f)
        {
            fish.nextGlideTime = UnityEngine.Random.Range(6f, 12f);
            fish.glideTime = UnityEngine.Random.Range(0.8f, 1.6f);
        }

        if (fish.glideTime > 0f)
        {
            fish.glideTime -= deltaTime;
            cruise *= 0.28f;
        }

        // Always turn around before leaving the visible water.
        const float turnMargin = 0.10f;
        if (fish.targetDirection > 0f && fish.position.x >= 1f - turnMargin)
        {
            fish.targetDirection = -1f;
        }
        else if (fish.targetDirection < 0f && fish.position.x <= turnMargin)
        {
            fish.targetDirection = 1f;
        }

        fish.direction = fish.targetDirection;

        if (emperorFedCooldown > 0f)
        {
            emperorFedCooldown -= deltaTime;
        }

        // Flee: dart away from a recent nearby tap.
        if (emperorFleeTime > 0f)
        {
            emperorFleeTime -= deltaTime;
            emperorTarget = null;
            var away = fish.position - emperorFleeFrom;
            if (away.sqrMagnitude < 0.0004f)
            {
                away = new Vector2(fish.direction > 0f ? 1f : -1f, 0f);
            }
            away.Normalize();
            var fleeSpeed = cruise * 3.4f;
            fish.position += away * fleeSpeed * deltaTime;
            fish.position.x = Mathf.Clamp(fish.position.x, 0.05f, 0.95f);
            fish.position.y = Mathf.Clamp(fish.position.y, 0.05f, 0.95f);
            fish.direction = away.x >= 0f ? 1f : -1f;
            return;
        }

        // Feed: chase the nearest pellet when hungry.
        if (emperorFedCooldown <= 0f && (emperorTarget == null || !emperorTarget.alive))
        {
            emperorTarget = FindNearestPellet(fish.position, PelletChaseRadius);
        }

        if (emperorTarget != null)
        {
            var pelletPosition = emperorTarget.position;
            var deltaX = pelletPosition.x - fish.position.x;
            var deltaY = pelletPosition.y - fish.position.y;
            fish.direction = deltaX >= 0f ? 1f : -1f;
            var chaseSpeed = Mathf.Max(cruise * 2.5f, 0.085f);
            fish.position.x = Mathf.MoveTowards(fish.position.x, pelletPosition.x, chaseSpeed * deltaTime);
            fish.position.y = Mathf.MoveTowards(fish.position.y, pelletPosition.y, chaseSpeed * 0.92f * deltaTime);
            fish.position.x = Mathf.Clamp(fish.position.x, 0.05f, 0.95f);
            fish.position.y = Mathf.Clamp(fish.position.y, 0.05f, 0.95f);

            if (Mathf.Abs(deltaX) < PelletEatRadius && Mathf.Abs(deltaY) < PelletEatRadius)
            {
                emperorFedCooldown = EmperorFullCooldown;
                EatPellet(emperorTarget);
                emperorTarget = null;
            }
            return;
        }

        // Depth excursions: swim farther from / closer to the camera.
        fish.nextDepthTime -= deltaTime;
        if (fish.nextDepthTime <= 0f)
        {
            fish.nextDepthTime = UnityEngine.Random.Range(5f, 10f);
            fish.targetDepth = UnityEngine.Random.Range(0.12f, 0.95f);
        }

        fish.depth = Mathf.Lerp(fish.depth, fish.targetDepth, 1f - Mathf.Exp(-0.45f * deltaTime));

        // Vertical: gentle bob around a slowly drifting band.
        var targetY = fish.baseY + Mathf.Sin(elapsed * 0.9f + fish.phase) * 0.035f;
        fish.position.y = Mathf.Lerp(fish.position.y, targetY, 1f - Mathf.Exp(-2.4f * deltaTime));

        // Horizontal speed follows the visual heading, so the fish slows while turning.
        var yawRadians = fish.yaw * Mathf.Deg2Rad;
        var headingX = Mathf.Cos(yawRadians);
        var turnSlow = 0.35f + 0.65f * Mathf.Abs(headingX);
        fish.position.x += (cruise + currentFlow) * turnSlow * Mathf.Sign(headingX) * deltaTime;
        fish.position.x = Mathf.Clamp(fish.position.x, turnMargin, 1f - turnMargin);
    }

    private void HandleTouchRipples()
    {
        for (var i = 0; i < Input.touchCount; i++)
        {
            var touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Began && touch.phase != TouchPhase.Moved)
            {
                continue;
            }

            // A tap on the pause button only toggles pause - no ripple or food.
            if (touch.phase == TouchPhase.Began &&
                PauseButtonRect().Contains(new Vector2(touch.position.x, Screen.height - touch.position.y)))
            {
                continue;
            }

            var normalized = ScreenToNormalized(touch.position);
            AddRipple(normalized.x, normalized.y, touch.phase == TouchPhase.Began ? 0.58f : 0.20f);
            lastTouchPosition = touch.position;
            rippleFlash = 1f;

            if (touch.phase == TouchPhase.Began)
            {
                HandleTapAction(normalized);
            }
        }

#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0))
        {
            HandleTapAction(ScreenToNormalized(Input.mousePosition));
        }

        if (Input.GetMouseButton(0))
        {
            if (Vector2.Distance(lastTouchPosition, Input.mousePosition) > 18f)
            {
                var normalized = ScreenToNormalized(Input.mousePosition);
                AddRipple(normalized.x, normalized.y, 0.28f);
                lastTouchPosition = Input.mousePosition;
                rippleFlash = 0.8f;
            }
        }
        else
        {
            lastTouchPosition = new Vector2(-1f, -1f);
        }
#endif
    }

    private Vector2 ScreenToNormalized(Vector2 screenPosition)
    {
        return new Vector2(
            Mathf.Clamp01(screenPosition.x / Mathf.Max(1f, Screen.width)),
            Mathf.Clamp01(1f - screenPosition.y / Mathf.Max(1f, Screen.height)));
    }

    private void AddRipple(float normalizedX, float normalizedY, float strength)
    {
        var centerX = Mathf.Clamp(Mathf.RoundToInt(normalizedX * (WaterWidth - 1)), 1, WaterWidth - 2);
        var centerY = Mathf.Clamp(Mathf.RoundToInt(normalizedY * (WaterHeight - 1)), 1, WaterHeight - 2);
        var radius = 5;

        for (var y = -radius; y <= radius; y++)
        {
            for (var x = -radius; x <= radius; x++)
            {
                var distance = Mathf.Sqrt(x * x + y * y);
                if (distance > radius)
                {
                    continue;
                }

                var cellX = Mathf.Clamp(centerX + x, 0, WaterWidth - 1);
                var cellY = Mathf.Clamp(centerY + y, 0, WaterHeight - 1);
                var index = cellY * WaterWidth + cellX;
                var falloff = 1f - distance / radius;
                heights[index] += strength * falloff * falloff;
                velocities[index] += strength * falloff * 0.45f;
            }
        }
    }

    private void RenderWaterTexture()
    {
        if (waterTexture == null)
        {
            return;
        }

        var lightX = 0.5f + Mathf.Sin(elapsed * 0.42f) * 0.26f;
        var lightY = 0.62f + Mathf.Cos(elapsed * 0.35f) * 0.12f;

        for (var y = 0; y < WaterHeight; y++)
        {
            for (var x = 0; x < WaterWidth; x++)
            {
                var index = y * WaterWidth + x;
                var height = heights[index];
                var left = heights[y * WaterWidth + Mathf.Max(0, x - 1)];
                var right = heights[y * WaterWidth + Mathf.Min(WaterWidth - 1, x + 1)];
                var down = heights[Mathf.Max(0, y - 1) * WaterWidth + x];
                var up = heights[Mathf.Min(WaterHeight - 1, y + 1) * WaterWidth + x];
                var slopeX = (right - left) * 0.75f;
                var slopeY = (up - down) * 0.75f;
                var normalizedY = (float)y / (WaterHeight - 1);
                var depth = Mathf.Clamp01(0.35f + normalizedY * 0.65f);
                var waveLight = Mathf.Clamp01(0.5f + height * 0.55f - slopeY * 0.22f);
                var caustic = Mathf.Sin(x * 0.21f + y * 0.075f + elapsed * 1.35f + height * 2.5f) *
                              Mathf.Sin(x * 0.09f - y * 0.16f - elapsed * 0.85f);
                var lightDistance = Vector2.Distance(new Vector2((float)x / WaterWidth, normalizedY),
                    new Vector2(lightX, lightY));
                var movingHighlight = Mathf.Pow(Mathf.Clamp01(1f - lightDistance * 2.8f), 3f);

                var deep = new Color(0.008f, 0.045f, 0.16f);
                var blue = new Color(0.015f, 0.22f, 0.48f);
                var turquoise = new Color(0.02f, 0.63f, 0.72f);
                var color = Color.Lerp(deep, blue, depth * 0.92f);
                color = Color.Lerp(color, turquoise, waveLight * 0.38f + Mathf.Max(0f, caustic) * 0.12f);
                color += new Color(0.02f, 0.42f, 0.48f) * Mathf.Max(0f, caustic) * 0.18f;
                color += Color.white * (movingHighlight * 0.18f + Mathf.Max(0f, -slopeX - slopeY) * 0.07f);

                // Darker toward the floor with a faint caustic shimmer near the bottom.
                var floorShade = 1f - Mathf.Clamp01((normalizedY - 0.68f) / 0.32f) * 0.38f;
                color *= floorShade;
                var floorCaustic = normalizedY > 0.72f
                    ? (0.5f + 0.5f * Mathf.Sin(x * 0.35f + elapsed * 0.9f)) *
                      (0.5f + 0.5f * Mathf.Sin(y * 0.11f - elapsed * 0.55f)) * 0.06f
                    : 0f;
                color += new Color(0.01f, 0.14f, 0.13f) * floorCaustic;

                var edge = Mathf.Min(Mathf.Min(x, WaterWidth - 1 - x), Mathf.Min(y, WaterHeight - 1 - y));
                var edgeShade = Mathf.Clamp01(edge / 7f);
                color *= 0.72f + edgeShade * 0.28f;
                waterPixels[index] = new Color(color.r, color.g, color.b, 1f);
            }
        }

        waterTexture.SetPixels(waterPixels);
        waterTexture.Apply(false, false);
    }

    private void OnGUI()
    {
        EnsureStyles();
        if (waterTexture == null)
        {
            return;
        }

        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), waterTexture,
            ScaleMode.StretchToFill, false);
        DrawFishShadows();
        DrawFishRenderTexture();
        DrawPellets();
        DrawEatFlash();
        DrawRain();
        DrawBubbles();
        DrawMotes();
        DrawRays();
        if (appPaused)
        {
            DrawPausedOverlay();
        }
        DrawHeader();
        DrawFooter();
        DrawPauseButton();
    }

    private void DrawFishRenderTexture()
    {
        if (fishRenderTexture == null)
        {
            return;
        }

        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), fishRenderTexture,
            ScaleMode.StretchToFill, true);
    }

    private void DrawGlow(Vector2 position, float size, float alpha)
    {
        GUI.color = new Color(0.18f, 0.82f, 1f, alpha);
        GUI.DrawTexture(new Rect(position.x - size * 0.5f, position.y - size * 0.5f,
            size, size), glowTexture, ScaleMode.StretchToFill, true);
        GUI.color = Color.white;
    }

    private void DrawRain()
    {
        for (var i = 0; i < rainDrops.Length; i++)
        {
            var drop = rainDrops[i];
            var x = drop.position.x * Screen.width;
            var y = (1f - drop.position.y) * Screen.height;
            var length = Mathf.Lerp(12f, 26f, drop.speed);
            var width = Mathf.Max(3f, Screen.width * 0.006f);
            GUI.color = new Color(0.60f, 0.96f, 1f, 0.24f + Mathf.Sin(drop.phase) * 0.08f);
            GUI.DrawTexture(new Rect(x - width * 0.5f, y, width, length), glowTexture,
                ScaleMode.StretchToFill, true);
        }

        GUI.color = Color.white;
    }

    private void DrawBubbles()
    {
        for (var i = 0; i < bubbles.Length; i++)
        {
            var bubble = bubbles[i];
            var pulse = 0.78f + Mathf.Sin(bubble.phase) * 0.20f;
            var size = Mathf.Max(5f, bubble.size * Screen.width * pulse);
            var x = bubble.position.x * Screen.width;
            var y = (1f - bubble.position.y) * Screen.height;
            GUI.color = new Color(0.52f, 0.96f, 1f, 0.12f * pulse);
            GUI.DrawTexture(new Rect(x - size * 0.5f, y - size * 0.5f, size, size),
                glowTexture, ScaleMode.StretchToFill, true);
        }

        GUI.color = Color.white;
    }

    private void DrawFishShadows()
    {
        // Soft fake shadows keep the fish visually grounded; they are drawn before
        // the fish texture so each fish body overlaps its own shadow.
        if (appPaused || fishRenderSize.x <= 0)
        {
            return;
        }

        DrawFishShadow(fish.position, fish.depth, EmperorLengthFraction);
        for (var i = 0; i < simpleFish.Count; i++)
        {
            DrawFishShadow(simpleFish[i].position, simpleFish[i].depth, SimpleFishLengthFractions[i]);
        }
    }

    private void DrawFishShadow(Vector2 fishPosition, float depth, float lengthFraction)
    {
        var depthScale = 0.75f + 0.5f * depth;
        var sizeFactor = lengthFraction / EmperorLengthFraction;
        var width = Screen.height * 0.10f * sizeFactor * depthScale;
        var height = width * 0.42f;
        var x = fishPosition.x * Screen.width;
        var y = fishPosition.y * Screen.height + height * 0.7f;
        GUI.color = new Color(0.005f, 0.015f, 0.04f, 0.15f * depthScale);
        GUI.DrawTexture(new Rect(x - width * 0.5f, y - height * 0.5f, width, height),
            glowTexture, ScaleMode.StretchToFill, true);
        GUI.color = Color.white;
    }

    private void DrawPellets()
    {
        for (var i = 0; i < foodPellets.Count; i++)
        {
            var pellet = foodPellets[i];
            if (!pellet.alive)
            {
                continue;
            }

            var x = pellet.position.x * Screen.width;
            var y = pellet.position.y * Screen.height;
            var pulse = 0.8f + 0.2f * Mathf.Sin(elapsed * 7f + pellet.age * 5f);
            var size = Screen.height * 0.02f * pulse;
            GUI.color = new Color(1f, 0.72f, 0.35f, 0.95f);
            GUI.DrawTexture(new Rect(x - size * 0.5f, y - size * 0.5f, size, size),
                glowTexture, ScaleMode.StretchToFill, true);
        }

        GUI.color = Color.white;
    }

    private void DrawEatFlash()
    {
        if (eatFlashTime <= 0f)
        {
            return;
        }

        var size = Screen.height * 0.09f * (1f - eatFlashTime * 0.4f);
        var x = eatFlashPosition.x * Screen.width;
        var y = eatFlashPosition.y * Screen.height;
        GUI.color = new Color(1f, 0.75f, 0.4f, Mathf.Clamp01(eatFlashTime * 1.4f));
        GUI.DrawTexture(new Rect(x - size * 0.5f, y - size * 0.5f, size, size),
            glowTexture, ScaleMode.StretchToFill, true);
        GUI.color = Color.white;
    }

    private void DrawMotes()
    {
        if (motes == null || appPaused)
        {
            return;
        }

        for (var i = 0; i < motes.Length; i++)
        {
            var mote = motes[i];
            var pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 0.7f + mote.phase * 2f);
            var size = mote.size * (0.85f + 0.35f * pulse);
            var x = mote.position.x * Screen.width;
            var y = mote.position.y * Screen.height;
            GUI.color = new Color(0.6f, 0.95f, 1f, 0.05f + pulse * 0.09f);
            GUI.DrawTexture(new Rect(x - size * 0.5f, y - size * 0.5f, size, size),
                glowTexture, ScaleMode.StretchToFill, true);
        }

        GUI.color = Color.white;
    }

    private void DrawRays()
    {
        if (appPaused)
        {
            return;
        }

        for (var i = 0; i < RayCount; i++)
        {
            var sway = Mathf.Sin(elapsed * 0.05f + i * 2.1f) * 0.05f;
            var x = Screen.width * (0.16f + i * 0.24f + sway);
            var width = Screen.width * 0.11f;
            var alpha = 0.035f + 0.02f * (0.5f + 0.5f * Mathf.Sin(elapsed * 0.3f + i * 1.7f));
            GUI.color = new Color(0.72f, 0.98f, 1f, alpha);
            GUI.DrawTexture(new Rect(x - width * 0.5f, -Screen.height * 0.08f, width, Screen.height * 0.58f),
                glowTexture, ScaleMode.StretchToFill, true);
        }

        GUI.color = Color.white;
    }

    private void HandlePauseInput()
    {
        var pauseRect = PauseButtonRect();
        for (var i = 0; i < Input.touchCount; i++)
        {
            var touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Began)
            {
                continue;
            }

            var guiPosition = new Vector2(touch.position.x, Screen.height - touch.position.y);
            if (pauseRect.Contains(guiPosition))
            {
                SetPaused(!appPaused);
                return;
            }

            if (appPaused)
            {
                var buttonWidth = Screen.width * 0.46f;
                var buttonHeight = Screen.height * 0.085f;
                var resumeRect = new Rect((Screen.width - buttonWidth) * 0.5f, Screen.height * 0.44f,
                    buttonWidth, buttonHeight);
                if (resumeRect.Contains(guiPosition))
                {
                    SetPaused(false);
                    return;
                }
            }
        }
    }

    private Rect PauseButtonRect()
    {
        var margin = Screen.width * 0.055f;
        return new Rect(Screen.width - margin - PauseButtonSize, margin, PauseButtonSize, PauseButtonSize);
    }

    private void DrawPauseButton()
    {
        var rect = PauseButtonRect();

        // Two soft bars as the pause glyph.
        var barWidth = 8f;
        var barHeight = 24f;
        var centerX = rect.x + rect.width * 0.5f;
        var top = rect.y + (rect.height - barHeight) * 0.5f;
        GUI.color = new Color(0.85f, 1f, 1f, 0.95f);
        GUI.DrawTexture(new Rect(centerX - barWidth - 4f, top, barWidth, barHeight),
            glowTexture, ScaleMode.StretchToFill, true);
        GUI.DrawTexture(new Rect(centerX + 4f, top, barWidth, barHeight),
            glowTexture, ScaleMode.StretchToFill, true);
        GUI.color = Color.white;
    }

    private void DrawPausedOverlay()
    {
        GUI.color = new Color(0f, 0.01f, 0.03f, 0.55f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), glowTexture,
            ScaleMode.StretchToFill, true);
        GUI.color = Color.white;

        var buttonWidth = Screen.width * 0.46f;
        var buttonHeight = Screen.height * 0.085f;
        var button = new Rect((Screen.width - buttonWidth) * 0.5f, Screen.height * 0.44f,
            buttonWidth, buttonHeight);

        GUI.Label(new Rect(button.x, button.y - buttonHeight * 1.15f, buttonWidth, buttonHeight * 1.1f),
            "PAUSED", titleStyle);
        GUI.Label(new Rect(button.x, button.y + buttonHeight * 1.05f, buttonWidth, buttonHeight * 0.8f),
            "TAP TO RESUME", subtitleStyle);
    }

    private void SetPaused(bool paused)
    {
        if (appPaused == paused)
        {
            return;
        }

        appPaused = paused;
        if (paused)
        {
            Debug.Log("PAUSE: simulation paused.");
        }
        else
        {
            Debug.Log("PAUSE: simulation resumed.");
            OnResumeFromPause();
        }
    }

    private void OnResumeFromPause()
    {
        // Natural interstitial-ad moment: the user has finished an idle break and
        // is returning to the aquarium. To plug AdMob here, load an interstitial
        // in SetPaused(true) and call Show() from this method once ready.
        Debug.Log("ADSPOT: interstitial trigger reached (pause -> resume).");
    }

    private void DrawHeader()
    {
        var margin = Screen.width * 0.055f;
        var panel = new Rect(margin, margin, Screen.width - margin * 2f, 92f);
        GUI.color = new Color(0.005f, 0.035f, 0.13f, 0.72f);
        GUI.Box(panel, GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(new Rect(panel.x + 18f, panel.y + 9f, panel.width - 36f, 40f), "AQUA FLOW", titleStyle);
        GUI.Label(new Rect(panel.x + 18f, panel.y + 51f, panel.width - 36f, 25f),
            "REAL-TIME RIPPLE SIMULATION", subtitleStyle);
    }

    private void DrawFooter()
    {
        var panelHeight = 66f;
        var panel = new Rect(0f, Screen.height - panelHeight, Screen.width, panelHeight);
        var instruction = Application.isMobilePlatform ? "TOUCH THE WATER TO CREATE WAVES" : "CLICK THE WATER TO CREATE WAVES";
        GUI.color = new Color(0.005f, 0.035f, 0.13f, 0.70f);
        GUI.Box(panel, GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(new Rect(0f, panel.y + 11f, Screen.width, 24f), instruction, footerStyle);
        GUI.Label(new Rect(0f, panel.y + 35f, Screen.width, 20f), "TILT GENTLY  •  WATCH THE FLOW", footerStyle);

        if (rippleFlash > 0f)
        {
            GUI.color = new Color(0.55f, 1f, 1f, rippleFlash * 0.12f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), glowTexture,
                ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
        }
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 29,
            fontStyle = FontStyle.Bold
        };
        titleStyle.normal.textColor = new Color(0.70f, 1f, 1f);

        subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 13,
            fontStyle = FontStyle.Bold
        };
        subtitleStyle.normal.textColor = new Color(0.46f, 0.80f, 0.94f);

        footerStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            fontStyle = FontStyle.Bold
        };
        footerStyle.normal.textColor = new Color(0.64f, 0.88f, 0.96f);
    }

    private void EnsurePortraitScreen()
    {
        if (Screen.orientation != ScreenOrientation.Portrait)
        {
            Screen.orientation = ScreenOrientation.Portrait;
        }
    }

    private void EnsureAudio()
    {
        if (audioSource != null)
        {
            return;
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0.12f;
        rippleClip = CreateToneClip("WaterRipple", 440f, 180f, 0.13f, 0.18f);
    }

    private void PlayRippleSound()
    {
        if (audioSource != null && rippleClip != null && Time.timeScale > 0f)
        {
            audioSource.PlayOneShot(rippleClip);
        }
    }

    private static AudioClip CreateToneClip(string clipName, float startFrequency,
        float endFrequency, float duration, float amplitude)
    {
        const int sampleRate = 44100;
        var sampleCount = Mathf.RoundToInt(sampleRate * duration);
        var samples = new float[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var progress = (float)i / sampleCount;
            var frequency = Mathf.Lerp(startFrequency, endFrequency, progress);
            var envelope = Mathf.Exp(-15f * progress);
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / sampleRate) * envelope * amplitude;
        }

        var clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void EnsureFishScene()
    {
        if (fishCamera != null)
        {
            return;
        }

        fishRoot = new GameObject("AquaFlow_3D_Fish");
        fishRoot.layer = FishLayer;
        fishRoot.transform.position = new Vector3(0f, 0f, 0f);

        fishPivot = new GameObject("AquaFlow_EmperorPivot");
        fishPivot.layer = FishLayer;
        fishPivot.transform.SetParent(fishRoot.transform, false);
        SpawnEmperorModel();

        fishCamera = new GameObject("AquaFlow_3D_FishCamera").AddComponent<Camera>();
        fishCamera.clearFlags = CameraClearFlags.SolidColor;
        fishCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        fishCamera.orthographic = false;
        fishCamera.fieldOfView = FishCameraFov;
        fishCamera.nearClipPlane = 0.1f;
        fishCamera.farClipPlane = 30f;
        fishCamera.cullingMask = 1 << FishLayer;
        fishCamera.allowHDR = false;
        fishCamera.allowMSAA = false;
        fishCamera.useOcclusionCulling = false;
        fishCamera.transform.position = new Vector3(0f, 0f, -10f);
        fishCamera.transform.rotation = Quaternion.identity;
        fishLight = fishRoot.AddComponent<Light>();
        fishLight.type = LightType.Directional;
        fishLight.color = new Color(0.55f, 0.86f, 1f);
        fishLight.intensity = 1.9f;
        fishLight.cullingMask = 1 << FishLayer;
        fishLight.transform.localRotation = Quaternion.Euler(28f, -35f, -22f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.26f, 0.32f, 0.42f);
        fishCamera.transform.rotation = Quaternion.identity;
        fishCamera.enabled = false;
        fishRenderSize = new Vector2Int(256, 440);
        fishRenderTexture = CreateFishRenderTexture();
        fishRenderTextureBack = CreateFishRenderTexture();
        fishCamera.targetTexture = fishRenderTextureBack;
        // Manual LateUpdate rendering keeps the front texture stable while OnGUI
        // composites it. The camera always renders into the separate back buffer.
        fishCamera.enabled = false;
        EnsureSimpleFish();
    }

    private RenderTexture CreateFishRenderTexture()
    {
        var texture = new RenderTexture(fishRenderSize.x, fishRenderSize.y, 24,
            RenderTextureFormat.ARGB32)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            antiAliasing = 1,
            useMipMap = false,
            autoGenerateMips = false
        };
        texture.Create();
        return texture;
    }

    private void SpawnEmperorModel()
    {
        var prefab = Resources.Load<GameObject>(EmperorPrefabResource);
        if (prefab == null)
        {
            Debug.LogError("Emperor Angelfish prefab missing from Resources: " + EmperorPrefabResource);
            return;
        }

        fishModel = Instantiate(prefab, fishPivot.transform, false);
        fishModel.name = "EmperorAngelfish";
        SetLayerRecursively(fishModel.transform, FishLayer);

        fishAnimator = fishModel.GetComponentInChildren<Animator>();
        if (fishAnimator != null)
        {
            fishAnimator.updateMode = AnimatorUpdateMode.Normal;
            fishAnimator.applyRootMotion = false;
            fishAnimator.Update(0f);
        }

        fishRenderer = fishModel.GetComponentInChildren<SkinnedMeshRenderer>();
        if (fishRenderer == null)
        {
            Debug.LogError("Emperor Angelfish prefab has no SkinnedMeshRenderer.");
            return;
        }

        // The fish is rendered by a manually positioned camera. Keep the skinned
        // bounds live even while the model crosses the camera frustum; otherwise
        // Unity can cull one animation pose and show it again on the next frame.
        fishRenderer.updateWhenOffscreen = true;
        fishRenderer.skinnedMotionVectors = false;
        fishRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        var head = MarkerCenter(fishModel.transform, HeadMarkerTokens);
        var tail = MarkerCenter(fishModel.transform, TailMarkerTokens);
        var dorsal = MarkerCenter(fishModel.transform, DorsalMarkerTokens);
        var ventral = MarkerCenter(fishModel.transform, VentralMarkerTokens);
        if (!head.HasValue || !tail.HasValue || !dorsal.HasValue || !ventral.HasValue)
        {
            Debug.LogError("Emperor rig markers not found; head=" + head.HasValue +
                " tail=" + tail.HasValue + " dorsal=" + dorsal.HasValue + " ventral=" + ventral.HasValue);
            return;
        }

        // Work in the prefab root's local space.
        var headLocal = fishModel.transform.InverseTransformPoint(head.Value);
        var tailLocal = fishModel.transform.InverseTransformPoint(tail.Value);
        var dorsalLocal = fishModel.transform.InverseTransformPoint(dorsal.Value);
        var ventralLocal = fishModel.transform.InverseTransformPoint(ventral.Value);

        fishLocalForward = (headLocal - tailLocal).normalized;
        fishLocalUp = dorsalLocal - ventralLocal;
        fishLocalUp -= fishLocalForward * Vector3.Dot(fishLocalUp, fishLocalForward);
        fishLocalUp = fishLocalUp.normalized;

        // Center the model mass on the pivot, then align its nose with world +X.
        var centerLocal = fishModel.transform.InverseTransformPoint(fishRenderer.bounds.center);
        var alignRotation = Quaternion.FromToRotation(fishLocalForward, Vector3.right);
        var roll = Vector3.SignedAngle(alignRotation * fishLocalUp, Vector3.up, Vector3.right);
        fishModel.transform.localRotation = Quaternion.AngleAxis(roll, Vector3.right) * alignRotation;
        // The centering offset lives on the model in unscaled model units, so the
        // fit scale must be applied on fishRoot (an ancestor) for the offsets to
        // cancel out and keep the fish centered on its pivot.
        fishModel.transform.localPosition = -(fishModel.transform.localRotation * centerLocal);
        fishModel.transform.localScale = Vector3.one;
        fishRoot.transform.localScale = Vector3.one * 0.02f;

        fishLocalLength = 0f;
        fishLocalHeight = 0f;
        fishCalibrateTimer = 0f;
        fishCalibrated = false;
        Debug.Log("EMPEROR: prefab spawned. mesh=" +
            (fishRenderer.sharedMesh != null ? fishRenderer.sharedMesh.name : "<none>") +
            " animator=" + (fishAnimator != null) +
            " fwd=" + fishLocalForward + " up=" + fishLocalUp);
    }

    private static void SetLayerRecursively(Transform current, int layer)
    {
        current.gameObject.layer = layer;
        for (var i = 0; i < current.childCount; i++)
        {
            SetLayerRecursively(current.GetChild(i), layer);
        }
    }

    private static Vector3? MarkerCenter(Transform root, string[] tokens)
    {
        var sum = Vector3.zero;
        var count = 0;
        CollectMarkers(root, tokens, ref sum, ref count);
        return count > 0 ? sum / count : (Vector3?)null;
    }

    private static void CollectMarkers(Transform current, string[] tokens, ref Vector3 sum, ref int count)
    {
        foreach (var token in tokens)
        {
            if (current.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                sum += current.position;
                count++;
                break;
            }
        }

        for (var i = 0; i < current.childCount; i++)
        {
            CollectMarkers(current.GetChild(i), tokens, ref sum, ref count);
        }
    }

    private void CalibrateFish()
    {
        if (fishCalibrated || fishModel == null || fishRenderer == null)
        {
            return;
        }

        fishCalibrateTimer += Time.deltaTime;
        var corners = FishBoundsCornersModel();
        if (corners == null)
        {
            return;
        }

        // Track the largest pose extent along the nose-tail and dorsal-ventral axes
        // while the swim animation plays, so the final fit includes fin sweeps.
        var minLength = float.MaxValue;
        var maxLength = float.MinValue;
        var minHeight = float.MaxValue;
        var maxHeight = float.MinValue;
        foreach (var corner in corners)
        {
            var alongForward = Vector3.Dot(corner, fishLocalForward);
            var alongUp = Vector3.Dot(corner, fishLocalUp);
            minLength = Mathf.Min(minLength, alongForward);
            maxLength = Mathf.Max(maxLength, alongForward);
            minHeight = Mathf.Min(minHeight, alongUp);
            maxHeight = Mathf.Max(maxHeight, alongUp);
        }

        fishLocalLength = Mathf.Max(fishLocalLength, maxLength - minLength);
        fishLocalHeight = Mathf.Max(fishLocalHeight, maxHeight - minHeight);

        if (fishLocalLength > 0.0001f)
        {
            // Gently grow from the tiny start scale toward the current best estimate.
            var hintScale = TargetFishWorldLength() / fishLocalLength;
            var current = fishRoot.transform.localScale.x;
            fishRoot.transform.localScale = Vector3.one * Mathf.Lerp(current, hintScale, 0.3f);
        }

        if (fishCalibrateTimer < 1.6f)
        {
            return;
        }

        var scaleByLength = fishLocalLength > 0.0001f
            ? TargetFishWorldLength() / fishLocalLength
            : 1f;
        var scaleByHeight = fishLocalHeight > 0.0001f
            ? FishCameraSize * 2f * EmperorHeightFraction / fishLocalHeight
            : 1f;
        fishFitScale = Mathf.Min(scaleByLength, scaleByHeight);
        fishRoot.transform.localScale = Vector3.one * fishFitScale;
        fishCalibrated = true;
        Debug.Log("EMPEROR: calibrated length=" + fishLocalLength.ToString("F3") +
            " height=" + fishLocalHeight.ToString("F3") +
            " scale=" + fishFitScale.ToString("F4"));
    }

    private float TargetFishWorldLength()
    {
        var worldWidth = FishCameraSize * 2f *
            (float)fishRenderSize.x / Mathf.Max(1, fishRenderSize.y);
        return worldWidth * EmperorLengthFraction;
    }

    private Vector3[] FishBoundsCornersModel()
    {
        var bounds = fishRenderer.bounds;
        var center = bounds.center;
        var extents = bounds.extents;
        var corners = new Vector3[8];
        var index = 0;
        for (var x = -1; x <= 1; x += 2)
        {
            for (var y = -1; y <= 1; y += 2)
            {
                for (var z = -1; z <= 1; z += 2)
                {
                    var world = center + new Vector3(extents.x * x, extents.y * y, extents.z * z);
                    corners[index++] = fishModel.transform.InverseTransformPoint(world);
                }
            }
        }

        return corners;
    }

    private void UpdateFishModel()
    {
        if (fishRoot == null || fishModel == null)
        {
            return;
        }

        CalibrateFish();

        // Depth-aware placement keeps the whole body inside the frustum at any depth.
        var halfLength = fishLocalLength * fishRoot.transform.localScale.x * 0.5f;
        var freeSwimming = emperorFleeTime <= 0f && emperorTarget == null;
        PlaceFishAt(fishRoot.transform, fish.position, fish.depth, halfLength,
            ref fish.direction, ref fish.targetDirection, freeSwimming);

        // Smooth yaw turn with banking roll into the turn and nose pitch with the bob.
        var yawTarget = fish.direction > 0f ? 0f : 180f;
        fish.yaw = Mathf.MoveTowardsAngle(fish.yaw, yawTarget, FishTurnYawSpeed * Time.deltaTime);
        var turnRate = Mathf.DeltaAngle(fish.yaw, yawTarget);
        var bank = Mathf.Clamp(turnRate * FishBankStrength, -FishBankMax, FishBankMax);
        var pitch = Mathf.Clamp(-Mathf.Cos(elapsed * 0.9f + fish.phase) * 6f, -9f, 9f);
        fishPivot.transform.localRotation =
            Quaternion.Euler(0f, fish.yaw, 0f) *
            Quaternion.Euler(0f, 0f, pitch) *
            Quaternion.Euler(bank, 0f, 0f);

        if (fishCalibrated)
        {
            fishRoot.transform.localScale = Vector3.one * fishFitScale;
        }
    }

    private void EnsureSimpleFish()
    {
        if (simpleFish.Count > 0)
        {
            return;
        }

        if (simpleFishParent == null)
        {
            // Neutral parent: the emperor's fishRoot carries its own fit scale and
            // position, so the simple fish must NOT be parented under it.
            simpleFishParent = new GameObject("AquaFlow_SimpleFish");
            simpleFishParent.transform.position = Vector3.zero;
            simpleFishParent.transform.localScale = Vector3.one;
        }

        for (var i = 0; i < SimpleFishResources.Length; i++)
        {
            var prefab = Resources.Load<GameObject>(SimpleFishResources[i]);
            if (prefab == null)
            {
                Debug.LogError("Low Poly fish prefab missing from Resources: " + SimpleFishResources[i]);
                continue;
            }

            var fish = new SimpleFish
            {
                phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
                baseY = SimpleFishBaseYs[i] + UnityEngine.Random.Range(-0.03f, 0.03f),
                position = new Vector2(UnityEngine.Random.Range(0.18f, 0.82f), 0.5f),
                depth = UnityEngine.Random.Range(0.25f, 0.75f),
                targetDepth = UnityEngine.Random.Range(0.2f, 0.8f),
                nextDepthTime = UnityEngine.Random.Range(4f, 9f),
                nextTurnTime = UnityEngine.Random.Range(3f, 8f),
                nextGlideTime = UnityEngine.Random.Range(5f, 12f),
                speedFactor = SimpleFishSpeedFactors[i],
                direction = UnityEngine.Random.value < 0.5f ? 1f : -1f,
                targetDirection = UnityEngine.Random.value < 0.5f ? 1f : -1f
            };
            fish.yaw = fish.direction > 0f ? 0f : 180f;
            fish.schoolOffset = i == 0 ? Vector2.zero
                : new Vector2(0.085f, i == 1 ? -0.02f : 0.035f);
            fish.depthOffset = i == 1 ? -0.06f : 0.07f;

            fish.root = new GameObject("AquaFlow_Fish_" + SimpleFishResources[i]);
            fish.root.layer = FishLayer;
            fish.root.transform.SetParent(simpleFishParent.transform, false);

            fish.pivot = new GameObject("Pivot");
            fish.pivot.layer = FishLayer;
            fish.pivot.transform.SetParent(fish.root.transform, false);

            fish.model = Instantiate(prefab, fish.pivot.transform, false);
            fish.model.name = SimpleFishResources[i];
            SetLayerRecursively(fish.model.transform, FishLayer);

            var filter = fish.model.GetComponentInChildren<MeshFilter>();
            fish.meshRenderer = fish.model.GetComponentInChildren<MeshRenderer>();
            if (filter == null || filter.sharedMesh == null || fish.meshRenderer == null)
            {
                Debug.LogError("Low Poly fish has no mesh: " + SimpleFishResources[i]);
                Destroy(fish.root);
                continue;
            }

            var mesh = filter.sharedMesh;
            var baseRotation = SimpleFishBaseRotations[i];
            fish.localForward = new Vector3(0f, 0f, -1f);
            fish.localUp = Vector3.up;

            // Center the mesh on the pivot and point its nose (-Z) toward +X.
            fish.model.transform.localRotation = baseRotation;
            fish.model.transform.localPosition = -(baseRotation * mesh.bounds.center);

            // Static mesh: measure the nose-tail half extent from the AABB corners.
            var halfLength = 0f;
            var halfHeight = 0f;
            foreach (var corner in MeshBoundsCorners(mesh.bounds))
            {
                halfLength = Mathf.Max(halfLength, Mathf.Abs(Vector3.Dot(corner, fish.localForward)));
                halfHeight = Mathf.Max(halfHeight, Mathf.Abs(Vector3.Dot(corner, fish.localUp)));
            }

            var aspect = (float)fishRenderSize.x / Mathf.Max(1, fishRenderSize.y);
            var targetWidth = FishCameraSize * 2f * aspect * SimpleFishLengthFractions[i];
            fish.fitScale = halfLength > 0.0001f ? targetWidth / (halfLength * 2f) : 1f;
            fish.root.transform.localScale = Vector3.one * fish.fitScale;

            fish.worldHalfLength = fish.fitScale * halfLength;
            fish.worldHalfHeight = fish.fitScale * halfHeight;
            fish.position.y = fish.baseY;
            simpleFish.Add(fish);
            Debug.Log("SIMPLEFISH spawned " + SimpleFishResources[i] +
                " scale=" + fish.fitScale.ToString("F4") +
                " halfLen=" + halfLength.ToString("F3") +
                " halfH=" + halfHeight.ToString("F3"));
        }
    }

    private static Vector3[] MeshBoundsCorners(Bounds bounds)
    {
        var corners = new Vector3[8];
        var index = 0;
        for (var x = -1; x <= 1; x += 2)
        {
            for (var y = -1; y <= 1; y += 2)
            {
                for (var z = -1; z <= 1; z += 2)
                {
                    corners[index++] = bounds.center + new Vector3(
                        bounds.extents.x * x, bounds.extents.y * y, bounds.extents.z * z);
                }
            }
        }

        return corners;
    }

    private void UpdateSimpleFishMotion(float deltaTime)
    {
        for (var i = 0; i < simpleFish.Count; i++)
        {
            var fish = simpleFish[i];
            var currentFlow = smoothedTilt.x * 0.012f;
            var cruise = FishCruiseSpeed * fish.speedFactor *
                (0.72f + 0.4f * (0.5f + 0.5f * Mathf.Sin(elapsed * 0.5f + fish.phase)));

            if (fish.fedCooldown > 0f)
            {
                fish.fedCooldown -= deltaTime;
            }

            // Flee: dart away from a recent nearby tap.
            if (fish.fleeTime > 0f)
            {
                fish.fleeTime -= deltaTime;
                fish.targetPellet = null;
                var away = fish.position - fish.fleeFrom;
                if (away.sqrMagnitude < 0.0004f)
                {
                    away = new Vector2(fish.direction > 0f ? 1f : -1f, 0f);
                }
                away.Normalize();
                fish.position += away * (cruise * 3.4f) * deltaTime;
                fish.position = ClampFishPosition(fish.position);
                fish.direction = away.x >= 0f ? 1f : -1f;
                continue;
            }

            // Feed: chase the nearest pellet when hungry.
            if (fish.fedCooldown <= 0f && (fish.targetPellet == null || !fish.targetPellet.alive))
            {
                fish.targetPellet = FindNearestPellet(fish.position, PelletChaseRadius * 0.85f);
            }

            if (fish.fedCooldown > 0f)
            {
                fish.targetPellet = null;
            }

            if (fish.targetPellet != null)
            {
                var pelletPosition = fish.targetPellet.position;
                var deltaX = pelletPosition.x - fish.position.x;
                var deltaY = pelletPosition.y - fish.position.y;
                var chaseSpeed = Mathf.Max(cruise * 2.3f, 0.07f);
                fish.position.x = Mathf.MoveTowards(fish.position.x, pelletPosition.x, chaseSpeed * deltaTime);
                fish.position.y = Mathf.MoveTowards(fish.position.y, pelletPosition.y, chaseSpeed * 0.85f * deltaTime);
                fish.position = ClampFishPosition(fish.position);

                if (Mathf.Abs(deltaX) < PelletEatRadius && Mathf.Abs(deltaY) < PelletEatRadius)
                {
                    fish.fedCooldown = SimpleFishFullCooldown;
                    EatPellet(fish.targetPellet);
                    fish.targetPellet = null;
                }
                fish.direction = deltaX >= 0f ? 1f : -1f;
                continue;
            }

            if (i == 0)
            {
                UpdateSimpleFishLeader(fish, cruise, currentFlow, deltaTime);
            }
            else
            {
                // Each companion keeps its own forward swim cycle. The previous
                // follower steering could settle at the leader's position after the
                // separation pass pushed it away, which made most fish look frozen.
                UpdateSimpleFishLeader(fish, cruise, currentFlow, deltaTime);
            }
        }
    }

    private void UpdateSimpleFishLeader(SimpleFish fish, float cruise, float currentFlow, float deltaTime)
    {
        // Occasional mid-water heading change and glides.
        fish.nextTurnTime -= deltaTime;
        if (fish.nextTurnTime <= 0f)
        {
            fish.nextTurnTime = UnityEngine.Random.Range(3f, 8f);
            if (UnityEngine.Random.value < 0.45f)
            {
                fish.targetDirection = -fish.targetDirection;
            }
        }

        fish.nextGlideTime -= deltaTime;
        if (fish.nextGlideTime <= 0f)
        {
            fish.nextGlideTime = UnityEngine.Random.Range(5f, 12f);
            fish.glideTime = UnityEngine.Random.Range(0.7f, 1.5f);
        }

        if (fish.glideTime > 0f)
        {
            fish.glideTime -= deltaTime;
            cruise *= 0.26f;
        }

        if (fish.targetDirection > 0f && fish.position.x >= 1f - SimpleFishTurnMargin)
        {
            fish.targetDirection = -1f;
        }
        else if (fish.targetDirection < 0f && fish.position.x <= SimpleFishTurnMargin)
        {
            fish.targetDirection = 1f;
        }

        fish.direction = fish.targetDirection;

        // Depth excursions.
        fish.nextDepthTime -= deltaTime;
        if (fish.nextDepthTime <= 0f)
        {
            fish.nextDepthTime = UnityEngine.Random.Range(4f, 9f);
            fish.targetDepth = UnityEngine.Random.Range(0.12f, 0.95f);
        }

        fish.depth = Mathf.Lerp(fish.depth, fish.targetDepth, 1f - Mathf.Exp(-0.4f * deltaTime));

        // Vertical: gentle bob around a slowly drifting band.
        var targetY = fish.baseY + Mathf.Sin(elapsed * 0.85f + fish.phase) * 0.03f;
        fish.position.y = Mathf.Lerp(fish.position.y, targetY, 1f - Mathf.Exp(-2.2f * deltaTime));

        // Horizontal speed follows the visual heading, so the fish slows while turning.
        var yawRadians = fish.yaw * Mathf.Deg2Rad;
        var headingX = Mathf.Cos(yawRadians);
        var turnSlow = 0.35f + 0.65f * Mathf.Abs(headingX);
        fish.position.x += (cruise + currentFlow) * turnSlow * Mathf.Sign(headingX) * deltaTime;
        fish.position.x = Mathf.Clamp(fish.position.x, SimpleFishTurnMargin, 1f - SimpleFishTurnMargin);
    }

    private void ApplyEmperorSeparation()
    {
        if (simpleFish.Count == 0)
        {
            return;
        }

        // Keep every fish separated in the composited screen view. Use projected
        // rectangular body bounds instead of large circles: circles overestimate
        // the empty space around long fish and can pin the school in place.
        for (var iteration = 0; iteration < FishSeparationIterations; iteration++)
        {
            var emperorExtents = EmperorScreenHalfExtents();
            for (var i = 0; i < simpleFish.Count; i++)
            {
                ResolveEmperorSimplePair(fish.position, emperorExtents, simpleFish[i]);
            }

            for (var i = 0; i < simpleFish.Count; i++)
            {
                for (var j = i + 1; j < simpleFish.Count; j++)
                {
                    ResolveSimpleFishPair(simpleFish[i], simpleFish[j]);
                }
            }
        }
    }

    private Vector2 EmperorScreenHalfExtents()
    {
        var scale = FishViewScale(fish.depth);
        var aspect = (float)fishRenderSize.x / Mathf.Max(1, fishRenderSize.y);
        var worldWidth = FishCameraSize * 2f * aspect * scale;
        var worldHeight = FishCameraSize * 2f * scale;
        var halfLength = fishLocalLength > 0.0001f
            ? fishLocalLength * fishRoot.transform.localScale.x * 0.5f
            : worldWidth * EmperorLengthFraction * 0.5f;
        var halfHeight = fishLocalHeight > 0.0001f
            ? fishLocalHeight * fishRoot.transform.localScale.x * 0.5f
            : halfLength * 0.42f;
        return new Vector2(
            halfLength / Mathf.Max(0.001f, worldWidth) + FishSeparationPadding,
            halfHeight / Mathf.Max(0.001f, worldHeight) + FishSeparationPadding);
    }

    private Vector2 SimpleFishScreenHalfExtents(SimpleFish fish)
    {
        var scale = FishViewScale(fish.depth);
        var aspect = (float)fishRenderSize.x / Mathf.Max(1, fishRenderSize.y);
        var worldWidth = FishCameraSize * 2f * aspect * scale;
        var worldHeight = FishCameraSize * 2f * scale;
        return new Vector2(
            fish.worldHalfLength / Mathf.Max(0.001f, worldWidth) + FishSeparationPadding,
            fish.worldHalfHeight / Mathf.Max(0.001f, worldHeight) + FishSeparationPadding);
    }

    private void ResolveEmperorSimplePair(Vector2 fixedPosition, Vector2 fixedExtents,
        SimpleFish movingFish)
    {
        ResolveAabbPair(fixedPosition, fixedExtents, movingFish, SimpleFishScreenHalfExtents(movingFish));
    }

    private void ResolveSimpleFishPair(SimpleFish first, SimpleFish second)
    {
        var firstExtents = SimpleFishScreenHalfExtents(first);
        var secondExtents = SimpleFishScreenHalfExtents(second);
        ResolveAabbPair(first.position, firstExtents, second, secondExtents);
    }

    private void ResolveAabbPair(Vector2 fixedPosition, Vector2 fixedExtents,
        SimpleFish movingFish, Vector2 movingExtents)
    {
        var delta = movingFish.position - fixedPosition;
        var overlapX = fixedExtents.x + movingExtents.x - Mathf.Abs(delta.x);
        var overlapY = fixedExtents.y + movingExtents.y - Mathf.Abs(delta.y);
        if (overlapX <= 0f || overlapY <= 0f)
        {
            return;
        }

        // Resolve along the shallowest penetration so the fish keep swimming in the
        // other axis instead of being repeatedly stopped in the travel direction.
        if (overlapX < overlapY)
        {
            var directionX = delta.x >= 0f ? 1f : -1f;
            if (Mathf.Abs(delta.x) < 0.0001f)
            {
                directionX = 1f;
            }
            movingFish.position.x = fixedPosition.x +
                directionX * (fixedExtents.x + movingExtents.x);
        }
        else
        {
            var directionY = delta.y >= 0f ? 1f : -1f;
            if (Mathf.Abs(delta.y) < 0.0001f)
            {
                directionY = 1f;
            }
            movingFish.position.y = fixedPosition.y +
                directionY * (fixedExtents.y + movingExtents.y);
        }

        movingFish.position = ClampFishPosition(movingFish.position);
    }

    private void UpdateSimpleFishFollower(SimpleFish fish, SimpleFish leader, float cruise, float deltaTime)
    {
        // Trail the leader from behind with a personal offset and gentle wobble.
        var desired = new Vector2(
            leader.position.x - leader.direction * fish.schoolOffset.x,
            leader.position.y + fish.schoolOffset.y);
        desired.x += Mathf.Sin(elapsed * 1.1f + fish.phase) * 0.016f;
        desired.y += Mathf.Cos(elapsed * 0.9f + fish.phase) * 0.012f;

        // Simple separation so followers do not stack on each other.
        for (var j = 0; j < simpleFish.Count; j++)
        {
            var other = simpleFish[j];
            if (other == fish)
            {
                continue;
            }

            var diff = fish.position - other.position;
            var distance = diff.magnitude;
            if (distance < 0.085f && distance > 0.0001f)
            {
                desired += diff / distance * (0.085f - distance) * 2.0f;
            }
        }

        var steer = Mathf.Max(cruise * 2.2f, 0.05f);
        fish.position.x = Mathf.MoveTowards(fish.position.x, desired.x, steer * deltaTime);
        fish.position.y = Mathf.MoveTowards(fish.position.y, desired.y, steer * 0.85f * deltaTime);
        fish.position = ClampFishPosition(fish.position);

        // Follow the leader's depth loosely, with a personal offset.
        fish.targetDepth = leader.depth + fish.depthOffset;
        fish.depth = Mathf.Lerp(fish.depth, fish.targetDepth, 1f - Mathf.Exp(-1.1f * deltaTime));

        fish.direction = desired.x >= fish.position.x ? 1f : -1f;
    }

    private void UpdateSimpleFishModel()
    {
        if (fishRenderSize.x <= 0)
        {
            return;
        }

        for (var i = 0; i < simpleFish.Count; i++)
        {
            var fish = simpleFish[i];
            var freeSwimming = fish.fleeTime <= 0f && fish.targetPellet == null;
            PlaceFishAt(fish.root.transform, fish.position, fish.depth,
                fish.worldHalfLength, ref fish.direction, ref fish.targetDirection, freeSwimming);

            // Smooth yaw turn with banking roll and nose pitch.
            var yawTarget = fish.direction > 0f ? 0f : 180f;
            fish.yaw = Mathf.MoveTowardsAngle(fish.yaw, yawTarget, FishTurnYawSpeed * Time.deltaTime);
            var turnRate = Mathf.DeltaAngle(fish.yaw, yawTarget);
            var bank = Mathf.Clamp(turnRate * FishBankStrength, -FishBankMax, FishBankMax);
            var pitch = Mathf.Clamp(-Mathf.Cos(elapsed * 0.85f + fish.phase) * 5f, -8f, 8f);
            fish.pivot.transform.localRotation =
                Quaternion.Euler(0f, fish.yaw, 0f) *
                Quaternion.Euler(0f, 0f, pitch) *
                Quaternion.Euler(bank, 0f, 0f);
        }
    }

    private void RenderFish()
    {
        if (fishCamera == null || fishRenderTexture == null || fishRenderTextureBack == null)
        {
            return;
        }

        if (!fishRenderTexture.IsCreated())
        {
            fishRenderTexture.Create();
        }

        if (!fishRenderTextureBack.IsCreated())
        {
            fishRenderTextureBack.Create();
        }

        // Never render into the texture sampled by OnGUI. This prevents a shared
        // all-fish blink when the Android tile renderer resolves the render target.
        fishCamera.targetTexture = fishRenderTextureBack;
        var previousActive = RenderTexture.active;
        RenderTexture.active = fishRenderTextureBack;
        GL.Clear(true, true, Color.clear);
        fishCamera.Render();

        // Swap references only after the camera has completed its frame. Avoiding a
        // Graphics.Blit here removes the extra GPU resolve/copy that could expose a
        // partially updated all-fish texture on the phone.
        var completedFrame = fishRenderTextureBack;
        fishRenderTextureBack = fishRenderTexture;
        fishRenderTexture = completedFrame;
        fishCamera.targetTexture = fishRenderTextureBack;
        RenderTexture.active = previousActive;
    }

    private static Texture2D CreateGlowTexture(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color[size * size];

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = (x + 0.5f) / size * 2f - 1f;
                var dy = (y + 0.5f) / size * 2f - 1f;
                var distance = Mathf.Sqrt(dx * dx + dy * dy);
                var alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.6f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void OnDestroy()
    {
        if (waterTexture != null)
        {
            Destroy(waterTexture);
        }

        if (glowTexture != null)
        {
            Destroy(glowTexture);
        }

        if (fishRoot != null)
        {
            Destroy(fishRoot);
        }

        if (simpleFishParent != null)
        {
            Destroy(simpleFishParent);
        }

        if (pelletsParent != null)
        {
            Destroy(pelletsParent);
        }

        if (fishRenderTexture != null)
        {
            fishRenderTexture.Release();
            Destroy(fishRenderTexture);
        }

        if (fishRenderTextureBack != null)
        {
            fishRenderTextureBack.Release();
            Destroy(fishRenderTextureBack);
        }

        if (rippleClip != null)
        {
            Destroy(rippleClip);
        }
    }
}
