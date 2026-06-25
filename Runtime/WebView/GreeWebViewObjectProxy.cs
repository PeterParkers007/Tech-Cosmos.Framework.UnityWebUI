using System;
using System.Reflection;
using UnityEngine;

namespace UnityWebUI.WebView
{
    /// <summary>
    /// Reflection wrapper for net.gree.unity-webview so UnityWebUI compiles without that package installed.
    /// </summary>
    sealed class GreeWebViewObjectProxy
    {
        const string WebViewObjectTypeName = "Gree.UnityWebView.WebViewObject, unity-webview";

        readonly Component _component;
        readonly Type _type;
        readonly FieldInfo _textureField;
        readonly FieldInfo _textureBufferField;
        readonly FieldInfo _webViewHandleField;
        readonly FieldInfo _rectField;
        readonly MethodInfo _setRectMethod;
        readonly MethodInfo _sendMouseEventMethod;
        readonly MethodInfo _getMessageMethod;
        readonly MethodInfo _pluginUpdateMethod;
        readonly MethodInfo _bitmapWidthMethod;
        readonly MethodInfo _bitmapHeightMethod;
        readonly MethodInfo _renderMethod;
        readonly PropertyInfo _devicePixelRatioProperty;
        readonly MethodInfo _isInitializedMethod;
        readonly MethodInfo _getVisibilityMethod;
        readonly MethodInfo _setVisibilityMethod;
        readonly MethodInfo _setMarginsMethod;
        readonly MethodInfo _loadUrlMethod;
        readonly MethodInfo _evaluateJsMethod;
        readonly MethodInfo _setUrlPatternMethod;
        readonly MethodInfo _callFromJsMethod;
        readonly MethodInfo _callOnErrorMethod;
        readonly MethodInfo _callOnHttpErrorMethod;
        readonly MethodInfo _callOnLoadedMethod;
        readonly MethodInfo _callOnStartedMethod;
        readonly MethodInfo _callOnHookedMethod;
        readonly MethodInfo _callOnCookiesMethod;

        public Component Component => _component;
        public GameObject GameObject => _component != null ? _component.gameObject : null;

        GreeWebViewObjectProxy(
            Component component,
            Type type,
            FieldInfo textureField,
            FieldInfo textureBufferField,
            FieldInfo webViewHandleField,
            FieldInfo rectField,
            MethodInfo setRectMethod,
            MethodInfo sendMouseEventMethod,
            MethodInfo getMessageMethod,
            MethodInfo pluginUpdateMethod,
            MethodInfo bitmapWidthMethod,
            MethodInfo bitmapHeightMethod,
            MethodInfo renderMethod,
            PropertyInfo devicePixelRatioProperty,
            MethodInfo isInitializedMethod,
            MethodInfo getVisibilityMethod,
            MethodInfo setVisibilityMethod,
            MethodInfo setMarginsMethod,
            MethodInfo loadUrlMethod,
            MethodInfo evaluateJsMethod,
            MethodInfo setUrlPatternMethod,
            MethodInfo callFromJsMethod,
            MethodInfo callOnErrorMethod,
            MethodInfo callOnHttpErrorMethod,
            MethodInfo callOnLoadedMethod,
            MethodInfo callOnStartedMethod,
            MethodInfo callOnHookedMethod,
            MethodInfo callOnCookiesMethod)
        {
            _component = component;
            _type = type;
            _textureField = textureField;
            _textureBufferField = textureBufferField;
            _webViewHandleField = webViewHandleField;
            _rectField = rectField;
            _setRectMethod = setRectMethod;
            _sendMouseEventMethod = sendMouseEventMethod;
            _getMessageMethod = getMessageMethod;
            _pluginUpdateMethod = pluginUpdateMethod;
            _bitmapWidthMethod = bitmapWidthMethod;
            _bitmapHeightMethod = bitmapHeightMethod;
            _renderMethod = renderMethod;
            _devicePixelRatioProperty = devicePixelRatioProperty;
            _isInitializedMethod = isInitializedMethod;
            _getVisibilityMethod = getVisibilityMethod;
            _setVisibilityMethod = setVisibilityMethod;
            _setMarginsMethod = setMarginsMethod;
            _loadUrlMethod = loadUrlMethod;
            _evaluateJsMethod = evaluateJsMethod;
            _setUrlPatternMethod = setUrlPatternMethod;
            _callFromJsMethod = callFromJsMethod;
            _callOnErrorMethod = callOnErrorMethod;
            _callOnHttpErrorMethod = callOnHttpErrorMethod;
            _callOnLoadedMethod = callOnLoadedMethod;
            _callOnStartedMethod = callOnStartedMethod;
            _callOnHookedMethod = callOnHookedMethod;
            _callOnCookiesMethod = callOnCookiesMethod;
        }

        public static bool IsPackagePresent => ResolveWebViewType() != null;

        public static GreeWebViewObjectProxy TryCreate(Transform parent, Action<string> onMessage, out string error)
        {
            error = null;
            var webViewType = ResolveWebViewType();
            if (webViewType == null)
            {
                error = "net.gree.unity-webview is not installed (optional fallback).";
                return null;
            }

            try
            {
                var go = new GameObject("UnityWebUI.GreeWebView");
                if (parent != null)
                    go.transform.SetParent(parent, false);

                var component = go.AddComponent(webViewType);
                var proxy = new GreeWebViewObjectProxy(
                    component,
                    webViewType,
                    webViewType.GetField("texture", BindingFlags.Instance | BindingFlags.NonPublic),
                    webViewType.GetField("textureDataBuffer", BindingFlags.Instance | BindingFlags.NonPublic),
                    webViewType.GetField("webView", BindingFlags.Instance | BindingFlags.NonPublic),
                    webViewType.GetField("rect", BindingFlags.Instance | BindingFlags.NonPublic),
                    webViewType.GetMethod("_CWebViewPlugin_SetRect", BindingFlags.Static | BindingFlags.NonPublic),
                    webViewType.GetMethod("_CWebViewPlugin_SendMouseEvent", BindingFlags.Static | BindingFlags.NonPublic),
                    webViewType.GetMethod("_CWebViewPlugin_GetMessage", BindingFlags.Static | BindingFlags.NonPublic),
                    webViewType.GetMethod("_CWebViewPlugin_Update", BindingFlags.Static | BindingFlags.NonPublic),
                    webViewType.GetMethod("_CWebViewPlugin_BitmapWidth", BindingFlags.Static | BindingFlags.NonPublic),
                    webViewType.GetMethod("_CWebViewPlugin_BitmapHeight", BindingFlags.Static | BindingFlags.NonPublic),
                    webViewType.GetMethod("_CWebViewPlugin_Render", BindingFlags.Static | BindingFlags.NonPublic),
                    webViewType.GetProperty("devicePixelRatio", BindingFlags.Instance | BindingFlags.Public),
                    webViewType.GetMethod("IsInitialized", BindingFlags.Instance | BindingFlags.Public),
                    webViewType.GetMethod("GetVisibility", BindingFlags.Instance | BindingFlags.Public),
                    webViewType.GetMethod("SetVisibility", BindingFlags.Instance | BindingFlags.Public),
                    webViewType.GetMethod("SetMargins", BindingFlags.Instance | BindingFlags.Public),
                    webViewType.GetMethod("LoadURL", BindingFlags.Instance | BindingFlags.Public),
                    webViewType.GetMethod("EvaluateJS", BindingFlags.Instance | BindingFlags.Public),
                    webViewType.GetMethod("SetURLPattern", BindingFlags.Instance | BindingFlags.Public),
                    webViewType.GetMethod("CallFromJS", BindingFlags.Instance | BindingFlags.NonPublic),
                    webViewType.GetMethod("CallOnError", BindingFlags.Instance | BindingFlags.NonPublic),
                    webViewType.GetMethod("CallOnHttpError", BindingFlags.Instance | BindingFlags.NonPublic),
                    webViewType.GetMethod("CallOnLoaded", BindingFlags.Instance | BindingFlags.NonPublic),
                    webViewType.GetMethod("CallOnStarted", BindingFlags.Instance | BindingFlags.NonPublic),
                    webViewType.GetMethod("CallOnHooked", BindingFlags.Instance | BindingFlags.NonPublic),
                    webViewType.GetMethod("CallOnCookies", BindingFlags.Instance | BindingFlags.NonPublic));

                if (!proxy.TryInit(onMessage, out var initError))
                {
                    error = initError;
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(go);
                    else
                        UnityEngine.Object.DestroyImmediate(go);
                    return null;
                }

                return proxy;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }

        static Type ResolveWebViewType()
        {
            var type = Type.GetType(WebViewObjectTypeName);
            if (type != null)
                return type;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType("Gree.UnityWebView.WebViewObject");
                if (type != null)
                    return type;
            }

            return null;
        }

        bool TryInit(Action<string> onMessage, out string error)
        {
            error = null;
            var initMethod = FindInitMethod(_type);
            if (initMethod == null)
            {
                error = "Could not find WebViewObject.Init.";
                return false;
            }

            SetMemberValue(_component, "bitmapRefreshCycle", 1);
            if (_devicePixelRatioProperty != null && _devicePixelRatioProperty.CanWrite)
                _devicePixelRatioProperty.SetValue(_component, 1f);

            Action<string> hooked = message => onMessage?.Invoke(message);
            Action<string> loaded = _ =>
            {
                _setUrlPatternMethod?.Invoke(_component, new object[] { string.Empty, string.Empty, "^unity:" });
                _evaluateJsMethod?.Invoke(_component, new object[] { "if(!window.Unity){window.Unity={call:function(m){window.location='unity:'+m;}};}" });
                onMessage?.Invoke(null);
            };

            var args = BuildInitArguments(initMethod, onMessage, hooked, loaded);
            initMethod.Invoke(_component, args);

            _setVisibilityMethod?.Invoke(_component, new object[] { true });
            _component.enabled = false;
            return true;
        }

        static MethodInfo FindInitMethod(Type webViewType)
        {
            foreach (var method in webViewType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.Name != "Init")
                    continue;

                var parameters = method.GetParameters();
                if (parameters.Length > 0 && parameters[0].ParameterType == typeof(Action<string>))
                    return method;
            }

            return null;
        }

        static object[] BuildInitArguments(MethodInfo initMethod, Action<string> cb, Action<string> hooked, Action<string> loaded)
        {
            var parameters = initMethod.GetParameters();
            var args = new object[parameters.Length];
            Action<string> noop = _ => { };

            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                if (parameter.ParameterType == typeof(Action<string>))
                {
                    args[i] = parameter.Name?.ToLowerInvariant() switch
                    {
                        "cb" => cb,
                        "hooked" => hooked,
                        "ld" => loaded,
                        "err" or "httperr" => noop,
                        _ => cb
                    };
                    continue;
                }

                if (parameter.ParameterType == typeof(bool))
                {
                    args[i] = parameter.HasDefaultValue ? parameter.DefaultValue : false;
                    continue;
                }

                if (parameter.ParameterType == typeof(int))
                {
                    args[i] = parameter.HasDefaultValue ? parameter.DefaultValue : 0;
                    continue;
                }

                if (parameter.ParameterType == typeof(string))
                {
                    args[i] = parameter.HasDefaultValue ? parameter.DefaultValue : string.Empty;
                    continue;
                }

                args[i] = parameter.HasDefaultValue ? parameter.DefaultValue : null;
            }

            return args;
        }

        static void SetMemberValue(Component component, string memberName, object value)
        {
            var field = component.GetType().GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(component, value);
        }

        public bool IsInitialized()
        {
            return _isInitializedMethod != null && (bool)_isInitializedMethod.Invoke(_component, null);
        }

        public bool GetVisibility()
        {
            return _getVisibilityMethod != null && (bool)_getVisibilityMethod.Invoke(_component, null);
        }

        public float DevicePixelRatio =>
            _devicePixelRatioProperty != null ? Convert.ToSingle(_devicePixelRatioProperty.GetValue(_component)) : 1f;

        public FieldInfo TextureField => _textureField;
        public FieldInfo TextureBufferField => _textureBufferField;
        public FieldInfo WebViewHandleField => _webViewHandleField;
        public FieldInfo RectField => _rectField;
        public MethodInfo SetRectMethod => _setRectMethod;
        public MethodInfo SendMouseEventMethod => _sendMouseEventMethod;
        public MethodInfo GetMessageMethod => _getMessageMethod;
        public MethodInfo PluginUpdateMethod => _pluginUpdateMethod;
        public MethodInfo BitmapWidthMethod => _bitmapWidthMethod;
        public MethodInfo BitmapHeightMethod => _bitmapHeightMethod;
        public MethodInfo RenderMethod => _renderMethod;
        public MethodInfo CallFromJsMethod => _callFromJsMethod;
        public MethodInfo CallOnErrorMethod => _callOnErrorMethod;
        public MethodInfo CallOnHttpErrorMethod => _callOnHttpErrorMethod;
        public MethodInfo CallOnLoadedMethod => _callOnLoadedMethod;
        public MethodInfo CallOnStartedMethod => _callOnStartedMethod;
        public MethodInfo CallOnHookedMethod => _callOnHookedMethod;
        public MethodInfo CallOnCookiesMethod => _callOnCookiesMethod;

        public void SetParent(Transform parent)
        {
            if (_component != null && parent != null)
                _component.transform.SetParent(parent, false);
        }

        public void SetMargins(int left, int top, int right, int bottom, bool relative)
        {
            _setMarginsMethod?.Invoke(_component, new object[] { left, top, right, bottom, relative });
        }

        public void LoadUrl(string url)
        {
            _loadUrlMethod?.Invoke(_component, new object[] { url });
        }

        public void EvaluateJs(string script)
        {
            _evaluateJsMethod?.Invoke(_component, new object[] { script });
        }
    }
}
