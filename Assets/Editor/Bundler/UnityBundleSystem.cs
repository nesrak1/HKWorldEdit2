using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace Assets.Bundler
{
    //handles unity's bundling system for non-scene assets only
    public class UnityBundleSystem
    {
        public static readonly string TEMP_BUN_PATH = Path.Combine("Assets", "AbTemp");

        public static string BundleFromAssets(List<string> assetPaths, string outBunName)
        {
            string outPath = Path.Combine(TEMP_BUN_PATH, outBunName);

            AssetBundleBuild[] buildMap = new AssetBundleBuild[]
            {
                new AssetBundleBuild()
                {
                    assetBundleName = Path.GetFileName(outBunName),
                    assetNames = assetPaths.ToArray()
                }
            };

            foreach (string assetPath in assetPaths)
            {
                BuildPipeline.BuildAssetBundles(outPath, buildMap,
                    BuildAssetBundleOptions.UncompressedAssetBundle, BuildTarget.StandaloneWindows64);
            }

            return outPath;
        }
    }
}
