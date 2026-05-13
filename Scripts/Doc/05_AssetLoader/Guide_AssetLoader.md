# AssetLoader 第一版学习记录

这个文档记录当前 `AssetLoader` 第一版的设计重点，方便后面继续扩展资源管理器。

对应代码：

- `Assets/_LearningWorkspace/Scripts/Core/Assets/AssetLoader.cs`
- `Assets/_LearningWorkspace/Scripts/Core/Assets/AssetHandle.cs`

---

## 1. 当前第一版做了什么

`AssetLoader` 当前负责：

- 作为框架模块继承 `ModuleBase`
- 异步加载任意资源
- 异步加载 Prefab
- 使用 `AssetHandle<T>` 包装加载结果
- 支持加载完成 callback
- 缓存已经加载过的资源
- 使用 `Logger` 输出加载结果

当前核心方法：

```csharp
LoadAsset<T>(string assetName, Action<AssetHandle<T>> callback = null)
LoadPrefab(string prefabName, Action<AssetHandle<GameObject>> callback = null)
```

---

## 2. 为什么缓存用 Dictionary<string, object>

`AssetHandle<GameObject>`、`AssetHandle<Sprite>`、`AssetHandle<AudioClip>` 在 C# 里是不同类型。

为了让一个缓存字典能放下不同类型的 `AssetHandle<T>`，第一版先用：

```csharp
Dictionary<string, object>
```

代价是：取出来时要判断类型。

```csharp
cachedAsset is AssetHandle<T> cachedHandle
```

这表示：

> 缓存里找到了东西，并且它正好是当前请求的 `AssetHandle<T>` 类型。

---

## 3. 为什么缓存 key 要带类型

只用资源名当 key 会有风险。

例如：

```text
Icon + Sprite
Icon + GameObject
```

它们名字一样，但资源类型不同。

所以当前使用：

```csharp
GetCacheKey<T>(assetName)
```

让缓存 key 同时包含：

- 资源类型
- 资源名

这样同名不同类型的资源不会互相覆盖。

---

## 4. callback 和返回值的关系

当前方法同时支持两种拿结果的方式。

第一种：通过返回值拿。

```csharp
AssetHandle<GameObject> handle = await loader.LoadPrefab("Player");
```

第二种：通过 callback 拿。

```csharp
await loader.LoadPrefab("Player", handle =>
{
    // 使用 handle
});
```

第一版保留 callback 是为了理解原项目 `GameAssetLoader` 的写法。

以后如果觉得重复，也可以只保留返回值，让调用处自己 `await`。

---

## 5. default 在这里是什么意思

当前代码里：

```csharp
T asset = handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : default;
```

意思是：

- 加载成功：使用 `handle.Result`
- 加载失败：使用 `default`

对于 `GameObject`、`Sprite`、`AudioClip` 这类引用类型，`default` 基本就是 `null`。

所以失败时创建出来的 `AssetHandle<T>` 会是无效句柄：

```csharp
IsValid == false
```

---

## 6. 当前暂时不做什么

第一版先不要继续膨胀。

暂时不做：

- 场景加载
- 实例化 Prefab
- Addressables 释放
- 引用计数
- 加载进度
- 失败重试
- 批量预加载

下一步建议先写一个测试脚本，确认：

- 第一次加载会走 Addressables
- 第二次加载同一个资源会命中缓存
- callback 能收到 `AssetHandle<T>`
- 加载失败时 `IsValid` 为 `false`

