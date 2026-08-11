using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class SceneTransitionPortal : MonoBehaviour
{
    [SerializeField] private string nextSceneName;
    [SerializeField, Min(0.25f)] private float triggerRadius = 1.5f;
    [SerializeField, Min(0f)] private float transitionDelay = 0.35f;
    [SerializeField] private TextMesh promptText;

    private Transform viewer;
    private bool isLoading;

    public string NextSceneName => nextSceneName;
    public bool IsLoading => isLoading;

    public void Configure(string sceneName, float radius, TextMesh prompt)
    {
        nextSceneName = sceneName;
        triggerRadius = radius;
        promptText = prompt;
    }

    private void OnEnable()
    {
        LocateViewer();
        isLoading = false;
        if (promptText != null) promptText.text = "FOLLOW THE SCENT";
    }

    private void Update()
    {
        if (isLoading || string.IsNullOrEmpty(nextSceneName)) return;
        if (viewer == null)
        {
            LocateViewer();
            return;
        }

        Vector3 offset = viewer.position - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude <= triggerRadius * triggerRadius)
        {
            StartCoroutine(LoadNextScene());
        }
    }

    public void DebugLoadNext()
    {
        if (!isLoading && !string.IsNullOrEmpty(nextSceneName)) StartCoroutine(LoadNextScene());
    }

    private void LocateViewer()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            viewer = mainCamera.transform;
            return;
        }

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
        if (cameras.Length > 0) viewer = cameras[0].transform;
    }

    private IEnumerator LoadNextScene()
    {
        isLoading = true;
        if (promptText != null) promptText.text = "ENTERING " + nextSceneName.ToUpperInvariant();
        yield return new WaitForSecondsRealtime(transitionDelay);

        AsyncOperation operation = SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Single);
        if (operation == null)
        {
            Debug.LogError("[Osmanthus] Could not load scene: " + nextSceneName);
            isLoading = false;
            yield break;
        }

        while (!operation.isDone) yield return null;
    }
}
