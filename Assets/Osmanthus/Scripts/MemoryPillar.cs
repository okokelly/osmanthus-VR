using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
[RequireComponent(typeof(XRSimpleInteractable))]
[RequireComponent(typeof(AudioSource))]
public sealed class MemoryPillar : MonoBehaviour
{
    [SerializeField] private int orderIndex;
    [SerializeField] private string memoryName = "Memory";
    [SerializeField] private Color activeColor = new Color(1f, 0.58f, 0.12f, 1f);
    [SerializeField] private Renderer pillarRenderer;
    [SerializeField] private GameObject memoryPlane;
    [SerializeField] private ParticleSystem feedbackParticles;
    [SerializeField] private TextMesh statusText;

    private XRSimpleInteractable interactable;
    private AudioSource audioSource;
    private Material runtimeMaterial;
    private Coroutine feedbackRoutine;
    private bool isAvailable;
    private bool isActivated;
    
    private Vector3 memoryPlaneTargetScale = Vector3.one;
private bool isActivating;

    public event Action<MemoryPillar> ActivationRequested;

    public int OrderIndex => orderIndex;
    public string MemoryName => memoryName;
    public bool IsActivated => isActivated;
    public bool IsAvailable => isAvailable;

    public void Configure(int index, string title, Color color, Renderer targetRenderer, GameObject plane, ParticleSystem particles, TextMesh status)
    {
        orderIndex = index;
        memoryName = title;
        activeColor = color;
        pillarRenderer = targetRenderer;
        memoryPlane = plane;
        feedbackParticles = particles;
        statusText = status;
    }

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        audioSource = GetComponent<AudioSource>();
        if (pillarRenderer == null) pillarRenderer = GetComponentInChildren<Renderer>();

        if (pillarRenderer != null && pillarRenderer.sharedMaterial != null)
        {
            runtimeMaterial = new Material(pillarRenderer.sharedMaterial);
            runtimeMaterial.name = pillarRenderer.sharedMaterial.name + " (" + memoryName + " Runtime)";
            runtimeMaterial.EnableKeyword("_EMISSION");
            pillarRenderer.material = runtimeMaterial;
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.75f;
        audioSource.minDistance = 0.6f;
        audioSource.maxDistance = 8f;
        audioSource.volume = 0.38f;
        if (audioSource.clip == null) audioSource.clip = CreateMemoryTone();

        if (memoryPlane != null)
        {
            memoryPlane.SetActive(false);
            memoryPlane.transform.localScale = Vector3.zero;
        }

        ApplyVisual(0.05f);
    }

    private void OnEnable()
    {
        if (interactable == null) interactable = GetComponent<XRSimpleInteractable>();
        interactable.selectEntered.AddListener(OnSelectEntered);
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
    }

    private void OnDisable()
    {
        if (interactable == null) return;
        interactable.selectEntered.RemoveListener(OnSelectEntered);
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.hoverExited.RemoveListener(OnHoverExited);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (isActivated || isActivating) return;
        ActivationRequested?.Invoke(this);
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (!isActivated && !isActivating) ApplyVisual(isAvailable ? 0.75f : 0.16f);
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        if (!isActivated && !isActivating) ApplyVisual(isAvailable ? 0.28f : 0.05f);
    }

    public void SetAvailable(bool available)
    {
        isAvailable = available && !isActivated;
        if (statusText != null)
        {
            statusText.text = isActivated ? "AWAKENED" : isAvailable ? "SELECT" : "LOCKED";
            statusText.color = isActivated ? activeColor : isAvailable ? new Color(1f, 0.78f, 0.3f) : new Color(0.55f, 0.58f, 0.62f);
        }
        if (!isActivated && !isActivating) ApplyVisual(isAvailable ? 0.28f : 0.05f);
    }

    public void Activate(Action completed)
    {
        if (isActivated || isActivating)
        {
            completed?.Invoke();
            return;
        }

        if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
        feedbackRoutine = StartCoroutine(ActivationRoutine(completed));
    }

    public void PulseLocked()
    {
        if (isActivated || isActivating) return;
        if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
        feedbackRoutine = StartCoroutine(LockedRoutine());
    }

    public void ResetMemory()
    {
        StopAllCoroutines();
        feedbackRoutine = null;
        isActivated = false;
        isActivating = false;
        isAvailable = false;
        if (audioSource != null) audioSource.Stop();
        if (feedbackParticles != null)
        {
            feedbackParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        if (memoryPlane != null)
        {
            memoryPlaneTargetScale = memoryPlane.transform.localScale;
            memoryPlane.SetActive(false);
            memoryPlane.transform.localScale = Vector3.zero;
        }
        SetAvailable(false);
    }

    public void DebugRequestActivation()
    {
        if (!isActivated && !isActivating) ActivationRequested?.Invoke(this);
    }

    private IEnumerator ActivationRoutine(Action completed)
    {
        isActivating = true;
        isAvailable = false;
        ApplyVisual(2.4f);

        if (statusText != null)
        {
            statusText.text = "AWAKENING";
            statusText.color = activeColor;
        }
        if (audioSource != null && audioSource.clip != null) audioSource.Play();
        if (feedbackParticles != null) feedbackParticles.Play(true);

        if (memoryPlane != null)
        {
            memoryPlane.SetActive(true);
            Vector3 targetScale = memoryPlaneTargetScale;
            float elapsed = 0f;
            while (elapsed < 0.65f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / 0.65f);
                memoryPlane.transform.localScale = Vector3.LerpUnclamped(Vector3.zero, targetScale, t);
                yield return null;
            }
            memoryPlane.transform.localScale = targetScale;
        }

        yield return new WaitForSeconds(0.45f);
        isActivated = true;
        isActivating = false;
        if (statusText != null)
        {
            statusText.text = "AWAKENED";
            statusText.color = activeColor;
        }
        ApplyVisual(1.65f);
        feedbackRoutine = null;
        completed?.Invoke();
    }

    private IEnumerator LockedRoutine()
    {
        Color blocked = new Color(0.25f, 0.35f, 0.55f, 1f);
        if (statusText != null)
        {
            statusText.text = "FOLLOW THE SCENT";
            statusText.color = blocked;
        }

        SetEmission(blocked * 0.55f);
        yield return new WaitForSeconds(0.3f);
        ApplyVisual(isAvailable ? 0.28f : 0.05f);
        if (statusText != null)
        {
            statusText.text = isAvailable ? "SELECT" : "LOCKED";
            statusText.color = isAvailable ? new Color(1f, 0.78f, 0.3f) : new Color(0.55f, 0.58f, 0.62f);
        }
        feedbackRoutine = null;
    }

    private void ApplyVisual(float intensity)
    {
        SetEmission(activeColor * intensity);
    }

    private void SetEmission(Color color)
    {
        if (runtimeMaterial == null) return;
        if (runtimeMaterial.HasProperty("_EmissionColor")) runtimeMaterial.SetColor("_EmissionColor", color);
        if (runtimeMaterial.HasProperty("_BaseColor") && isActivated)
        {
            runtimeMaterial.SetColor("_BaseColor", Color.Lerp(new Color(0.34f, 0.075f, 0.055f), activeColor, 0.38f));
        }
    }

    private AudioClip CreateMemoryTone()
    {
        const int sampleRate = 44100;
        const float duration = 1.05f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        float baseFrequency = orderIndex == 0 ? 392f : orderIndex == 1 ? 523.25f : 659.25f;

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float attack = Mathf.Clamp01(time / 0.08f);
            float release = Mathf.Clamp01((duration - time) / 0.35f);
            float envelope = attack * release;
            float fundamental = Mathf.Sin(2f * Mathf.PI * baseFrequency * time);
            float harmonic = Mathf.Sin(2f * Mathf.PI * baseFrequency * 2.01f * time) * 0.28f;
            samples[i] = (fundamental + harmonic) * envelope * 0.22f;
        }

        AudioClip clip = AudioClip.Create("Memory Tone - " + memoryName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
