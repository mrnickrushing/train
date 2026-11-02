using UnityEngine;
#if IRONHORSE_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace Ironhorse.Game
{
    /// <summary>
    /// Screen-space polish when running on URP: bloom (glowing windows and lamps at night), warm color
    /// grading, a soft vignette and a tilt-shift depth of field that follows the camera's zoom so the
    /// valley reads like a detailed model railway diorama. Does nothing on the built-in pipeline.
    /// </summary>
    public sealed class PostFx : MonoBehaviour
    {
#if IRONHORSE_URP
        DepthOfField _dof;
        ColorAdjustments _color;
        Bloom _bloom;

        void Start()
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)) { enabled = false; return; }

            var cam = CameraRig.Instance.Cam;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.Medium;
            cam.allowHDR = true;

            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;

            _bloom = profile.Add<Bloom>(true);
            _bloom.threshold.Override(1.05f);
            _bloom.intensity.Override(0.55f);
            _bloom.scatter.Override(0.68f);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            _color = profile.Add<ColorAdjustments>(true);
            _color.postExposure.Override(0f);
            _color.contrast.Override(22f);
            _color.saturation.Override(18f);

            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(7f);

            var lgg = profile.Add<LiftGammaGain>(true);
            lgg.lift.Override(new Vector4(1.0f, 0.98f, 0.96f, 0f));
            lgg.gain.Override(new Vector4(1.0f, 0.99f, 0.96f, 0.02f));

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.24f);
            vignette.smoothness.Override(0.45f);

            _dof = profile.Add<DepthOfField>(true);
            _dof.mode.Override(DepthOfFieldMode.Gaussian);
            _dof.highQualitySampling.Override(false);
            _dof.gaussianMaxRadius.Override(1.1f);
        }

        void LateUpdate()
        {
            var rig = CameraRig.Instance;
            if (_dof == null || rig == null) return;
            // Keep the focus point sharp; blur the distant horizon for the miniature look.
            float d = rig.Distance;
            _dof.gaussianStart.Override(d * 1.9f);
            _dof.gaussianEnd.Override(d * 3.2f + 60f);
            // Nights get a touch cooler and darker; bloom picks up the lamps.
            var dn = DayNightCycle.Instance;
            if (dn != null)
            {
                _color.postExposure.Override(Mathf.Lerp(0f, -0.2f, dn.Night));
                _bloom.intensity.Override(Mathf.Lerp(0.55f, 1.2f, dn.Night));
            }
        }
#endif
    }
}
