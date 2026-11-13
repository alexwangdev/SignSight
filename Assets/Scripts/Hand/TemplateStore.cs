using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SignSight.Hand
{
    /// <summary>One recorded hold of a sign.</summary>
    [Serializable]
    public class SignSample
    {
        public float[] curl = new float[5];
        public Vector3 palm;
    }

    /// <summary>One sign template: curl[5] + palm + tolerance (SPEC §4.2), plus the recordings it was averaged from.</summary>
    [Serializable]
    public class SignTemplate
    {
        public string intent;
        public string phrase;
        public float[] curl = new float[5];
        public Vector3 palm;
        public string palmLabel;
        public float tolerance = 0.35f;
        public List<SignSample> samples = new List<SignSample>();
    }

    [Serializable]
    public class TemplateFile
    {
        public int version = 1;
        public List<SignTemplate> templates = new List<SignTemplate>();
    }

    /// <summary>Distance of the live hand to one template. confidence = 1 - distance, both 0..1.</summary>
    public struct TemplateScore
    {
        public SignTemplate Template;
        public float Distance;
        public float Confidence => Mathf.Clamp01(1f - Distance);
        public bool WithinTolerance => Template != null && Distance <= Template.tolerance;
    }

    /// <summary>
    /// templates.json read/write (SPEC §4.4). Defaults come from Resources/templates.json (the §4.3 initial values);
    /// recordings are saved to Application.persistentDataPath/templates.json and override the defaults.
    /// </summary>
    public class TemplateStore
    {
        public const string FileName = "templates.json";
        public const int MaxSamplesPerIntent = 3;

        // Weighted distance (SPEC §4.5): index/middle count more, thumb less.
        private static readonly float[] CurlWeights = { 0.6f, 1.3f, 1.2f, 1.0f, 1.0f };
        private const float CurlShare = 0.75f; // the rest is palm direction

        public TemplateFile Data { get; private set; }
        public IReadOnlyList<SignTemplate> Templates => Data.templates;

        public static string UserPath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Loads the user's recorded templates if present, otherwise the defaults.</summary>
        public static TemplateStore Load()
        {
            var store = new TemplateStore();
            if (File.Exists(UserPath))
            {
                try
                {
                    store.Data = JsonUtility.FromJson<TemplateFile>(File.ReadAllText(UserPath));
                }
                catch (Exception e)
                {
                    Debug.LogError($"[TemplateStore] Could not read {UserPath}, using defaults. {e.Message}");
                }
            }
            if (store.Data == null || store.Data.templates.Count == 0) store.Data = LoadDefaults();
            return store;
        }

        public static TemplateFile LoadDefaults()
        {
            var asset = Resources.Load<TextAsset>(Path.GetFileNameWithoutExtension(FileName));
            if (asset == null)
            {
                Debug.LogError("[TemplateStore] Resources/templates.json is missing.");
                return new TemplateFile();
            }
            return JsonUtility.FromJson<TemplateFile>(asset.text);
        }

        public void Save()
        {
            File.WriteAllText(UserPath, JsonUtility.ToJson(Data, true));
        }

        /// <summary>Discards every recording and goes back to the §4.3 initial values.</summary>
        public void ResetToDefaults()
        {
            Data = LoadDefaults();
            if (File.Exists(UserPath)) File.Delete(UserPath);
        }

        public SignTemplate Get(string intent) => Data.templates.Find(t => t.intent == intent);

        /// <summary>Adds one recording (keeping the latest 3) and sets the template to their average.</summary>
        public void AddSample(string intent, SignSample sample)
        {
            var t = Get(intent);
            if (t == null) throw new ArgumentException($"Unknown intent {intent}");
            t.samples.Add(sample);
            while (t.samples.Count > MaxSamplesPerIntent) t.samples.RemoveAt(0);

            var curl = new float[5];
            var palm = Vector3.zero;
            foreach (var s in t.samples)
            {
                for (int i = 0; i < 5; i++) curl[i] += s.curl[i] / t.samples.Count;
                palm += s.palm;
            }
            t.curl = curl;
            t.palm = palm.sqrMagnitude > 1e-6f ? palm.normalized : t.palm;
            t.palmLabel = CurlCalculator.Classify(t.palm).ToString();
        }

        /// <summary>Removes the recordings for one intent and restores its default values.</summary>
        public void ClearIntent(string intent)
        {
            var t = Get(intent);
            var d = LoadDefaults().templates.Find(x => x.intent == intent);
            if (t == null || d == null) return;
            t.samples.Clear();
            t.curl = d.curl;
            t.palm = d.palm;
            t.palmLabel = d.palmLabel;
        }

        /// <summary>Normalised weighted distance 0..1 between a live hand and a template.</summary>
        public static float Distance(float[] curl, Vector3 palm, SignTemplate t)
        {
            float sum = 0f, wsum = 0f;
            for (int i = 0; i < 5; i++)
            {
                float d = curl[i] - t.curl[i];
                sum += CurlWeights[i] * d * d;
                wsum += CurlWeights[i];
            }
            float curlTerm = sum / wsum;                          // 0..1
            float palmTerm = Vector3.Angle(palm, t.palm) / 180f;  // 0..1
            return Mathf.Sqrt(CurlShare * curlTerm + (1f - CurlShare) * palmTerm * palmTerm);
        }

        /// <summary>Scores every template against the live hand; best first.</summary>
        public List<TemplateScore> Rank(float[] curl, Vector3 palm)
        {
            var scores = new List<TemplateScore>(Data.templates.Count);
            foreach (var t in Data.templates)
            {
                scores.Add(new TemplateScore { Template = t, Distance = Distance(curl, palm, t) });
            }
            scores.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            return scores;
        }
    }
}
