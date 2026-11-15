using System.Collections;
using System.Collections.Generic;
using SignSight.UI;
using TMPro;
using UnityEngine;

namespace SignSight.Learn
{
    /// <summary>
    /// Visual reward for a correct answer (v3.2 A1). Silent: a confetti burst (one reused ParticleSystem, gold + green,
    /// 40 particles, 1 s), the title popping 0.8x -> 1.15x -> 1.0x over 0.4 s, and stars popping in one at a time
    /// (3 for first try, 2 for second try), plus the streak line in the same pop. Wrong answers never call this.
    /// Kept in its own file (not inside FeedbackPanel) so FeedbackPanel only gains one call.
    /// </summary>
    public class CelebrationFX : MonoBehaviour
    {
        public const int Particles = 40;
        public const float BurstSeconds = 1.0f;
        public const float PopSeconds = 0.4f;
        public const float StarSize = 0.035f;
        public const float StarGap = 0.04f;
        public static readonly Color Gold = new Color(1f, 0.82f, 0.29f);
        public static readonly Color Green = new Color(0.30f, 0.85f, 0.39f);

        private ParticleSystem _confetti;
        private readonly List<Transform> _stars = new List<Transform>();
        private Transform _starRow;
        private Coroutine _routine;

        public int Plays { get; private set; }

        /// <summary>Stars row and confetti live under 'anchor' (the feedback text column).</summary>
        public static CelebrationFX Create(Transform anchor)
        {
            var go = new GameObject("CelebrationFX");
            go.transform.SetParent(anchor, false);
            var fx = go.AddComponent<CelebrationFX>();
            fx.Build();
            return fx;
        }

        private void Build()
        {
            // Confetti: one system, reused; cone burst upward/forward, gravity pulls it down; no collisions, no audio.
            var ps = new GameObject("Confetti").AddComponent<ParticleSystem>();
            ps.transform.SetParent(transform, false);
            ps.transform.localPosition = new Vector3(0f, 0.02f, -0.03f);
            ps.transform.localRotation = Quaternion.Euler(-70f, 0f, 0f);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = BurstSeconds;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, BurstSeconds);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.014f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.35f;
            main.maxParticles = 100;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(Gold, Green) { mode = ParticleSystemGradientMode.TwoColors };
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Particles) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.02f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Resources.Load<Material>("UI/Confetti");
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _confetti = ps;

            _starRow = new GameObject("Stars").transform;
            _starRow.SetParent(transform, false);
            for (int i = 0; i < 3; i++)
            {
                var s = UIStyle.AddIcon(_starRow, "star", Vector3.zero, StarSize, "Star" + (i + 1)).transform;
                s.GetComponent<MeshRenderer>().sharedMaterial = StarMaterial();
                s.gameObject.SetActive(false);
                _stars.Add(s);
            }
        }

        private static Material _starMat;

        private static Material StarMaterial()
        {
            if (_starMat != null) return _starMat;
            _starMat = new Material(UIStyle.IconMaterial("star")) { name = "UIIcon_star_gold" };
            _starMat.SetColor("_BaseColor", Gold);
            return _starMat;
        }

        /// <summary>Celebrate. 'title' and 'streak' pop; stars appear at 'starsAt' (local to this object's parent).</summary>
        public void Play(TMP_Text title, TMP_Text streak, int stars, Vector3 starsAt)
        {
            Plays++;
            if (_routine != null) StopCoroutine(_routine);
            _confetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _confetti.Play(true);
            _routine = StartCoroutine(Pop(title, streak, Mathf.Clamp(stars, 0, 3), starsAt));
        }

        public void Hide()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            foreach (var s in _stars) s.gameObject.SetActive(false);
        }

        private static float PopScale(float u)
        {
            // 0.8 -> 1.15 (at 60 %) -> 1.0
            if (u < 0.6f) return Mathf.Lerp(0.8f, 1.15f, Mathf.SmoothStep(0f, 1f, u / 0.6f));
            return Mathf.Lerp(1.15f, 1f, Mathf.SmoothStep(0f, 1f, (u - 0.6f) / 0.4f));
        }

        private IEnumerator Pop(TMP_Text title, TMP_Text streak, int stars, Vector3 starsAt)
        {
            foreach (var s in _stars) s.gameObject.SetActive(false);
            _starRow.position = transform.parent.TransformPoint(starsAt);
            for (int i = 0; i < stars; i++) _stars[i].localPosition = new Vector3((i - (stars - 1) * 0.5f) * StarGap, 0f, -0.002f);

            var targets = new List<Transform>();
            if (title != null) targets.Add(title.transform);
            if (streak != null && !string.IsNullOrEmpty(streak.text)) targets.Add(streak.transform);
            for (float t = 0f; t < PopSeconds; t += Time.deltaTime)
            {
                float k = PopScale(t / PopSeconds);
                foreach (var tr in targets) tr.localScale = Vector3.one * k;
                yield return null;
            }
            foreach (var tr in targets) tr.localScale = Vector3.one;

            // Stars one at a time, each with its own small pop.
            for (int i = 0; i < stars; i++)
            {
                var s = _stars[i];
                s.gameObject.SetActive(true);
                for (float t = 0f; t < 0.2f; t += Time.deltaTime)
                {
                    s.localScale = Vector3.one * (StarSize * PopScale(t / 0.2f));
                    yield return null;
                }
                s.localScale = Vector3.one * StarSize;
            }
            _routine = null;
        }
    }
}
