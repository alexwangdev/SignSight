using TMPro;
using UnityEngine;

namespace SignSight.UI
{
    /// <summary>Large word card (SPEC §6): the word in large text, its English meaning on a smaller line below.</summary>
    public class WordCard : MonoBehaviour
    {
        [SerializeField] private TMP_Text wordText;
        [SerializeField] private TMP_Text meaningText;

        public string Word => wordText != null ? wordText.text : "";

        public void Show(string word, string meaning)
        {
            gameObject.SetActive(true);
            if (wordText != null) wordText.text = word;
            if (meaningText != null) meaningText.text = meaning;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
