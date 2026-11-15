using System;
using UnityEngine;

namespace SignSight.Explore
{
    /// <summary>
    /// STUB — the Quest 3 / 3S passthrough camera as an ICameraFrameProvider (v3.3 Task B).
    /// TODO (real Quest — needs a headset, Horizon OS v74+, camera permission): wrap the Passthrough Camera API
    /// (PassthroughCameraAccess / WebCamTexture), copy frames at 5-10 fps, and replace the mock's 2D box -> panel mapping
    /// with the camera pose + intrinsics and a depth raycast (as Meta's MultiObjectDetection sample does).
    /// The API returns no frames in the Editor or the XR Simulator, so this class refuses to run there and is never
    /// called in Play Mode; Explore uses MockCameraProvider in the Editor and the virtual objects on a device.
    /// </summary>
    public class QuestCameraProvider : ICameraFrameProvider
    {
        public Texture2D Frame =>
            throw new NotSupportedException("QuestCameraProvider is a stub: TODO (real Quest — needs a headset, Horizon OS v74+, camera permission).");

        public bool IsAvailable => false;

        public string Source => "Quest passthrough camera (stub, not implemented)";

        public QuestCameraProvider()
        {
            if (Application.isEditor)
                throw new NotSupportedException("The Passthrough Camera API gives no frames in the Editor / XR Simulator; use MockCameraProvider.");
        }
    }
}
