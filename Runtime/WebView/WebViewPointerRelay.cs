using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UnityWebUI.WebView
{
    /// <summary>
    /// Forwards UI pointer events on a RawImage to an off-screen WebViewHost.
    /// Attach to the same GameObject as the RawImage (or assign Display).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.UI.RawImage))]
    public sealed class WebViewPointerRelay : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IPointerMoveHandler, IPointerExitHandler, IScrollHandler
    {
        const float MinPressVisualSec = 0.06f;

        WebViewHost _host;
        RectTransform _area;
        Vector2 _lastPoint = new Vector2(float.NaN, float.NaN);
        bool _pointerInside;
        bool _pressActive;
        Vector2 _pressPoint;
        float _pressTime;
        Coroutine _pendingRelease;

        public void Bind(WebViewHost host, RectTransform area)
        {
            _host = host;
            _area = area != null ? area : transform as RectTransform;
            var raw = GetComponent<UnityEngine.UI.RawImage>();
            if (raw != null)
                raw.raycastTarget = true;
        }

        void Update()
        {
            if (_host == null || _area == null)
                return;

            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.IsPointerOverGameObject())
            {
                var data = new PointerEventData(eventSystem) { position = Input.mousePosition };
                var results = new System.Collections.Generic.List<RaycastResult>();
                eventSystem.RaycastAll(data, results);
                var overSelf = false;
                for (var i = 0; i < results.Count; i++)
                {
                    if (results[i].gameObject == gameObject)
                    {
                        overSelf = true;
                        break;
                    }
                }

                if (!overSelf)
                {
                    if (_pointerInside)
                    {
                        _pointerInside = false;
                        _lastPoint = new Vector2(float.NaN, float.NaN);
                        _host.ForwardPointerLeave();
                    }
                    return;
                }
            }

            if (!TryGetWebViewPointFromScreen(Input.mousePosition, out var point))
            {
                if (_pointerInside)
                {
                    _pointerInside = false;
                    _lastPoint = new Vector2(float.NaN, float.NaN);
                    _host.ForwardPointerLeave();
                }
                return;
            }

            _pointerInside = true;
            if (point == _lastPoint)
                return;

            _lastPoint = point;
            _host.ForwardPointerMove(point);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_host == null || !TryGetWebViewPoint(eventData, out var point))
                return;

            CancelPendingRelease();
            _pressActive = true;
            _pressPoint = point;
            _pressTime = Time.unscaledTime;
            _host.ForwardPointerDown(point);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_pressActive || _host == null)
                return;

            if (!TryGetWebViewPoint(eventData, out var point))
                point = _pressPoint;

            SchedulePointerRelease(point);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Click is handled by PointerDown/PointerUp so CSS :active can render.
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (_host == null || !TryGetWebViewPoint(eventData, out var point))
                return;
            _lastPoint = point;
            _pointerInside = true;
            _host.ForwardPointerMove(point);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_pressActive)
                SchedulePointerRelease(_pressPoint);

            _pointerInside = false;
            _lastPoint = new Vector2(float.NaN, float.NaN);
            _host?.ForwardPointerLeave();
        }

        public void OnScroll(PointerEventData eventData)
        {
            _host?.ForwardScroll(eventData.scrollDelta * 40f);
        }

        void CancelPendingRelease()
        {
            if (_pendingRelease == null)
                return;

            StopCoroutine(_pendingRelease);
            _pendingRelease = null;
        }

        void SchedulePointerRelease(Vector2 point)
        {
            CancelPendingRelease();
            _pendingRelease = StartCoroutine(ReleaseAfterMinHold(point));
        }

        IEnumerator ReleaseAfterMinHold(Vector2 point)
        {
            var wait = MinPressVisualSec - (Time.unscaledTime - _pressTime);
            if (wait > 0f)
                yield return new WaitForSecondsRealtime(wait);

            _pendingRelease = null;
            if (!_pressActive || _host == null)
                yield break;

            _pressActive = false;
            _host.ForwardPointerUp(point);
        }

        bool TryGetWebViewPoint(PointerEventData eventData, out Vector2 point)
        {
            return TryGetWebViewPointFromScreen(eventData.position, out point);
        }

        bool TryGetWebViewPointFromScreen(Vector2 screenPosition, out Vector2 point)
        {
            point = default;
            if (_area == null)
                return false;

            var camera = _area.GetComponentInParent<Canvas>()?.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _area, screenPosition, camera, out var local))
                return false;

            var rect = _area.rect;
            var x = local.x - rect.xMin;
            var yFromTop = rect.yMax - local.y;
            var flip = _host?.Backend != null && _host.Backend.FlipVertically;
            var webY = flip ? yFromTop : rect.height - yFromTop;
            point = new Vector2(x, webY);
            return point.x >= 0f && point.y >= 0f && point.x <= rect.width && point.y <= rect.height;
        }
    }
}
