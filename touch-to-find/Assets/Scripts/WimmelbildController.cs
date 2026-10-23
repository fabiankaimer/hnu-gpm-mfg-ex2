
using UnityEngine;
using UnityEngine.InputSystem;

public class WimmelbildController : MonoBehaviour
{
    [Header("Kamera")]
    [SerializeField] private Camera mainCamera;

    [Header("Zoom")]
    [SerializeField] private float minZoom = 1.5f;
    [SerializeField] private float maxZoom = 5.12f;
    [SerializeField] private float zoomSpeed = 0.5f;
    [SerializeField] private float pinchZoomSpeed = 1.0f;

	[Header("Suchziel – Leuchtturm oben rechts")]
	[SerializeField] private Vector2 targetMin = new Vector2(1310f, 0f);
	[SerializeField] private Vector2 targetMax = new Vector2(1380f, 145f);

    private SpriteRenderer imageRenderer;

    private Vector2 lastPointerPosition;
    private Vector2 pointerStartPosition;
    private Vector3 cameraStartPosition;

    private float lastPinchDistance;
    private bool isDragging;
    private bool pointerMoved;

    private const float DragThreshold = 10f;

    private void Awake()
    {
        imageRenderer = GetComponent<SpriteRenderer>();

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera != null && !mainCamera.orthographic)
        {
            Debug.LogWarning(
                "WimmelbildController benötigt eine orthografische Kamera."
            );
        }
    }

    private void Update()
    {
        if (mainCamera == null || imageRenderer == null)
        {
            return;
        }

        HandleMouseInput();
        HandleTouchInput();
        ClampCameraToImage();
    }

    private void HandleMouseInput()
    {
        Mouse mouse = Mouse.current;

        if (mouse == null)
        {
            return;
        }

        Vector2 mousePosition = mouse.position.ReadValue();

        // Zoomen mit dem Mausrad
        float scroll = mouse.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            Debug.Log("Mausrad erkannt: " + scroll);
        }
        
        if (Mathf.Abs(scroll) > 0.01f)
        {
            ZoomAtPosition(
                mousePosition,
                -scroll * zoomSpeed * 0.01f
            );
        }

        // Linke Maustaste gedrückt
        if (mouse.leftButton.wasPressedThisFrame)
        {
            BeginPointerInteraction(mousePosition);
        }

        // Ziehen mit der Maus
        if (mouse.leftButton.isPressed && isDragging)
        {
            UpdatePointerInteraction(mousePosition);
        }

        // Linke Maustaste losgelassen
        if (mouse.leftButton.wasReleasedThisFrame && isDragging)
        {
            EndPointerInteraction(mousePosition);
        }
    }

    private void HandleTouchInput()
    {
        if (Touchscreen.current == null)
        {
            return;
        }

        var touches = Touchscreen.current.touches;

        int activeTouchCount = 0;
        Vector2 firstTouchPosition = Vector2.zero;
        Vector2 secondTouchPosition = Vector2.zero;

        foreach (var touch in touches)
        {
            if (!touch.press.isPressed)
            {
                continue;
            }

            Vector2 position = touch.position.ReadValue();

            if (activeTouchCount == 0)
            {
                firstTouchPosition = position;
            }
            else if (activeTouchCount == 1)
            {
                secondTouchPosition = position;
            }

            activeTouchCount++;
        }

        // Zwei Finger: Pinch-Zoom
        if (activeTouchCount >= 2)
        {
            isDragging = false;

            float currentDistance = Vector2.Distance(
                firstTouchPosition,
                secondTouchPosition
            );

            Vector2 pinchCenter =
                (firstTouchPosition + secondTouchPosition) * 0.5f;

            if (lastPinchDistance > 0f)
            {
                float delta = lastPinchDistance - currentDistance;

                ZoomAtPosition(
                    pinchCenter,
                    delta * pinchZoomSpeed * 0.01f
                );
            }

            lastPinchDistance = currentDistance;
            return;
        }

        lastPinchDistance = 0f;

        if (activeTouchCount == 0)
        {
            return;
        }

        var primaryTouch = Touchscreen.current.primaryTouch;
        Vector2 touchPosition = primaryTouch.position.ReadValue();

        if (primaryTouch.press.wasPressedThisFrame)
        {
            BeginPointerInteraction(touchPosition);
        }

        if (primaryTouch.press.isPressed && isDragging)
        {
            UpdatePointerInteraction(touchPosition);
        }

        if (primaryTouch.press.wasReleasedThisFrame && isDragging)
        {
            EndPointerInteraction(touchPosition);
        }
    }

    private void BeginPointerInteraction(Vector2 screenPosition)
    {
        isDragging = true;
        pointerMoved = false;

        lastPointerPosition = screenPosition;
        pointerStartPosition = screenPosition;
        cameraStartPosition = mainCamera.transform.position;
    }

    private void UpdatePointerInteraction(Vector2 screenPosition)
    {
        if (Vector2.Distance(pointerStartPosition, screenPosition)
            > DragThreshold)
        {
            pointerMoved = true;
        }

        Vector3 previousWorldPosition =
            mainCamera.ScreenToWorldPoint(
                new Vector3(
                    lastPointerPosition.x,
                    lastPointerPosition.y,
                    -mainCamera.transform.position.z
                )
            );

        Vector3 currentWorldPosition =
            mainCamera.ScreenToWorldPoint(
                new Vector3(
                    screenPosition.x,
                    screenPosition.y,
                    -mainCamera.transform.position.z
                )
            );

        Vector3 difference =
            previousWorldPosition - currentWorldPosition;

        mainCamera.transform.position += difference;

        lastPointerPosition = screenPosition;
    }

    private void EndPointerInteraction(Vector2 screenPosition)
    {
        if (!pointerMoved)
        {
        	Debug.Log("Touch erkannt!");
            CheckWaldo(screenPosition);
        }

        isDragging = false;
    }

    private void ZoomAtPosition(
        Vector2 screenPosition,
        float zoomDelta)
    {
        float oldSize = mainCamera.orthographicSize;

        Vector3 worldPositionBeforeZoom =
            mainCamera.ScreenToWorldPoint(
                new Vector3(
                    screenPosition.x,
                    screenPosition.y,
                    -mainCamera.transform.position.z
                )
            );

        float newSize = Mathf.Clamp(
            oldSize + zoomDelta,
            minZoom,
            maxZoom
        );

        mainCamera.orthographicSize = newSize;

        Vector3 worldPositionAfterZoom =
            mainCamera.ScreenToWorldPoint(
                new Vector3(
                    screenPosition.x,
                    screenPosition.y,
                    -mainCamera.transform.position.z
                )
            );

        mainCamera.transform.position +=
            worldPositionBeforeZoom - worldPositionAfterZoom;
    }

    private void ClampCameraToImage()
    {
        Bounds bounds = imageRenderer.bounds;

        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;

        Vector3 position = mainCamera.transform.position;

        float minX = bounds.min.x + halfWidth;
        float maxX = bounds.max.x - halfWidth;
        float minY = bounds.min.y + halfHeight;
        float maxY = bounds.max.y - halfHeight;

        // Wenn das Bild kleiner als der sichtbare Bereich ist,
        // die Kamera auf der jeweiligen Achse zentrieren.
        if (minX > maxX)
        {
            position.x = bounds.center.x;
        }
        else
        {
            position.x = Mathf.Clamp(position.x, minX, maxX);
        }

        if (minY > maxY)
        {
            position.y = bounds.center.y;
        }
        else
        {
            position.y = Mathf.Clamp(position.y, minY, maxY);
        }

        mainCamera.transform.position = position;
    }
    
    private void CheckWaldo(Vector2 screenPosition)
	{
		Vector3 worldPosition = mainCamera.ScreenToWorldPoint(
		    new Vector3(
		        screenPosition.x,
		        screenPosition.y,
		        -mainCamera.transform.position.z
		    )
		);

		Vector3 localPosition =
		    imageRenderer.transform.InverseTransformPoint(worldPosition);

		Bounds spriteBounds = imageRenderer.localBounds;

		float normalizedX =
		    (localPosition.x - spriteBounds.min.x) / spriteBounds.size.x;

		float normalizedY =
		    (localPosition.y - spriteBounds.min.y) / spriteBounds.size.y;

		// Unity hat den Ursprung unten links;
		// die Bildkoordinaten beginnen dagegen oben links.
		float imageX = normalizedX * imageRenderer.sprite.rect.width;
		float imageY = (1f - normalizedY) * imageRenderer.sprite.rect.height;

		bool found =
		    imageX >= targetMin.x &&
		    imageX <= targetMax.x &&
		    imageY >= targetMin.y &&
		    imageY <= targetMax.y;

		if (found)
		{
		    Debug.Log("Leuchtturm gefunden!");
		}
		else
		{
		    Debug.Log("Hier ist der Leuchtturm nicht.");
		}
	}

}
