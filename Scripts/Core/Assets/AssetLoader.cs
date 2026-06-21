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
        private class AssetEntry
        {
            public AsyncOperationHandle Handle;
            public int RefCount;
        }

        private readonly Dictionary<string, AsyncOperationHandle> loadingAssets = new Dictionary<string, AsyncOperationHandle>();
        private readonly Dictionary<string, AssetEntry> loadedAssets = new Dictionary<string, AssetEntry>();

        public override string ModuleName => "AssetLoader";

        protected override void OnInitialize()
        {
        }

        protected override void OnShutdown()
        {
            foreach (AssetEntry entry in loadedAssets.Values)
            {
                Addressables.Release(entry.Handle);
            }

            loadingAssets.Clear();
            loadedAssets.Clear();
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
            if (loadedAssets.TryGetValue(cacheKey, out AssetEntry loadedEntry))
            {
                loadedEntry.RefCount++;
                AssetHandle<T> cachedHandle = WrapHandle<T>(assetName, loadedEntry.Handle);
                Logger.Log(ModuleName, $"Cache hit: {assetName}, ref count: {loadedEntry.RefCount}");
                callback?.Invoke(cachedHandle);
                return cachedHandle;
            }

            if (loadingAssets.TryGetValue(cacheKey, out AsyncOperationHandle loadingHandle))
            {
                await loadingHandle.ToUniTask();
                if (loadedAssets.TryGetValue(cacheKey, out loadedEntry))
                {
                    loadedEntry.RefCount++;
                    AssetHandle<T> sharedHandle = WrapHandle<T>(assetName, loadingHandle);
                    Logger.Log(ModuleName, $"Shared loading completed: {assetName}, ref count: {loadedEntry.RefCount}");
                    callback?.Invoke(sharedHandle);
                    return sharedHandle;
                }

                AssetHandle<T> invalidSharedHandle = new AssetHandle<T>(assetName, default, default);
                callback?.Invoke(invalidSharedHandle);
                return invalidSharedHandle;
            }

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(assetName);
            loadingAssets[cacheKey] = handle;

            await handle.ToUniTask();
            loadingAssets.Remove(cacheKey);

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Logger.Error(ModuleName, $"Failed to load asset: {assetName}");
                Addressables.Release(handle);

                AssetHandle<T> failedHandle = new AssetHandle<T>(assetName, default, default);
                callback?.Invoke(failedHandle);
                return failedHandle;
            }

            loadedAssets[cacheKey] = new AssetEntry
            {
                Handle = handle,
                RefCount = 1
            };

            AssetHandle<T> assetHandle = WrapHandle<T>(assetName, handle);
            Logger.Log(ModuleName, $"Loaded asset: {assetName}, ref count: 1");

            callback?.Invoke(assetHandle);
            return assetHandle;
        }

        /// <summary>
        /// 释放一次资源引用，引用数归零时交还给 Addressables。
        /// </summary>
        public void Release<T>(string assetName)
        {
            Release(GetCacheKey<T>(assetName), assetName);
        }

        /// <summary>
        /// 释放 GameObject Prefab 资源引用。
        /// </summary>
        public void ReleasePrefab(string prefabName)
        {
            Release<GameObject>(prefabName);
        }

        private AssetHandle<T> WrapHandle<T>(string assetName, AsyncOperationHandle handle)
        {
            if (handle.Result is T asset)
            {
                return new AssetHandle<T>(assetName, asset, handle);
            }

            Logger.Error(ModuleName, $"Asset type mismatch: {assetName}");
            return new AssetHandle<T>(assetName, default, handle);
        }

        private void Release(string cacheKey, string assetName)
        {
            if (!loadedAssets.TryGetValue(cacheKey, out AssetEntry entry))
            {
                Logger.Warning(ModuleName, $"Release ignored, asset not loaded: {assetName}");
                return;
            }

            entry.RefCount--;
            if (entry.RefCount > 0)
            {
                Logger.Log(ModuleName, $"Released asset reference: {assetName}, ref count: {entry.RefCount}");
                return;
            }

            Addressables.Release(entry.Handle);
            loadedAssets.Remove(cacheKey);
            Logger.Log(ModuleName, $"Released asset: {assetName}");
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
