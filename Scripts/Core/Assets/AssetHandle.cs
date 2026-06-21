namespace Core
{
    using UnityEngine.ResourceManagement.AsyncOperations;

    public class AssetHandle<T>
    {
        public string Key { get; }
        public T Asset { get; }
        public bool IsValid => Asset != null;
        internal AsyncOperationHandle Handle { get; }

        internal AssetHandle(string key, T asset, AsyncOperationHandle handle)
        {
            Key = key;
            Asset = asset;
            Handle = handle;
        }
    }
}
