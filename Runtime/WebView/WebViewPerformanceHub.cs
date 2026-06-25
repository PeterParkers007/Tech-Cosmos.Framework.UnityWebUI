using System.Collections.Generic;

namespace UnityWebUI.WebView
{
    /// <summary>
    /// Coordinates all active WebView pump targets: message/update every frame,
    /// GPU/bitmap capture staggered across multiple instances.
    /// </summary>
    public static class WebViewPerformanceHub
    {
        static readonly List<IWebViewPumpTarget> _entries = new List<IWebViewPumpTarget>(4);
        static int _lastPumpFrame = -1;

        public static int ActiveCount => _entries.Count;

        public static void Register(IWebViewPumpTarget target)
        {
            if (target == null)
                return;

            Unregister(target);

            var priority = target.PumpPriority;
            var insertAt = _entries.Count;
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].PumpPriority > priority)
                {
                    insertAt = i;
                    break;
                }
            }

            _entries.Insert(insertAt, target);
        }

        public static void Unregister(IWebViewPumpTarget target)
        {
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i] == target)
                    _entries.RemoveAt(i);
            }
        }

        public static void TickAll(int frame)
        {
            if (_entries.Count == 0)
                return;

            if (frame == _lastPumpFrame)
                return;

            _lastPumpFrame = frame;

            var count = _entries.Count;
            for (var i = 0; i < count; i++)
            {
                var entry = _entries[i];
                if (entry == null)
                    continue;

                var captureFrame = ShouldCaptureFrame(i, count, frame, entry.RefreshCycle);
                entry.Pump(captureFrame);
            }
        }

        public static void SyncAllDisplays()
        {
            var count = _entries.Count;
            for (var i = 0; i < count; i++)
            {
                var entry = _entries[i];
                entry?.SyncDisplay();
            }
        }

        static bool ShouldCaptureFrame(int index, int totalCount, int frame, int refreshCycle)
        {
            var cycle = refreshCycle < 1 ? 1 : refreshCycle;
            if (totalCount <= 1)
                return frame % cycle == 0;

            return (frame + index) % cycle == 0;
        }
    }
}
