using UnityEngine;

namespace SignSight.Explore
{
    /// <summary>
    /// An object the learner can point at in Explore mode (Task A: simple primitives). Carries only a COCO-style label;
    /// what it means is decided by CocoMapping. No floating word: the word appears only after the learner points.
    /// </summary>
    public class ExploreObject : MonoBehaviour
    {
        public string Label = "apple";

        private Vector3 _scale;
        private bool _targeted;

        private void Awake() => _scale = transform.localScale;

        /// <summary>Slight grow while the pointing ray is on it (visual only).</summary>
        public void SetTargeted(bool on)
        {
            if (_targeted == on) return;
            _targeted = on;
            transform.localScale = on ? _scale * 1.08f : _scale;
        }
    }
}
