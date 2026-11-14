using UnityEngine;

namespace SignSight.Hand
{
    /// <summary>
    /// Named start locations for the v3.1 signs, in the signer's body frame relative to the point between the eyes
    /// (x = signer's right, y = up, z = toward the partner; metres). The recogniser (learner's headset as the eyes)
    /// and the teacher (Rocketbox eye bones) use the same table, so a location means the same thing to both.
    /// Side anchors (cheek, temple, ...) are on the dominant (right) side.
    /// </summary>
    public static class SignAnchors
    {
        public static bool TryGet(string name, out Vector3 p)
        {
            switch (name)
            {
                case "forehead": p = new Vector3(0f, 0.055f, 0.045f); return true;
                case "temple": p = new Vector3(0.07f, 0.03f, -0.01f); return true;
                case "side-head": p = new Vector3(0.075f, -0.03f, 0.0f); return true;
                case "cheek": p = new Vector3(0.05f, -0.05f, 0.03f); return true;
                case "jaw": p = new Vector3(0.045f, -0.09f, 0.02f); return true;
                case "nose": p = new Vector3(0f, -0.035f, 0.06f); return true;
                case "face": p = new Vector3(0f, -0.04f, 0.10f); return true;
                case "mouth": p = new Vector3(0f, -0.075f, 0.04f); return true;
                case "chin": p = new Vector3(0f, -0.105f, 0.035f); return true;
                case "throat": p = new Vector3(0f, -0.15f, 0.0f); return true;
                case "upper-chest": p = new Vector3(0f, -0.25f, 0.06f); return true;
                case "chest": p = new Vector3(0f, -0.33f, 0.08f); return true;
                case "stomach": p = new Vector3(0f, -0.50f, 0.10f); return true;
                case "neutral": p = new Vector3(0.06f, -0.33f, 0.28f); return true;
                default: p = Vector3.zero; return false;
            }
        }

        /// <summary>Anchors that sit on the non-dominant hand and move with it.</summary>
        public static bool IsOnLeftHand(string name) => name != null && name.StartsWith("left-");

        /// <summary>Point on the left hand for a left-hand anchor (left-palm | left-wrist | left-index | left-back).</summary>
        public static Vector3 OnLeftHand(string name, in HandSample left)
        {
            switch (name)
            {
                case "left-wrist": return left.Wrist;
                case "left-index": return left.IndexTip;
                case "left-back": return left.Palm - left.Normal * 0.02f;
                default: return left.Palm + left.Normal * 0.02f;
            }
        }

        /// <summary>Where the anchor is in this frame; false if it is on the left hand and that hand is not tracked.</summary>
        public static bool Resolve(string name, in TrajFrame f, out Vector3 p)
        {
            if (IsOnLeftHand(name))
            {
                p = f.Left.Tracked ? OnLeftHand(name, f.Left) : Vector3.zero;
                return f.Left.Tracked;
            }
            return TryGet(name, out p);
        }

        public static Vector3 MirrorX(Vector3 v) => new Vector3(-v.x, v.y, v.z);

        /// <summary>Start point of the dominant hand's contact point (body anchor + offset, or a point on the left hand).</summary>
        public static bool RightTarget(SignTemplate t, in TrajFrame f, out Vector3 p)
        {
            if (IsOnLeftHand(t.anchor)) return Resolve(t.anchor, f, out p);
            if (!TryGet(t.anchor, out p)) return false;
            p += t.anchorOffset;
            return true;
        }

        /// <summary>Start point of the left hand's contact point; "mirror" = the right start mirrored across the mid-line.</summary>
        public static bool LeftTarget(SignTemplate t, out Vector3 p)
        {
            if (t.leftAnchor == "mirror")
            {
                if (!TryGet(t.anchor, out p)) return false;
                p = MirrorX(p + t.anchorOffset);
                return true;
            }
            if (!TryGet(t.leftAnchor, out p)) return false;
            p += t.leftOffset;
            return true;
        }

        /// <summary>The left hand has a body location of its own to be judged against.</summary>
        public static bool HasLeftTarget(SignTemplate t) =>
            t.TwoHanded && !string.IsNullOrEmpty(t.leftAnchor) && !IsOnLeftHand(t.leftAnchor);

        /// <summary>The part of the hand that touches the anchor.</summary>
        public static Vector3 Contact(in HandSample h, string contact)
        {
            switch (contact)
            {
                case "thumb": return h.ThumbTip;
                case "index": return h.IndexTip;
                case "middle": return h.MiddleTip;
                case "fingers": return (h.IndexTip + h.MiddleTip) * 0.5f;
                case "knuckle": return Vector3.Lerp(h.Palm, h.IndexTip, 0.6f);
                default: return h.Palm;
            }
        }
    }
}
