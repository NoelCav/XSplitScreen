using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dodad.XSplitscreen.Components
{
	// (specter) Shrinks a full-window panel to fit its player's camera viewport, then nudges it back on screen.
	// Measured in screen pixels every frame, since popouts only get their real size once active and laid out.
	public class FitToViewport : MonoBehaviour
	{
		public float widthFraction = 1f;
		public float heightFraction = 1f;

		private const float MinScale = 0.25f;
		private const float EdgeMargin = 0.02f;

		private static readonly List<Graphic> graphics = new List<Graphic>();
		private static readonly Vector3[] corners = new Vector3[4];

		private RectTransform rectTransform;
		private Vector3 baseScale;
		private Canvas rootCanvas;

		private void Awake()
		{
			rectTransform = (RectTransform) transform;
			baseScale = rectTransform.localScale;
		}

		private void LateUpdate()
		{
			if (!SplitscreenUserManager.IsSplitscreenEnabled)
			{
				rectTransform.localScale = baseScale;
				return;
			}

			if (!rootCanvas)
				rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;

			if (!rootCanvas || baseScale.x <= 0f || rectTransform.localScale.x <= 0f)
				return;

			var cam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
			var viewport = cam ? cam.pixelRect : new Rect(0, 0, Screen.width, Screen.height);

			if (!TryGetScreenBounds(cam, out var bounds))
				return;

			float currentFit = rectTransform.localScale.x / baseScale.x;
			float width = bounds.width / currentFit;
			float height = bounds.height / currentFit;

			float fit = Mathf.Clamp(Mathf.Min(viewport.width * widthFraction / width, viewport.height * heightFraction / height), MinScale, 1f);

			if (!Mathf.Approximately(fit, currentFit))
			{
				Log.Diag($"FitToViewport '{name}': base scale {baseScale.x}, size {width:0}x{height:0}px, viewport {viewport}, fit {fit:0.00}");
				rectTransform.localScale = new Vector3(baseScale.x * fit, baseScale.y * fit, baseScale.z);
				return;
			}

			KeepOnScreen(cam, viewport, bounds);
		}

		// (specter) Child images can reach past the panel's own rect, and lobby panels are tilted in 3D,
		// so take the projected bounds of everything drawn.
		private bool TryGetScreenBounds(Camera cam, out Rect bounds)
		{
			Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;

			GetComponentsInChildren(false, graphics);

			foreach (var graphic in graphics)
			{
				if (!graphic.enabled || graphic.color.a <= 0f)
					continue;

				graphic.rectTransform.GetWorldCorners(corners);

				foreach (var corner in corners)
				{
					var point = RectTransformUtility.WorldToScreenPoint(cam, corner);
					min = Vector2.Min(min, point);
					max = Vector2.Max(max, point);
				}
			}

			graphics.Clear();
			bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);

			return bounds.width > 0f && bounds.height > 0f;
		}

		private void KeepOnScreen(Camera cam, Rect viewport, Rect bounds)
		{
			float marginX = viewport.width * EdgeMargin;
			float marginY = viewport.height * EdgeMargin;
			var shift = Vector2.zero;

			if (bounds.xMax > viewport.xMax - marginX)
				shift.x = viewport.xMax - marginX - bounds.xMax;
			if (bounds.xMin + shift.x < viewport.xMin + marginX)
				shift.x = viewport.xMin + marginX - bounds.xMin;
			if (bounds.yMax > viewport.yMax - marginY)
				shift.y = viewport.yMax - marginY - bounds.yMax;
			if (bounds.yMin + shift.y < viewport.yMin + marginY)
				shift.y = viewport.yMin + marginY - bounds.yMin;

			if (shift.sqrMagnitude < 1f)
				return;

			Log.Diag($"FitToViewport '{name}': shifting {shift} to stay inside {viewport}");

			if (!cam)
			{
				rectTransform.position += (Vector3) shift;
				return;
			}

			var screenPosition = cam.WorldToScreenPoint(rectTransform.position);
			rectTransform.position = cam.ScreenToWorldPoint(screenPosition + (Vector3) shift);
		}
	}
}
