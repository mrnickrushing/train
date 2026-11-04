using UnityEngine;

namespace Ironhorse.Game
{
    public enum FxKind { Chimney, Steam, Exhaust, Dust, Spray, Sparkle }

    /// <summary>Particle systems configured entirely from code.</summary>
    public static class Fx
    {
        public static Vector3 Wind = new Vector3(0.9f, 0, 0.35f);

        public static ParticleSystem Create(FxKind kind, Transform parent, Vector3 localPos)
        {
            var go = new GameObject("FX " + kind);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.rotation = Quaternion.Euler(-90, 0, 0); // emit upward
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = ps.emission;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8;
            shape.radius = 0.2f;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

            Color c0, c1;
            switch (kind)
            {
                case FxKind.Steam:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
                    main.maxParticles = 160;
                    main.gravityModifier = -0.04f;
                    emission.rateOverTime = 6;
                    c0 = new Color(0.82f, 0.82f, 0.82f, 0.75f); c1 = new Color(0.55f, 0.55f, 0.58f, 0f);
                    size.size = new ParticleSystem.MinMaxCurve(1, Curve(1f, 4.5f));
                    break;
                case FxKind.Exhaust:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 3f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
                    main.maxParticles = 60;
                    emission.rateOverTime = 4;
                    c0 = new Color(0.3f, 0.3f, 0.32f, 0.35f); c1 = new Color(0.4f, 0.4f, 0.42f, 0);
                    size.size = new ParticleSystem.MinMaxCurve(1, Curve(1f, 3f));
                    break;
                case FxKind.Dust:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(1f, 2f);
                    main.maxParticles = 60;
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.scale = new Vector3(9, 9, 0.5f);
                    emission.rateOverTime = 8;
                    c0 = new Color(0.78f, 0.65f, 0.48f, 0.5f); c1 = new Color(0.78f, 0.65f, 0.48f, 0);
                    size.size = new ParticleSystem.MinMaxCurve(1, Curve(1f, 2.2f));
                    break;
                case FxKind.Spray:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 3.5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
                    main.gravityModifier = 0.9f;
                    main.maxParticles = 120;
                    shape.angle = 25;
                    shape.radius = 0.05f;
                    emission.rateOverTime = 50;
                    c0 = new Color(0.8f, 0.92f, 1f, 0.8f); c1 = new Color(0.8f, 0.92f, 1f, 0.1f);
                    size.size = new ParticleSystem.MinMaxCurve(1, Curve(1f, 1f));
                    break;
                case FxKind.Sparkle:
                    main.loop = false;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
                    main.gravityModifier = 1.2f;
                    main.maxParticles = 80;
                    shape.angle = 40;
                    emission.rateOverTime = 0;
                    c0 = new Color(1f, 0.85f, 0.3f, 1f); c1 = new Color(1f, 0.95f, 0.6f, 0);
                    size.size = new ParticleSystem.MinMaxCurve(1, Curve(1f, 0.2f));
                    break;
                default: // Chimney
                    main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
                    main.maxParticles = 50;
                    emission.rateOverTime = 2.2f;
                    c0 = new Color(0.62f, 0.6f, 0.6f, 0.5f); c1 = new Color(0.75f, 0.75f, 0.78f, 0);
                    size.size = new ParticleSystem.MinMaxCurve(1, Curve(1f, 5f));
                    break;
            }
            main.startColor = c0;
            var w = kind == FxKind.Spray || kind == FxKind.Sparkle ? Vector3.zero : Wind;
            vel.x = new ParticleSystem.MinMaxCurve(w.x);
            vel.y = new ParticleSystem.MinMaxCurve(0);
            vel.z = new ParticleSystem.MinMaxCurve(w.z);

            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(c0, 0), new GradientColorKey(c1, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(c0.a, 0.08f), new GradientAlphaKey(c0.a * 0.6f, 0.5f), new GradientAlphaKey(c1.a, 1) });
            col.color = g;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Materials.Smoke;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = 10;
            if (kind != FxKind.Sparkle) ps.Play();
            return ps;
        }

        static AnimationCurve Curve(float a, float b)
        {
            return new AnimationCurve(new Keyframe(0, a), new Keyframe(1, b));
        }

        public static void SetRate(ParticleSystem ps, float rate)
        {
            if (ps == null) return;
            var e = ps.emission;
            e.rateOverTime = rate;
        }

        static ParticleSystem _sparkle;

        /// <summary>One-shot burst of golden sparkles at a world position (collect feedback).</summary>
        public static void Burst(Vector3 worldPos, int count = 30)
        {
            if (_sparkle == null) _sparkle = Create(FxKind.Sparkle, null, Vector3.zero);
            _sparkle.transform.position = worldPos;
            _sparkle.Emit(count);
        }

        static ParticleSystem _drain, _kick;

        /// <summary>
        /// A train pulling out: steam jetting sideways from the cylinder cocks (steam engines) and dust kicked up
        /// off the ballast under the lead wheels. <paramref name="lead"/> is the cylinders at rail level,
        /// <paramref name="forward"/> the direction of travel.
        /// </summary>
        public static void Departure(Vector3 lead, Vector3 forward, bool steam)
        {
            if (_drain == null) _drain = Quiet(FxKind.Steam);
            if (_kick == null) _kick = Quiet(FxKind.Dust);
            forward.y = 0;
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, forward);
            var p = new ParticleSystem.EmitParams();
            if (steam)
                for (int i = 0; i < 48; i++)
                {
                    float side = (i & 1) == 0 ? 1 : -1;
                    p.position = lead + right * side * 1.3f + Vector3.up * 0.8f + forward * Random.Range(-1f, 0.8f);
                    p.velocity = right * side * Random.Range(4f, 8f) + Vector3.up * Random.Range(0.4f, 1.6f) - forward * Random.Range(0f, 1.5f);
                    p.startSize = Random.Range(1f, 1.8f);
                    p.startLifetime = Random.Range(1.2f, 2.2f);
                    _drain.Emit(p, 1);
                }
            p = new ParticleSystem.EmitParams();
            for (int i = 0; i < 14; i++)
            {
                p.position = lead + right * Random.Range(-2f, 2f) + Vector3.up * 0.2f - forward * Random.Range(0f, 6f);
                p.velocity = right * Random.Range(-1.2f, 1.2f) + Vector3.up * Random.Range(0.3f, 1f);
                _kick.Emit(p, 1);
            }
        }

        /// <summary>A shared world-space system that only emits on demand.</summary>
        static ParticleSystem Quiet(FxKind kind)
        {
            var ps = Create(kind, null, Vector3.zero);
            var em = ps.emission;
            em.rateOverTime = 0;
            var main = ps.main;
            main.maxParticles = 300;
            return ps;
        }
    }
}
