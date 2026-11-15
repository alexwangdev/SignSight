using TMPro;
using UnityEngine;

namespace SignSight.UI
{
    /// <summary>
    /// Large word card (SPEC v3 §6, v3.2 A2 / B4): the word in large text, its meaning below, the movement cue in white,
    /// and — only when the teacher's face does something — one amber expression line naming brows / eyes / mouth, with
    /// "Not scored — watch and copy". The learner's face is never read or scored. Dark rounded backing sized to the text.
    /// </summary>
    public class WordCard : MonoBehaviour
    {
        [SerializeField] private TMP_Text wordText;
        [SerializeField] private TMP_Text meaningText;

        public const string NotScored = "Not scored — watch and copy";

        public string Word => wordText != null ? wordText.text : "";
        public string FaceLine { get; private set; } = "";

        private string _meaning, _cue;
        private bool _highlight;

        private void Awake()
        {
            // One shared backing style (UIStyle): grows with the text, never smaller than the v3 card.
            var old = transform.Find("Background");
            if (old != null && old.TryGetComponent<Renderer>(out var r)) r.enabled = false;
            var b = TextBacking.For(gameObject, wordText, meaningText);
            b.MinSize = new Vector2(0.52f, 0.26f);
            b.Depth = 0.01f;
            // Extra lines (cue, expression) flow down from the meaning line instead of spreading over the word.
            if (meaningText != null) meaningText.alignment = TextAlignmentOptions.Top;
            // The word stays on one line: long titles ("Learn 50 signs", lesson summaries) shrink instead of wrapping
            // into the meaning line. Sign words keep the v3 size.
            if (wordText != null)
            {
                wordText.enableAutoSizing = true;
                wordText.fontSizeMax = wordText.fontSize;
                wordText.fontSizeMin = wordText.fontSize * 0.4f;
                wordText.enableWordWrapping = false;
            }
        }

        public void Show(string word, string meaning, string cue = null, string faceLine = null)
        {
            gameObject.SetActive(true);
            if (wordText != null) wordText.text = word;
            _meaning = meaning;
            _cue = cue;
            FaceLine = faceLine ?? "";
            Render();
        }

        /// <summary>Highlights the expression line (while the FACE replay plays).</summary>
        public void HighlightFace(bool on)
        {
            if (_highlight == on) return;
            _highlight = on;
            Render();
        }

        private void Render()
        {
            if (meaningText == null) return;
            var s = _meaning ?? "";
            if (!string.IsNullOrEmpty(_cue)) s += $"\n<size=80%><color=#FFFFFF>{_cue}</color></size>";
            if (!string.IsNullOrEmpty(FaceLine))
            {
                string line = _highlight ? $"<b><u>Face: {FaceLine}</u></b>" : $"Face: {FaceLine}";
                s += $"\n<size=72%><color={UIStyle.Amber}>{line}</color></size>\n<size=58%><color={UIStyle.Amber}>{NotScored}</color></size>";
            }
            meaningText.text = s;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
