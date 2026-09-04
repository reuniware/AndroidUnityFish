using UnityEngine;

public sealed class HelloAndroid : MonoBehaviour
{
    private const int WaterWidth = 112;
    private const int WaterHeight = 192;
    private const int BubbleCount = 18;
    private const int RainDropCount = 7;
    private const float SimulationSpeed = 42f;
    private const int FishLayer = 8;
    private const float FishCameraSize = 1.55f;
    private const float FishBaseSizeRatio = 0.22f;
    private const string EmperorPrefabResource = "EmperorAngelfish_swim1";
    private const float FishCruiseSpeed = 0.07f;
    private const float EmperorLengthFraction = 0.44f;
    private const float EmperorHeightFraction = 0.40f;
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

    private struct Fish
    {
        public Vector2 position;
        public float baseY;
        public float speed;
        public float size;
        public float phase;
        public float direction;
    }

    private Texture2D waterTexture;
    private Texture2D glowTexture;
    private RenderTexture fishRenderTexture;
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
    private Color[] waterPixels;
    private float[] heights;
    private float[] velocities;
    private Bubble[] bubbles;
    private RainDrop[] rainDrops;
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

        var deltaTime = Mathf.Min(Time.deltaTime, 0.033f);
        elapsed += deltaTime;
        rippleFlash = Mathf.MoveTowards(rippleFlash, 0f, deltaTime * 2.4f);

        var tilt = new Vector2(Input.acceleration.x, -Input.acceleration.y);
        smoothedTilt = Vector2.Lerp(smoothedTilt, tilt, 1f - Mathf.Exp(-4.5f * deltaTime));
        SimulateWater(deltaTime);
        UpdateRain(deltaTime);
        UpdateBubbles(deltaTime);
        UpdateFish(deltaTime);
        EnsureFishScene();
        UpdateFishModel();
        HandleTouchRipples();
        RenderWaterTexture();
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

        fish = new Fish
        {
            position = new Vector2(0.18f, 0.43f),
            baseY = 0.43f,
            speed = 0.075f,
            size = 0.22f,
            phase = 0.8f,
            direction = 1f
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

    private void UpdateFish(float deltaTime)
    {
        var currentFlow = smoothedTilt.x * 0.018f;
        var cruise = FishCruiseSpeed * (0.72f + 0.4f * (0.5f + 0.5f * Mathf.Sin(elapsed * 0.62f + fish.phase * 1.7f)));
        fish.position.x += (cruise + currentFlow) * fish.direction * deltaTime;
        fish.position.y = fish.baseY + Mathf.Sin(elapsed * 0.9f + fish.phase) * 0.035f;

        // Turn around near the screen edges instead of teleporting across.
        const float turnMargin = 0.10f;
        if (fish.direction > 0f && fish.position.x >= 1f - turnMargin)
        {
            fish.direction = -1f;
        }
        else if (fish.direction < 0f && fish.position.x <= turnMargin)
        {
            fish.direction = 1f;
        }
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

            var normalized = ScreenToNormalized(touch.position);
            AddRipple(normalized.x, normalized.y, touch.phase == TouchPhase.Began ? 0.58f : 0.20f);
            lastTouchPosition = touch.position;
            rippleFlash = 1f;
        }

#if UNITY_EDITOR
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

                var cellX = centerX + x;
                var cellY = centerY + y;
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
        DrawFishRenderTexture();
        DrawRain();
        DrawBubbles();
        DrawHeader();
        DrawFooter();
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
        fishCamera.orthographic = true;
        fishCamera.orthographicSize = FishCameraSize;
        fishCamera.nearClipPlane = 0.1f;
        fishCamera.farClipPlane = 20f;
        fishCamera.cullingMask = 1 << FishLayer;
        fishCamera.allowHDR = true;
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
        fishRenderTexture = new RenderTexture(fishRenderSize.x, fishRenderSize.y, 16,
            RenderTextureFormat.ARGB32)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            antiAliasing = 2,
            useMipMap = false,
            autoGenerateMips = false
        };
        fishCamera.targetTexture = fishRenderTexture;
        fishCamera.enabled = false;
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

        var aspect = (float)fishRenderSize.x / Mathf.Max(1, fishRenderSize.y);
        var worldWidth = FishCameraSize * 2f * aspect;
        var worldHeight = FishCameraSize * 2f;
        var x = (fish.position.x - 0.5f) * worldWidth;
        var y = (0.5f - fish.position.y) * worldHeight;
        fishRoot.transform.position = new Vector3(x, y, 0f);
        fishPivot.transform.localRotation = Quaternion.Euler(0f, fish.direction > 0f ? 0f : 180f, 0f);
        if (fishCalibrated)
        {
            fishRoot.transform.localScale = Vector3.one * fishFitScale;
        }
    }

    private void RenderFish()
    {
        if (fishCamera == null || fishRenderTexture == null)
        {
            return;
        }

        fishCamera.Render();
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

        if (fishRenderTexture != null)
        {
            fishRenderTexture.Release();
            Destroy(fishRenderTexture);
        }

        if (rippleClip != null)
        {
            Destroy(rippleClip);
        }
    }
}
