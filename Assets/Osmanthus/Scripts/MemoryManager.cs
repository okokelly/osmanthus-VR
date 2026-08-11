using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class MemoryManager : MonoBehaviour
{
    [SerializeField] private List<MemoryPillar> pillars = new List<MemoryPillar>();
    [SerializeField] private GameObject exitGate;
    [SerializeField] private GameObject exitPortal;
    [SerializeField] private ParticleSystem completionParticles;
    [SerializeField] private TextMesh instructionText;

    private int nextMemoryIndex;
    private bool activationInProgress;
    private bool allMemoriesActivated;

    public int ActivatedCount => nextMemoryIndex;
    public int TotalCount => pillars.Count;
    public bool AllMemoriesActivated => allMemoriesActivated;

    public void Configure(MemoryPillar[] orderedPillars, GameObject gate, GameObject portal, ParticleSystem completion, TextMesh instructions)
    {
        pillars.Clear();
        if (orderedPillars != null) pillars.AddRange(orderedPillars);
        pillars.Sort((a, b) => a.OrderIndex.CompareTo(b.OrderIndex));
        exitGate = gate;
        exitPortal = portal;
        completionParticles = completion;
        instructionText = instructions;
    }

    private void Awake()
    {
        if (pillars.Count == 0)
        {
            MemoryPillar[] found = Object.FindObjectsByType<MemoryPillar>(FindObjectsInactive.Include);
            pillars.AddRange(found);
        }
        pillars.RemoveAll(pillar => pillar == null);
        pillars.Sort((a, b) => a.OrderIndex.CompareTo(b.OrderIndex));
    }

    private void OnEnable()
    {
        foreach (MemoryPillar pillar in pillars)
        {
            if (pillar != null) pillar.ActivationRequested += HandleActivationRequested;
        }
    }

    private void Start()
    {
        ResetMemories();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            ResetMemories();
        }
    }

    private void OnDisable()
    {
        foreach (MemoryPillar pillar in pillars)
        {
            if (pillar != null) pillar.ActivationRequested -= HandleActivationRequested;
        }
    }

    public void ResetMemories()
    {
        activationInProgress = false;
        allMemoriesActivated = false;
        nextMemoryIndex = 0;

        foreach (MemoryPillar pillar in pillars)
        {
            if (pillar != null) pillar.ResetMemory();
        }

        if (exitGate != null) exitGate.SetActive(true);
        if (exitPortal != null) exitPortal.SetActive(false);
        if (completionParticles != null)
        {
            completionParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        UpdateAvailability();
        if (instructionText != null)
        {
            instructionText.text = pillars.Count == 0
                ? "NO MEMORY PILLARS FOUND"
                : "SELECT PLACE TO BEGIN";
            instructionText.color = new Color(1f, 0.78f, 0.3f);
        }

        Debug.Log("[Osmanthus] Memory sequence reset.");
    }

    public void DebugActivateNext()
    {
        if (allMemoriesActivated || activationInProgress || nextMemoryIndex >= pillars.Count) return;
        HandleActivationRequested(pillars[nextMemoryIndex]);
    }

    public string GetDebugState()
    {
        return "Activated=" + nextMemoryIndex + "/" + pillars.Count
            + "; Busy=" + activationInProgress
            + "; Complete=" + allMemoriesActivated
            + "; ExitPortalActive=" + (exitPortal != null && exitPortal.activeSelf)
            + "; ExitGateActive=" + (exitGate != null && exitGate.activeSelf);
    }

    private void HandleActivationRequested(MemoryPillar pillar)
    {
        if (pillar == null || allMemoriesActivated || pillar.IsActivated) return;

        if (activationInProgress || nextMemoryIndex >= pillars.Count || pillar != pillars[nextMemoryIndex])
        {
            pillar.PulseLocked();
            return;
        }

        activationInProgress = true;
        if (instructionText != null)
        {
            instructionText.text = "AWAKENING " + pillar.MemoryName.ToUpperInvariant();
            instructionText.color = new Color(1f, 0.62f, 0.18f);
        }

        pillar.Activate(() => CompleteActivation(pillar));
    }

    private void CompleteActivation(MemoryPillar pillar)
    {
        if (nextMemoryIndex >= pillars.Count || pillar != pillars[nextMemoryIndex])
        {
            activationInProgress = false;
            return;
        }

        nextMemoryIndex++;
        activationInProgress = false;

        if (nextMemoryIndex >= pillars.Count)
        {
            UnlockLakePath();
            return;
        }

        UpdateAvailability();
        if (instructionText != null)
        {
            instructionText.text = "FOLLOW THE SCENT TO " + pillars[nextMemoryIndex].MemoryName.ToUpperInvariant();
            instructionText.color = new Color(1f, 0.78f, 0.3f);
        }

        Debug.Log("[Osmanthus] Memory activated: " + pillar.MemoryName + " (" + nextMemoryIndex + "/" + pillars.Count + ")");
    }

    private void UpdateAvailability()
    {
        for (int i = 0; i < pillars.Count; i++)
        {
            pillars[i].SetAvailable(!allMemoriesActivated && !activationInProgress && i == nextMemoryIndex);
        }
    }

    private void UnlockLakePath()
    {
        allMemoriesActivated = true;
        if (exitGate != null) exitGate.SetActive(false);
        if (exitPortal != null) exitPortal.SetActive(true);
        if (completionParticles != null)
        {
            completionParticles.gameObject.SetActive(true);
            completionParticles.Play(true);
        }

        UpdateAvailability();
        if (instructionText != null)
        {
            instructionText.text = "THE PATH TO THE LAKE IS OPEN";
            instructionText.color = new Color(1f, 0.68f, 0.2f);
        }

        Debug.Log("[Osmanthus] All memories activated. Scene 3 path unlocked.");
    }
}
