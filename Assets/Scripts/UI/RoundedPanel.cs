using UnityEngine;

namespace SignSight.UI
{
    /// <summary>
    /// Turns a scaled quad backing into the shared rounded panel (same corner radius and colour as every other panel,
    /// UIStyle) at its current width / height.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class RoundedPanel : MonoBehaviour
    {
        private void Awake()
        {
            var s = transform.localScale;
            GetComponent<MeshFilter>().sharedMesh = UIStyle.RoundedRect(Mathf.Abs(s.x), Mathf.Abs(s.y), UIStyle.CornerRadius);
            transform.localScale = new Vector3(1f, 1f, s.z);
            GetComponent<MeshRenderer>().sharedMaterial = UIStyle.PanelMaterial;
        }
    }
}
