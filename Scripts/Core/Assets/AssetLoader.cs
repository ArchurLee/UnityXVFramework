using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Core
{
    public class AssetLoader : ModuleBase
    {
        private readonly Dictionary<string, object> loadedAssets = new Dictionary<string, object>();

        public override string ModuleName => "AssetLoader";

        protected override void OnInitialize()
        {
            Logger.Log(ModuleName, "Initialized.");
        }

        protected override void OnShutdown()
        {
            loadedAssets.Clear();
            Logger.Log(ModuleName, "Shutdown and cache cleared.");
        }
/// <summary>
/// 异步加载预制体，支持缓存和回调
/// </summary>
/// <param name="prefabName"></param>
/// <param name="callback"></param>
/// <returns></returns>
        public async UniTask<AssetHandle<GameObject>> LoadPrefab(string prefabName, Action<AssetHandle<GameObject>> callback = null)
        {
            return await LoadAsset<GameObject>(prefabName, callback);
        }
/// <summary>
/// 异步加载资源，支持缓存和回调
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="assetName">资源名称</param>
/// <param name="callback">回调函数</param>
/// <returns></returns>
        public async UniTask<AssetHandle<T>> LoadAsset<T>(string assetName, Action<AssetHandle<T>> callback = null)
        {
            string cacheKey = GetCacheKey<T>(assetName);
            // 检查缓存
            if (loadedAssets.TryGetValue(cacheKey, out object cachedAsset))
            {
                if (cachedAsset is AssetHandle<T> cachedHandle)
                {
                    Logger.Log(ModuleName, $"Cache hit: {assetName}");
                    callback?.Invoke(cachedHandle);
                    return cachedHandle;
                }
                // 缓存类型不匹配，记录错误并返回无效句柄
                Logger.Error(ModuleName, $"Cache type mismatch: {assetName}");
                AssetHandle<T> mismatchHandle = new AssetHandle<T>(assetName, default);
                callback?.Invoke(mismatchHandle);
                return mismatchHandle;
            }

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(assetName);
            await handle.ToUniTask();

            T asset = handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : default;
            AssetHandle<T> assetHandle = new AssetHandle<T>(assetName, asset);

            if (assetHandle.IsValid)
            {
                cacheKey = GetCacheKey<T>(assetName);
                loadedAssets.Add(cacheKey, assetHandle);
                Logger.Log(ModuleName, $"Loaded asset: {assetName}");
            }
            else
            {
                Logger.Error(ModuleName, $"Failed to load asset: {assetName}");
            }

            callback?.Invoke(assetHandle);
            return assetHandle;
        }

/// <summary>
/// 构造字典key
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="assetName"></param>
/// <returns></returns>
        private string GetCacheKey<T>(string assetName)
        {
            return $"{typeof(T).FullName}:{assetName}";
        }

    }
}
