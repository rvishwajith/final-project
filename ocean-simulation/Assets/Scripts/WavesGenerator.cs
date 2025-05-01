/// Rohith Vishwajith
/// WavesGenerator.cs

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Generates multi-scale ocean waves using cascaded FFT-based spectra.
/// Refactored to use arrays for cascades and length scales.
/// </summary>
public class WavesGenerator : MonoBehaviour
{
    // --- Inspector Fields ---
    [SerializeField] private int size = 512;
    [SerializeField] private WavesSettings wavesSettings;
    [SerializeField] private bool alwaysRecalculateInitials = false;

    // Default length scales: 250, 17, 5
    [SerializeField] private float[] lengthScales = new float[] { 250f, 17f, 5f };

    [SerializeField] private ComputeShader fftShader;
    [SerializeField] private ComputeShader initialSpectrumShader;
    [SerializeField] private ComputeShader timeDependentSpectrumShader;
    [SerializeField] private ComputeShader texturesMergerShader;

    // --- Runtime Fields ---
    public WavesCascade[] cascades;
    private FastFourierTransform fft;
    private Texture2D gaussianNoise;
    private Texture2D physicsReadback;

    /// <summary>
    /// Initialize FFT, noise, cascades and compute initial spectra.
    /// </summary>
    private void Awake()
    {
        Application.targetFrameRate = -1;
        fft = new FastFourierTransform(size, fftShader);
        gaussianNoise = GetNoiseTexture(size);

        cascades = new WavesCascade[lengthScales.Length];
        for (int i = 0; i < cascades.Length; i++)
        {
            cascades[i] = new WavesCascade(
                size,
                initialSpectrumShader,
                timeDependentSpectrumShader,
                texturesMergerShader,
                fft,
                gaussianNoise);
        }

        InitialiseCascades();
        physicsReadback = new Texture2D(size, size, TextureFormat.RGBAFloat, false);
    }

    /// <summary>
    /// Computes and dispatches initial spectra for each cascade.
    /// </summary>
    private void InitialiseCascades()
    {
        // Compute cutoffs between cascades
        float boundary1 = 2f * Mathf.PI / lengthScales[1] * 6f;
        float boundary2 = 2f * Mathf.PI / lengthScales[2] * 6f;

        // Assign cutoffs for three cascades
        cascades[0].CalculateInitials(wavesSettings, lengthScales[0], 0.0001f, boundary1);
        cascades[1].CalculateInitials(wavesSettings, lengthScales[1], boundary1, boundary2);
        cascades[2].CalculateInitials(wavesSettings, lengthScales[2], boundary2, float.MaxValue);

        // Expose global length scales to shaders
        for (int i = 0; i < lengthScales.Length; i++)
        {
            Shader.SetGlobalFloat($"LengthScale{i}", lengthScales[i]);
        }
    }

    /// <summary>
    /// Update wave states and request GPU readback each frame.
    /// </summary>
    private void Update()
    {
        if (alwaysRecalculateInitials)
        {
            InitialiseCascades();
        }

        float time = Time.time;
        foreach (var cascade in cascades)
        {
            cascade.CalculateWavesAtTime(time);
        }

        RequestReadbacks();
    }

    /// <summary>
    /// Request async readback of displacement data from first cascade.
    /// </summary>
    private void RequestReadbacks()
    {
        AsyncGPUReadback.Request(
            cascades[0].Displacement,
            0,
            TextureFormat.RGBAFloat,
            OnCompleteReadback);
    }

    /// <summary>
    /// Dispose cascades when destroyed.
    /// </summary>
    private void OnDestroy()
    {
        foreach (var cascade in cascades)
        {
            cascade.Dispose();
        }
    }

    // --- Noise Texture Helpers ---
    private Texture2D GetNoiseTexture(int size)
    {
        string filename = $"GaussianNoiseTexture{size}x{size}";
        Texture2D noise = Resources.Load<Texture2D>($"GaussianNoiseTextures/{filename}");
        return noise ? noise : GenerateNoiseTexture(size, true);
    }

    private Texture2D GenerateNoiseTexture(int size, bool saveIntoAssetFile)
    {
        Texture2D noise = new Texture2D(size, size, TextureFormat.RGFloat, false, true)
        {
            filterMode = FilterMode.Point
        };

        for (int i = 0; i < size; i++)
            for (int j = 0; j < size; j++)
                noise.SetPixel(i, j, new Vector4(NormalRandom(), NormalRandom()));

        noise.Apply();

#if UNITY_EDITOR
        if (saveIntoAssetFile)
        {
            string filename = "GaussianNoiseTexture" + size.ToString() + "x" + size.ToString();
            string path = "Assets/Resources/GaussianNoiseTextures/";
            AssetDatabase.CreateAsset(noise, path + filename + ".asset");
            Debug.Log($"Created noise texture at {path}{filename}.asset");
        }
#endif
        return noise;
    }

    private float NormalRandom()
    {
        return Mathf.Cos(2f * Mathf.PI * Random.value)
             * Mathf.Sqrt(-2f * Mathf.Log(Random.value));
    }

    // --- Water Sampling ---
    public float GetWaterHeight(Vector3 position)
    {
        Vector3 disp = GetWaterDisplacement(position);
        disp = GetWaterDisplacement(position - disp);
        disp = GetWaterDisplacement(position - disp);
        return GetWaterDisplacement(position - disp).y;
    }

    public Vector3 GetWaterDisplacement(Vector3 position)
    {
        Color c = physicsReadback.GetPixelBilinear(
            position.x / lengthScales[0],
            position.z / lengthScales[0]);
        return new Vector3(c.r, c.g, c.b);
    }

    // --- GPU Readback Callback ---
    private void OnCompleteReadback(AsyncGPUReadbackRequest request)
        => OnCompleteReadback(request, physicsReadback);

    private void OnCompleteReadback(AsyncGPUReadbackRequest request, Texture2D result)
    {
        if (request.hasError)
        {
            Debug.LogError("GPU readback error detected.");
            return;
        }
        result.LoadRawTextureData(request.GetData<Color>());
        result.Apply();
    }
}
