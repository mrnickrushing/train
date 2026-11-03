// App Store screenshot: renders the game camera plus all overlay UI offscreen at an exact size (the
// Editor Game view cannot), with the world-anchored bubbles hidden. Run in Play mode via
//   unity command script eval --result-only "$(sed "s/__W__/2868/; s/__H__/1320/; s#__OUT__#/abs/out.png#" Tools/store-shot.cs)"
// iPhone 6.9": 2868x1320; iPad 13": 2752x2064.
int W = __W__, H = __H__;
var cam = Ironhorse.Game.CameraRig.Instance.Cam;
var rt = new UnityEngine.RenderTexture(W, H, 24, UnityEngine.RenderTextureFormat.ARGB32, UnityEngine.RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
var mk = UnityEngine.GameObject.Find("Markers"); if (mk != null) mk.SetActive(false);
var changed = new System.Collections.Generic.List<UnityEngine.Canvas>();
foreach (var c in UnityEngine.Object.FindObjectsByType<UnityEngine.Canvas>(UnityEngine.FindObjectsSortMode.None))
    if (c.isRootCanvas && c.renderMode == UnityEngine.RenderMode.ScreenSpaceOverlay && c.gameObject.activeInHierarchy) { c.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = cam.nearClipPlane + 0.2f; changed.Add(c); }
var prev = cam.targetTexture;
cam.targetTexture = rt;
UnityEngine.Canvas.ForceUpdateCanvases();
cam.Render();
UnityEngine.Canvas.ForceUpdateCanvases();
cam.Render();
var prevActive = UnityEngine.RenderTexture.active;
UnityEngine.RenderTexture.active = rt;
var tex = new UnityEngine.Texture2D(W, H, UnityEngine.TextureFormat.RGB24, false);
tex.ReadPixels(new UnityEngine.Rect(0, 0, W, H), 0, 0);
tex.Apply();
UnityEngine.RenderTexture.active = prevActive;
cam.targetTexture = prev;
foreach (var c in changed) { c.renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay; }
System.IO.File.WriteAllBytes("__OUT__", tex.EncodeToPNG());
rt.Release();
if (mk != null) mk.SetActive(true);
return W + "x" + H;
