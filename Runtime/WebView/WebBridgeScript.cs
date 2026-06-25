using System;
using System.IO;
using UnityEngine;

namespace UnityWebUI.WebView
{
    public static class WebBridgeScript
    {
        public const string FileName = "unity-bridge.js";
        public const string RelativeFolder = "WebUI";

        public static string GetBridgeFilePath()
        {
            var projectPath = UnityWebUIPackagePaths.GetProjectStreamingAssetPath(Path.Combine(RelativeFolder, FileName));
            if (File.Exists(projectPath))
                return projectPath;

            var packagePath = UnityWebUIPackagePaths.GetBundledStreamingAssetPath(Path.Combine(RelativeFolder, FileName));
            if (!string.IsNullOrEmpty(packagePath) && File.Exists(packagePath))
                return packagePath;

            return projectPath;
        }

        public static string GetBridgeFileUrl()
        {
            var path = GetBridgeFilePath();
            if (!File.Exists(path))
                return null;
            return LocalPageUrl.ToFileUrl(path);
        }

        public static string GetBootstrapJavaScript()
        {
            var script = TryReadBridgeScriptText();
            if (string.IsNullOrEmpty(script))
                return "if(!window.Unity){window.Unity={call:function(m){window.location='unity:'+m;}};};";

            var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(script));
            return "if(typeof window.__unityWebUiEmitActionAtPoint!=='function'){" +
                   "try{eval(atob('" + b64 + "'));}catch(e){console.error(e);}" +
                   "}";
        }

        static string TryReadBridgeScriptText()
        {
            var path = GetBridgeFilePath();
            if (File.Exists(path))
                return File.ReadAllText(path);

            var resource = Resources.Load<TextAsset>(UnityWebUIPackagePaths.BridgeResourcePath);
            return resource != null ? resource.text : null;
        }

        public static void EnsureInstalled(IWebViewBackend backend)
        {
            if (backend == null)
                return;

            ExecuteJavaScript(backend, GetBootstrapJavaScript());
        }

        public static string GetActionAtPointScript(int x, int y)
        {
            return "(function(){" +
                   "if(window.__unityWebUiEmitActionAtPoint){window.__unityWebUiEmitActionAtPoint(" + x + "," + y + ");return;}" +
                   FallbackEmitAtPointJs(x, y) +
                   "})();";
        }

        public static void ExecuteJavaScript(IWebViewBackend backend, string script)
        {
            if (backend == null || string.IsNullOrEmpty(script))
                return;

            if (backend is IWebViewJavaScriptExecutor executor)
                executor.ExecuteJavaScript(script);
        }

        static string FallbackEmitAtPointJs(int x, int y)
        {
            return "function post(p){var j=JSON.stringify(p);if(window.chrome&&window.chrome.webview)window.chrome.webview.postMessage(j);}" +
                   "function stripTags(t){return(t||'').replace(/<[^>]+>/g,' ').replace(/\\s+/g,' ').trim();}" +
                   "function isCjk(c){var n=c.charCodeAt(0);return n>=0x4e00&&n<=0x9fff;}" +
                   "function slugFromText(t){t=stripTags(t);if(!t)return '';var s='',sep=false;for(var i=0;i<t.length;i++){" +
                   "var ch=t.charAt(i),code=t.charCodeAt(i),w=(code>=48&&code<=57)||(code>=65&&code<=90)||(code>=97&&code<=122)||isCjk(ch);" +
                   "if(w){s+=ch;sep=false;}else if(!sep&&s.length){s+='-';sep=true;}}s=s.replace(/^-+|-+$/g,'');if(s.length>32)s=s.substring(0,32).replace(/-+$/g,'');return s;}" +
                   "function resolveButtonActionIds(buttons){var slugCounts={},ids=new Array(buttons.length);for(var i=0;i<buttons.length;i++){" +
                   "var btn=buttons[i],explicit=btn.getAttribute('data-unity-action');if(explicit){ids[i]=explicit.trim();continue;}" +
                   "if(btn.id){ids[i]=btn.id.trim();continue;}var slug=slugFromText(btn.textContent||'');" +
                   "if(slug){if(!slugCounts[slug]){slugCounts[slug]=1;ids[i]=slug;}else{slugCounts[slug]++;ids[i]=slug+'-'+slugCounts[slug];}continue;}" +
                   "ids[i]='button-'+i;}return ids;}" +
                   "function resolveButtonActionId(btn){var buttons=Array.prototype.slice.call(document.querySelectorAll('button'));" +
                   "var index=buttons.indexOf(btn);if(index<0)return 'button';return resolveButtonActionIds(buttons)[index];}" +
                   "var e=document.elementFromPoint(" + x + "," + y + ");" +
                   "if(!e)return;" +
                   "e.dispatchEvent(new MouseEvent('click',{bubbles:true,cancelable:true,view:window,clientX:" + x + ",clientY:" + y + ",button:0}));" +
                   "var actionEl=e.closest?e.closest('[data-unity-action]'):null;" +
                   "if(actionEl){post({type:'action',id:actionEl.getAttribute('data-unity-action')});return;}" +
                   "var btn=e.closest?e.closest('button'):null;" +
                   "if(btn){post({type:'action',id:resolveButtonActionId(btn)});return;}";
        }
    }
}
