using TMPro;
using UnityEngine;

namespace SignSight.UI
{
    /// <summary>
    /// Large word card (SPEC v3 §6): the word in large text, its English meaning on a smaller line below, and one more
    /// line with the movement cue (e.g. "Both fists — only the top hand cranks").
    /// </summary>
    public class WordCard : MonoBehaviour
    {
        [SerializeField] private TMP_Text wordText;
        [SerializeField] private TMP_Text meaningText;

        public string Word => wordText != null ? wordText.text : "";

        public void Show(string word, string meaning, string cue = null)
        {
            gameObject.SetActive(true);
            if (wordText != null) wordText.text = word;
            if (meaningText != null)
            {
                meaningText.text = string.IsNullOrEmpty(cue)
                    ? meaning
                    : $"{meaning}\n<size=80%><color=#BFD4FF>{cue}</color></size>";
            }
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
