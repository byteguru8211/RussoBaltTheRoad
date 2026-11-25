using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(200)]
public class NetworkNameplateUI : MonoBehaviour
{
    public NetworkPlayerCar car;
    public bool showLocalPlayer;
    public Vector3 localPosition = new Vector3(0f, 2.35f, 0f);
    public float worldScale = 0.012f;

    private Canvas canvas;
    private Text label;
    private Camera trackedCamera;

    private void Awake()
    {
        if (car == null)
            car = GetComponent<NetworkPlayerCar>();
        Build();
        if (car != null)
            car.DisplayName.OnValueChanged += OnNameChanged;
    }

    private void OnDestroy()
    {
        if (car != null)
            car.DisplayName.OnValueChanged -= OnNameChanged;
    }

    private void Build()
    {
        GameObject canvasObject = new GameObject("NetworkNameplate", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = localPosition;
        canvasObject.transform.localScale = new Vector3(worldScale, worldScale, worldScale);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;
        RectTransform rect = canvasObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(640f, 150f);
        label = UIUtils.Text(canvas.transform, "Name", "Гонщик", 58, UITheme.Cream, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(18f, 10f), new Vector2(-18f, -10f), UITheme.TitleFont);
        label.supportRichText = false;
    }

    private void OnNameChanged(Unity.Collections.FixedString32Bytes previous, Unity.Collections.FixedString32Bytes current)
    {
        if (label != null)
            label.text = current.ToString();
    }

    private void LateUpdate()
    {
        if (canvas == null || label == null || car == null)
            return;
        bool visible = car.IsSpawned && (showLocalPlayer || !car.IsOwner);
        if (!visible)
        {
            canvas.enabled = false;
            return;
        }
        if (trackedCamera == null)
            trackedCamera = Camera.main;
        if (trackedCamera == null)
            return;
        Vector3 direction = trackedCamera.transform.position - canvas.transform.position;
        canvas.enabled = Vector3.Dot(trackedCamera.transform.forward, direction) > 0f;
        if (!canvas.enabled)
            return;
        canvas.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        label.text = car.DisplayName.Value.ToString();
        float distance = direction.magnitude;
        float scale = Mathf.Clamp(distance / 35f, 0.65f, 1.6f);
        canvas.transform.localScale = new Vector3(worldScale * scale, worldScale * scale, worldScale * scale);
    }
}
