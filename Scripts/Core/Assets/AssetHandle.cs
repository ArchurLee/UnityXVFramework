namespace Core
{
    public  class AssetHandle<T>
    {
        public  string Key{get;}
        public T Asset { get; }
        public bool IsValid => Asset != null;
        public AssetHandle(string key, T asset)
        {
            Key = key;
            Asset = asset;
        }
    }
}