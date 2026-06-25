using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace UnityWebUI.WebView
{
    /// <summary>
    /// Optional Vuplex 3D WebView integration via reflection so the package compiles without the plugin installed.
    /// Install Vuplex and reload scripts — no code changes required.
    /// </summary>
    public sealed class VuplexWebViewBackend : IWebViewBackend, IWebViewJavaScriptExecutor
    {
        static readonly string[] AssemblyNames =
        {
            "Vuplex.WebView",
            "Vuplex.WebViewStandaloneWindows",
            "Vuplex.WebViewStandaloneMac",
        };

        readonly Transform _hostTransform;
        object _prefabInstance;
        object _webView;
        Texture _texture;
        bool _initialized;

        public bool IsAvailable { get; private set; }
        public string StatusMessage { get; private set; }
        public Texture Texture => _texture;
        public bool FlipVertically => false;

        public event Action<string> MessageEmitted;
        public event Action<string> UrlChanged;
        public event Action Initialized;

        public VuplexWebViewBackend(Transform hostTransform)
        {
            _hostTransform = hostTransform;
            TryCreate();
        }

        void TryCreate()
        {
            var prefabType = FindType("Vuplex.WebView.CanvasWebViewPrefab");
            if (prefabType == null)
            {
                StatusMessage = "Vuplex 3D WebView not found. Import the plugin from https://developer.vuplex.com/webview/overview";
                IsAvailable = false;
                return;
            }

            try
            {
                var instantiate = prefabType.GetMethod("Instantiate", BindingFlags.Public | BindingFlags.Static);
                if (instantiate == null)
                    throw new MissingMethodException(prefabType.FullName, "Instantiate");

                _prefabInstance = instantiate.Invoke(null, null);
                if (_prefabInstance is Component component)
                    component.transform.SetParent(_hostTransform, false);

                var webViewProp = prefabType.GetProperty("WebView");
                if (webViewProp == null)
                    throw new MissingMemberException("CanvasWebViewPrefab.WebView");

                var initMethod = prefabType.GetMethod("WaitUntilInitialized", BindingFlags.Public | BindingFlags.Instance);
                if (initMethod != null && _hostTransform != null)
                {
                    var runner = _hostTransform.GetComponent<WebViewCoroutineRunner>();
                    if (runner == null)
                        runner = _hostTransform.gameObject.AddComponent<WebViewCoroutineRunner>();
                    runner.StartCoroutine(WaitForInit(initMethod, webViewProp));
                }
                else
                {
                    FinishInit(webViewProp);
                }

                IsAvailable = true;
                StatusMessage = "Vuplex WebView ready.";
            }
            catch (Exception ex)
            {
                IsAvailable = false;
                StatusMessage = $"Vuplex init failed: {ex.Message}";
                Debug.LogWarning($"UnityWebUI: {StatusMessage}");
            }
        }

        IEnumerator WaitForInit(MethodInfo initMethod, PropertyInfo webViewProp)
        {
            var enumerator = initMethod.Invoke(_prefabInstance, null) as IEnumerator;
            if (enumerator != null)
            {
                while (enumerator.MoveNext())
                    yield return enumerator.Current;
            }

            FinishInit(webViewProp);
        }

        void FinishInit(PropertyInfo webViewProp)
        {
            _webView = webViewProp?.GetValue(_prefabInstance);
            if (_webView == null)
            {
                StatusMessage = "Vuplex WebView instance is null after initialization.";
                IsAvailable = false;
                return;
            }

            HookEvents(_webView);
            RefreshTexture();
            _initialized = true;
            Initialized?.Invoke();
        }

        void HookEvents(object webView)
        {
            BindStringEvent(webView, "MessageEmitted", value => MessageEmitted?.Invoke(value));
            BindStringEvent(webView, "UrlChanged", value => UrlChanged?.Invoke(value));
        }

        static void BindStringEvent(object target, string eventName, Action<string> callback)
        {
            var evt = target.GetType().GetEvent(eventName);
            if (evt == null)
                return;

            var eventArgType = evt.EventHandlerType.GetMethod("Invoke").GetParameters()[1].ParameterType;
            var binder = typeof(VuplexWebViewBackend).GetMethod(nameof(BindStringEventGeneric),
                BindingFlags.NonPublic | BindingFlags.Static);
            binder?.MakeGenericMethod(eventArgType).Invoke(null, new object[] { target, evt, callback });
        }

        static void BindStringEventGeneric<TEventArgs>(object target, EventInfo evt, Action<string> callback)
        {
            EventHandler<TEventArgs> handler = (_, args) =>
            {
                var value = typeof(TEventArgs).GetProperty("Value")?.GetValue(args) as string;
                if (!string.IsNullOrEmpty(value))
                    callback(value);
            };
            evt.AddEventHandler(target, handler);
        }

        public void Attach(Transform hostTransform)
        {
            if (_prefabInstance is Component component && hostTransform != null)
                component.transform.SetParent(hostTransform, false);
        }

        public void SetSize(int width, int height)
        {
            if (_webView == null || width <= 0 || height <= 0)
                return;

            var resize = _webView.GetType().GetMethod("Resize", new[] { typeof(int), typeof(int) });
            if (resize != null)
                resize.Invoke(_webView, new object[] { width, height });
            else
            {
                var sizeProp = _webView.GetType().GetProperty("Size");
                if (sizeProp != null && sizeProp.CanWrite && sizeProp.PropertyType == typeof(Vector2))
                    sizeProp.SetValue(_webView, new Vector2(width, height));
            }

            if (_prefabInstance is Component component)
            {
                var rect = component.transform as RectTransform;
                if (rect != null)
                    rect.sizeDelta = new Vector2(width, height);
            }

            RefreshTexture();
        }

        public void LoadUrl(string url)
        {
            if (_webView == null)
                return;

            var method = _webView.GetType().GetMethod("LoadUrl", new[] { typeof(string) });
            method?.Invoke(_webView, new object[] { url });
        }

        public void PostMessage(string message)
        {
            ExecuteJavaScript($"if(window.vuplex)window.vuplex.postMessage({JsonString(message)});");
        }

        public void Click(int x, int y)
        {
            if (_webView == null)
                return;

            var method = _webView.GetType().GetMethod("Click", new[] { typeof(int), typeof(int) });
            method?.Invoke(_webView, new object[] { x, y });
        }

        public void PointerDown(int x, int y) => Click(x, y);

        public void PointerUp(int x, int y) { }

        public void MovePointer(int x, int y) { }

        public void LeavePointer() { }

        public void Scroll(int deltaX, int deltaY)
        {
            if (_webView == null)
                return;

            var method = _webView.GetType().GetMethod("Scroll", new[] { typeof(int), typeof(int) })
                         ?? _webView.GetType().GetMethod("Scroll", new[] { typeof(float), typeof(float) });
            method?.Invoke(_webView, new object[] { deltaX, deltaY });
        }

        public void Tick()
        {
            if (!_initialized)
                return;
            RefreshTexture();
        }

        void RefreshTexture()
        {
            if (_webView == null)
                return;

            var texProp = _webView.GetType().GetProperty("Texture");
            _texture = texProp?.GetValue(_webView) as Texture;
        }

        public void ExecuteJavaScript(string script)
        {
            if (_webView == null)
                return;

            var method = _webView.GetType().GetMethod("ExecuteJavaScript", new[] { typeof(string) });
            method?.Invoke(_webView, new object[] { script });
        }

        static string JsonString(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "''";
            return "'" + raw.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
        }

        static Type FindType(string fullName)
        {
            foreach (var assemblyName in AssemblyNames)
            {
                var type = Type.GetType($"{fullName}, {assemblyName}");
                if (type != null)
                    return type;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(fullName, false);
                    if (type != null)
                        return type;
                }
                catch
                {
                    // ignored
                }
            }

            return null;
        }

        public void Dispose()
        {
            if (_prefabInstance is Component component && component != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(component.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(component.gameObject);
            }

            _prefabInstance = null;
            _webView = null;
            _texture = null;
            _initialized = false;
        }
    }

    sealed class WebViewCoroutineRunner : MonoBehaviour { }
}
