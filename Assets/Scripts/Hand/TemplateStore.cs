using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SignSight.Hand
{
    /// <summary>One recorded start pose of a sign (right / dominant hand, plus the left hand for two-handed signs).</summary>
    [Serializable]
    public class SignSample
    {
        public float[] curl = new float[5];
        public Vector3 palm;
        public float[] curlLeft = new float[0];
        /// <summary>Movement amplitude measured when the movement was performed once (template unit); 0 = none.</summary>
        public float movementAmplitude;
        /// <summary>Palm centres at the start pose, signer's body frame (metres). hasStart = false for old recordings.</summary>
        public bool hasStart;
        public Vector3 startRight, startLeft;
    }

    /// <summary>Movement part of a sign (SPEC v3 §4.5). type is one of MovementTypes.</summary>
    [Serializable]
    public class SignMovement
    {
        public string type = "none";
        public string axis = "";
        public float amplitude;
        public string unit = "";
        public int repeats;
        public float durationSec;
        public string movingHand = "";
    }

    /// <summary>
    /// Teacher-only head motion, never scored: none | nod | shake (v3) | tilt (sideways roll, held; WH-questions) |
    /// bow (forward pitch, held). Only where Lifeprint describes it.
    /// </summary>
    [Serializable]
    public class SignHead
    {
        public string type = "none";
        public float amplitudeDeg;
        public int repeats;
    }

    /// <summary>
    /// Teacher-only face, ARKit blendshape weights 0..100 (v3.2). Demonstration only: the learner's face is never read
    /// or scored (Quest 3 / 3S has no face tracking). 'preset' names a FaceDriver preset (Smile, Sad, Furrowed, Tired,
    /// Pain) whose values are used instead of the fields; 'name' is the short label for the expression line.
    /// Old files with only {smile, jawOpen} still load (all other fields default to 0).
    /// </summary>
    [Serializable]
    public class SignFace
    {
        public float smile;
        public float jawOpen;
        public float browInnerUp, browDown, browOuterUp;
        public float eyeSquint, eyeWide;
        public float mouthFrown, mouthPress, mouthPucker;
        public float cheekSquint, noseSneer;
        public string preset = "";
        public string name = "";

        public bool IsNeutral =>
            string.IsNullOrEmpty(preset) && smile <= 0f && jawOpen <= 0f && browInnerUp <= 0f && browDown <= 0f &&
            browOuterUp <= 0f && eyeSquint <= 0f && eyeWide <= 0f && mouthFrown <= 0f && mouthPress <= 0f &&
            mouthPucker <= 0f && cheekSquint <= 0f && noseSneer <= 0f;
    }

    /// <summary>
    /// One sign = handshape + location + movement + head / face (SPEC v3 §4.1, §4.5).
    /// Curl order is [thumb, index, middle, ring, pinky], 0 = straight, 1 = fully curled.
    /// Palm is in the signer's own space (x = signer's right, y = up, z = forward).
    /// </summary>
    [Serializable]
    public class SignTemplate
    {
        public string intent;
        public string phrase;
        public string hands = "right";                // "right" | "both"
        public float[] curlRight = new float[5];
        public float[] curlLeft = new float[0];       // required when hands == "both"
        public float spread = 0.1f;                   // §4.3: 0 = fingers together, 1 = fully spread
        public Vector3 palm;
        public string palmLabel;
        public string startLocation = "";
        public SignMovement movement = new SignMovement();
        public float[] endCurlRight = new float[0];   // empty = same as curlRight
        public SignHead head = new SignHead();
        public SignFace face = new SignFace();
        public float tolerance = 0.35f;
        public List<SignSample> samples = new List<SignSample>();
        /// <summary>
        /// Recorded start location (average of the recordings' start palms, body frame). When set, location is judged
        /// against where this learner actually starts the sign instead of the generic body anchors — which also
        /// calibrates away the Simulator's webcam offset.
        /// </summary>
        public bool hasRecordedStart;
        public Vector3 recordedStartRight, recordedStartLeft;

        // ---- v3.1 (50 words) ----
        /// <summary>false = TEACH-ONLY: the teacher demonstrates it, the learner practises, nothing is scored.</summary>
        public bool scored = true;
        /// <summary>Why the word is not scored (shown nowhere in the app; kept for the report).</summary>
        public string teachOnlyReason = "";
        /// <summary>
        /// Named start location (SignAnchors). Empty for the 8 v3 signs, which keep their v3 location rules.
        /// When set, location is judged as the distance of the contact point to this anchor.
        /// </summary>
        public string anchor = "";
        /// <summary>Part of the dominant hand at the anchor: thumb | index | middle | fingers | palm | knuckle.</summary>
        public string contact = "palm";
        /// <summary>Allowed slack around the anchor (metres) before location starts to cost.</summary>
        public float anchorRadius = 0.08f;
        /// <summary>Direction the dominant hand's fingers point at the start (signer space).</summary>
        public Vector3 fingerDir = Vector3.up;
        /// <summary>Non-dominant hand start (two-handed signs): anchor, contact, palm, finger direction.</summary>
        public string leftAnchor = "";
        public string leftContact = "palm";
        /// <summary>Metres added to the anchor (right hand) and to the left anchor; leftAnchor "mirror" = the right start mirrored.</summary>
        public Vector3 anchorOffset = Vector3.zero;
        public Vector3 leftOffset = Vector3.zero;
        public Vector3 leftPalm = Vector3.zero;
        public Vector3 leftFingerDir = Vector3.zero;
        /// <summary>Movement direction for stroke (signer space; the left hand mirrors x when both hands move).</summary>
        public Vector3 direction = Vector3.zero;
        /// <summary>Stroke only: the palm must end facing this way (within 50 deg), e.g. BAD ends palm down. Zero = no check.</summary>
        public Vector3 endPalm = Vector3.zero;
        /// <summary>Caption for non-manual markers the app does not score (e.g. WH-question eyebrows).</summary>
        public string nonManual = "";
        /// <summary>Second beat of a compound sign (teacher only; compounds are TEACH-ONLY).</summary>
        public SignBeat beat2 = new SignBeat();

        public bool TwoHanded => hands == "both";
        public bool HasLeftCurl => curlLeft != null && curlLeft.Length == 5;
        public float[] EndCurlRight => endCurlRight != null && endCurlRight.Length == 5 ? endCurlRight : curlRight;
    }

    /// <summary>Second part of a compound sign (e.g. GOOD + MORNING). Teacher display only.</summary>
    [Serializable]
    public class SignBeat
    {
        public string anchor = "";
        public string contact = "palm";
        public Vector3 palm = Vector3.zero;
        public Vector3 fingerDir = Vector3.zero;
        public float[] curlRight = new float[0];
        public float[] curlLeft = new float[0];
        public string leftAnchor = "";
        public Vector3 leftPalm = Vector3.zero;
        public Vector3 leftFingerDir = Vector3.zero;
        public SignMovement movement = new SignMovement();
        public Vector3 direction = Vector3.zero;

        public bool Present => !string.IsNullOrEmpty(anchor);
    }

    public static class MovementTypes
    {
        public const string None = "none";
        public const string Wave = "wave";
        public const string WristNod = "wrist-nod";
        public const string Snap = "snap";
        public const string PushForward = "push-forward";
        public const string Crank = "crank";
        public const string FlickOut = "flick-out";
        public const string Shake = "shake";
        public const string Lift = "lift";

        // v3.1: added so the 42 new words can be scored (each needs the anchor field for location).
        public const string Tap = "tap";           // contact point touches the anchor and leaves, 'repeats' times
        public const string Stroke = "stroke";     // contact point travels 'amplitude' cm along 'direction' from the anchor
        public const string Circle = "circle";     // palm circles in a plane (axis: frontal | sagittal | horizontal)
        public const string Twist = "twist";       // wrist roll back and forth about the hand axis
        public const string Turn = "turn";         // the palm turns once by 'amplitude' degrees (e.g. HOW, TIRED)
        public const string Squeeze = "squeeze";   // fingers in 'axis' close and open, 'repeats' times
        public const string Converge = "converge"; // the two hands' contact points come together, 'repeats' times
        public const string Hold = "hold";         // both hands held still at the anchor (LOVE)
        public const string Rock = "rock";         // both forearms sway side to side, slowly (BABY)

        public static bool IsV3(string type) =>
            type == Wave || type == WristNod || type == Snap || type == PushForward || type == Crank || type == FlickOut ||
            type == Shake || type == Lift;
    }

    [Serializable]
    public class TemplateFile
    {
        public int version = TemplateStore.CurrentVersion;
        public List<SignTemplate> templates = new List<SignTemplate>();
    }

    // ---- v1 schema, read only for migration ----
    [Serializable]
    internal class SignTemplateV1
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
    internal class TemplateFileV1
    {
        public int version = 1;
        public List<SignTemplateV1> templates = new List<SignTemplateV1>();
    }

    [Serializable]
    internal class VersionProbe
    {
        public int version = 1;
    }

    /// <summary>Handshape distance of the live hand to one template. confidence = 1 - distance, both 0..1.</summary>
    public struct TemplateScore
    {
        public SignTemplate Template;
        public float Distance;
        public float Confidence => Mathf.Clamp01(1f - Distance);
        public bool WithinTolerance => Template != null && Distance <= Template.tolerance;
    }

    /// <summary>
    /// templates.json read/write (SPEC v3 §4.5). Defaults come from Resources/templates.json (the §4.3 initial values);
    /// recordings are saved to Application.persistentDataPath/templates.json and override the defaults.
    /// v1 files are migrated on load: curl becomes curlRight, movement = none.
    /// </summary>
    public class TemplateStore
    {
        public const string FileName = "templates.json";
        public const int CurrentVersion = 2;
        public const int MaxSamplesPerIntent = 3;

        // Weighted handshape distance: index/middle count more, thumb less.
        private static readonly float[] CurlWeights = { 0.6f, 1.3f, 1.2f, 1.0f, 1.0f };
        private const float CurlShare = 0.75f; // the rest is palm direction

        public TemplateFile Data { get; private set; }
        public IReadOnlyList<SignTemplate> Templates => Data.templates;

        public static string UserPath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Loads the user's recorded templates if present (migrating v1), otherwise the defaults.</summary>
        public static TemplateStore Load()
        {
            var store = new TemplateStore();
            if (File.Exists(UserPath))
            {
                try
                {
                    store.Data = Parse(File.ReadAllText(UserPath), UserPath);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[TemplateStore] Could not read {UserPath}, using defaults. {e.Message}");
                }
            }
            if (store.Data == null || store.Data.templates.Count == 0) store.Data = LoadDefaults();
            return store;
        }

        /// <summary>A store with only the §4.3 defaults, ignoring the user's recordings (tests, demos).</summary>
        public static TemplateStore FromDefaults() => new TemplateStore { Data = LoadDefaults() };

        private static TemplateFile _canonical;

        /// <summary>
        /// The §4.3 action definition for a word (Resources/templates.json), never changed by recordings.
        /// The teacher demonstrates from this, so a recording can tune recognition but never the ASL shown.
        /// </summary>
        public static SignTemplate Canonical(string intent)
        {
            if (_canonical == null) _canonical = LoadDefaults();
            return _canonical.templates.Find(t => t.intent == intent);
        }

        public static TemplateFile LoadDefaults()
        {
            var asset = Resources.Load<TextAsset>(Path.GetFileNameWithoutExtension(FileName));
            if (asset == null)
            {
                Debug.LogError("[TemplateStore] Resources/templates.json is missing.");
                return new TemplateFile();
            }
            return Parse(asset.text, "Resources/templates.json", migrateAgainstDefaults: false);
        }

        /// <summary>Reads a v1 or v2 file. v1 is migrated (SPEC v3 §4.5) and one line is logged.</summary>
        private static TemplateFile Parse(string json, string source, bool migrateAgainstDefaults = true)
        {
            int version = JsonUtility.FromJson<VersionProbe>(json).version;
            if (version >= CurrentVersion)
            {
                var file2 = JsonUtility.FromJson<TemplateFile>(json);
                if (migrateAgainstDefaults) MergeDefinitions(file2, LoadDefaults(), source);
                return file2;
            }

            var v1 = JsonUtility.FromJson<TemplateFileV1>(json);
            var defaults = migrateAgainstDefaults ? LoadDefaults() : null;
            var file = new TemplateFile();
            var notInV3 = new List<string>();
            var filled = new List<string>();
            foreach (var o in v1.templates)
            {
                var t = new SignTemplate
                {
                    intent = o.intent,
                    phrase = o.phrase,
                    curlRight = o.curl,
                    palm = o.palm,
                    palmLabel = o.palmLabel,
                    tolerance = o.tolerance,
                    samples = o.samples ?? new List<SignSample>(),
                    movement = new SignMovement { type = MovementTypes.None },
                };
                var d = defaults?.templates.Find(x => x.intent == o.intent);
                if (d != null)
                {
                    // Fields v1 never had come from the v3 definition; the recorded handshape is kept.
                    t.phrase = d.phrase;
                    t.hands = d.hands;
                    t.curlLeft = d.curlLeft;
                    t.spread = d.spread;
                    t.startLocation = d.startLocation;
                    t.movement = d.movement;
                    t.endCurlRight = d.endCurlRight;
                    t.head = d.head;
                    t.face = d.face;
                }
                else notInV3.Add(o.intent);
                file.templates.Add(t);
            }
            if (defaults != null)
            {
                foreach (var d in defaults.templates)
                {
                    if (file.templates.Exists(t => t.intent == d.intent)) continue;
                    file.templates.Add(d);
                    filled.Add(d.intent);
                }
            }
            Debug.Log($"[TemplateStore] Migrated {source} from v1 to v2: {v1.templates.Count} templates, old curl -> curlRight, " +
                      "movement / hands / head / face from the v3 definitions (none for unknown words)" +
                      (filled.Count > 0 ? $"; added new v3 words: {string.Join(", ", filled)}" : "") +
                      (notInV3.Count > 0 ? $"; not v3 words, kept but unused by lessons: {string.Join(", ", notInV3)}" : "") +
                      ". Old recordings predate the v3 handshapes: re-record in TemplateRecorder (or Reset).");
            return file;
        }

        /// <summary>
        /// A learner's saved file keeps its recordings (handshape, amplitude, start location), but the definition of
        /// each word comes from the shipped file: new words are added and the v3.1 fields are refreshed.
        /// </summary>
        private static void MergeDefinitions(TemplateFile file, TemplateFile defaults, string source)
        {
            var added = new List<string>();
            foreach (var d in defaults.templates)
            {
                var t = file.templates.Find(x => x.intent == d.intent);
                if (t == null)
                {
                    file.templates.Add(d);
                    added.Add(d.intent);
                    continue;
                }
                t.scored = d.scored;
                t.teachOnlyReason = d.teachOnlyReason;
                t.anchor = d.anchor;
                t.contact = d.contact;
                t.anchorRadius = d.anchorRadius;
                t.fingerDir = d.fingerDir;
                t.leftAnchor = d.leftAnchor;
                t.leftContact = d.leftContact;
                t.anchorOffset = d.anchorOffset;
                t.leftOffset = d.leftOffset;
                t.leftPalm = d.leftPalm;
                t.leftFingerDir = d.leftFingerDir;
                t.direction = d.direction;
                t.endPalm = d.endPalm;
                t.nonManual = d.nonManual;
                t.beat2 = d.beat2;
                t.face = d.face;  // teacher display data: always the shipped definition
                t.head = d.head;
                if (!MovementTypes.IsV3(d.movement.type)) t.movement = d.movement;
            }
            // Keep the shipped order (lessons and reports follow it).
            file.templates.Sort((a, b) => defaults.templates.FindIndex(x => x.intent == a.intent)
                                          .CompareTo(defaults.templates.FindIndex(x => x.intent == b.intent)));
            if (added.Count > 0) Debug.Log($"[TemplateStore] {source}: added {added.Count} new words from the shipped definitions.");
        }

        public void Save()
        {
            Data.version = CurrentVersion;
            File.WriteAllText(UserPath, JsonUtility.ToJson(Data, true));
        }

        /// <summary>Discards every recording and goes back to the §4.3 initial values.</summary>
        public void ResetToDefaults()
        {
            Data = LoadDefaults();
            if (File.Exists(UserPath)) File.Delete(UserPath);
        }

        public SignTemplate Get(string intent) => Data.templates.Find(t => t.intent == intent);

        /// <summary>Adds one recording (keeping the latest 3) and sets the template's start handshape to their average.</summary>
        public void AddSample(string intent, SignSample sample)
        {
            var t = Get(intent);
            if (t == null) throw new ArgumentException($"Unknown intent {intent}");
            t.samples.Add(sample);
            while (t.samples.Count > MaxSamplesPerIntent) t.samples.RemoveAt(0);

            var curl = new float[5];
            var left = new float[5];
            int leftCount = 0;
            var palm = Vector3.zero;
            foreach (var s in t.samples)
            {
                for (int i = 0; i < 5; i++) curl[i] += s.curl[i] / t.samples.Count;
                palm += s.palm;
                if (s.curlLeft != null && s.curlLeft.Length == 5)
                {
                    for (int i = 0; i < 5; i++) left[i] += s.curlLeft[i];
                    leftCount++;
                }
            }
            t.curlRight = curl;
            if (leftCount > 0)
            {
                for (int i = 0; i < 5; i++) left[i] /= leftCount;
                t.curlLeft = left;
            }
            t.palm = palm.sqrMagnitude > 1e-6f ? palm.normalized : t.palm;
            t.palmLabel = CurlCalculator.Classify(t.palm).ToString();

            // Movement amplitude: average of the recorded performances, kept within 0.5x..1.5x of the §4.3 value
            // so one odd recording cannot make the sign trivial or impossible.
            float amp = 0f;
            int ampCount = 0;
            foreach (var s in t.samples)
            {
                if (s.movementAmplitude <= 0f) continue;
                amp += s.movementAmplitude;
                ampCount++;
            }
            // Start location: average of the recordings that have one.
            var sr = Vector3.zero;
            var sl = Vector3.zero;
            int sn = 0;
            foreach (var s in t.samples)
            {
                if (!s.hasStart) continue;
                sr += s.startRight;
                sl += s.startLeft;
                sn++;
            }
            t.hasRecordedStart = sn > 0;
            if (sn > 0)
            {
                t.recordedStartRight = sr / sn;
                t.recordedStartLeft = sl / sn;
            }

            var canon = Canonical(intent);
            if (ampCount > 0 && canon != null && canon.movement.amplitude > 0f)
            {
                float c = canon.movement.amplitude;
                t.movement.amplitude = Mathf.Clamp(amp / ampCount, 0.5f * c, 1.5f * c);
            }
        }

        /// <summary>Removes the recordings for one intent and restores its default values.</summary>
        public void ClearIntent(string intent)
        {
            var i = Data.templates.FindIndex(t => t.intent == intent);
            var d = LoadDefaults().templates.Find(x => x.intent == intent);
            if (i < 0 || d == null) return;
            Data.templates[i] = d;
        }

        /// <summary>Normalised weighted handshape distance 0..1 (curl + palm) between a live hand and a template.</summary>
        public static float Distance(float[] curl, Vector3 palm, SignTemplate t)
        {
            float sum = 0f, wsum = 0f;
            for (int i = 0; i < 5; i++)
            {
                float d = curl[i] - t.curlRight[i];
                sum += CurlWeights[i] * d * d;
                wsum += CurlWeights[i];
            }
            float curlTerm = sum / wsum;                          // 0..1
            float palmTerm = Vector3.Angle(palm, t.palm) / 180f;  // 0..1
            return Mathf.Sqrt(CurlShare * curlTerm + (1f - CurlShare) * palmTerm * palmTerm);
        }

        /// <summary>Weighted curl-only distance 0..1 (used for the non-dominant hand, whose palm is not in the schema).</summary>
        public static float CurlDistance(float[] curl, float[] target)
        {
            float sum = 0f, wsum = 0f;
            for (int i = 0; i < 5; i++)
            {
                float d = curl[i] - target[i];
                sum += CurlWeights[i] * d * d;
                wsum += CurlWeights[i];
            }
            return Mathf.Sqrt(sum / wsum);
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
