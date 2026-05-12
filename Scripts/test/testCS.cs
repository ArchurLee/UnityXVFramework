using UnityEngine;
namespace Core{
public class testCS : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        var assetHandle = new AssetHandle<string>("testKey", "testAsset");
        Logger.Log("testCS", $"Asset Key: {assetHandle.Key}, Asset Value: {assetHandle.Asset}, IsValid: {assetHandle.IsValid}");
    }

    // Update is called once per frame
    void Update()
    {

    }
}
}
