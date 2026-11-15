using UnityEngine;

namespace SignSight.Explore
{
    /// <summary>
    /// Where Explore's object detector gets its pictures (v3.3 Task B). YoloDetector only ever sees Frame, so it cannot
    /// tell a mock image / video / webcam from a Quest passthrough camera — that is what makes the mock a valid test of
    /// the detection logic (not of the Quest camera itself).
    /// </summary>
    public interface ICameraFrameProvider
    {
        /// <summary>The latest frame (top row = top of the picture), or null.</summary>
        Texture2D Frame { get; }

        bool IsAvailable { get; }

        /// <summary>Short description for logs and the report (e.g. "image explore-mock.png").</summary>
        string Source { get; }
    }
}
