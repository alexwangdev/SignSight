using System.IO;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEngine;

namespace SignSight.EditorTools
{
    /// <summary>
    /// Builds StreamingAssets/yolov9-t.sentis from Meta's yolov9onnx.onnx (Unity-PassthroughCameraApiSamples,
    /// MultiObjectDetection/SentisInference/Model, MIT per that repo's README). Same graph as Meta's
    /// SentisModelEditorConverter (box centres -> corners, max class score, class id) but WITHOUT Meta's UInt8 weight
    /// quantisation, which cut the scores on the mock frame (cup 0.70 -> 0.12). Editor only.
    /// </summary>
    public static class ExploreModelBuilder
    {
        public static string Build(string onnxAssetPath, string outPath, bool float16)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ModelAsset>(onnxAssetPath);
            var model = ModelLoader.Load(asset);
            var graph = new FunctionalGraph();
            var input = graph.AddInput(model, 0);
            var centersToCornersData = new[]
            {
                1, 0, 1, 0,
                0, 1, 0, 1,
                -0.5f, 0, 0.5f, 0,
                0, -0.5f, 0, 0.5f,
            };
            var centersToCorners = Functional.Constant(new TensorShape(4, 4), centersToCornersData);
            var modelOutput = Functional.Forward(model, input)[0];          // (1, 84, N)
            var boxCoords = modelOutput[0, ..4, ..].Transpose(0, 1);
            var allScores = modelOutput[0, 4.., ..].Transpose(0, 1);
            var scores = Functional.ReduceMax(allScores, 1);
            var classIDs = Functional.ArgMax(allScores, 1);
            var corners = Functional.MatMul(boxCoords, centersToCorners);
            var final = graph.Compile(corners, classIDs, scores);
            if (float16) ModelQuantizer.QuantizeWeights(QuantizationType.Float16, ref final);
            ModelWriter.Save(outPath, final);
            return $"{outPath} {new FileInfo(outPath).Length / 1024} KB (float16 {float16})";
        }
    }
}
