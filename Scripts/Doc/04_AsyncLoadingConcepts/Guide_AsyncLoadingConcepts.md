# 异步加载概念学习指引

这个文档不是让你马上完整掌握异步编程，而是为了后面能看懂和复刻资源管理器。

本节目标：理解 `async`、`await`、`UniTask<T>`、Addressables 异步加载的大概关系。

---

## 1. 为什么资源加载要异步

资源加载可能比较慢。

例如：

- 加载 Prefab
- 加载 UI 面板
- 加载场景
- 加载音频
- 加载配置资源

如果加载时直接卡住主线程，游戏画面可能会停顿。

异步加载的目标是：

> 发起加载后，等待资源准备好，再继续执行后面的逻辑。

---

## 2. async 是什么

`async` 表示这个方法内部可以使用 `await`。

例如原项目里：

```csharp
public async UniTask<GameObject> LoadPrefab(string prefabName)
```

可以先读成：

> 这是一个异步方法，最后会返回一个 `GameObject`。

注意：

- `async` 不是“自动开新线程”。
- `async` 只是允许方法中途等待一个异步任务。
- 方法执行到 `await` 时，会等任务完成再继续往下走。

---

## 3. await 是什么

`await` 可以先理解成：

> 等这个异步操作完成，然后再继续执行下面的代码。

原项目里有：

```csharp
AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(prefabName);
await handle.ToUniTask();
loadedAsset.Add(prefabName, handle.Result);
```

阅读顺序是：

1. 发起 Addressables 加载。
2. 等加载完成。
3. 从 `handle.Result` 里取结果。
4. 把结果放进缓存字典。

---

## 4. UniTask<T> 是什么

`UniTask<T>` 可以先理解成 Unity 项目里常用的异步返回类型。

它表示：

> 现在还没有结果，但以后会得到一个 T。

例如：

```csharp
UniTask<GameObject>
```

表示：

> 这个异步任务完成后，会得到一个 `GameObject`。

普通 C# 里经常见 `Task<T>`，Unity 项目里常用 `UniTask<T>`，是因为它更适合 Unity 的运行环境。

第一版你不需要深入性能差异，只要会读懂它的意思。

---

## 5. Addressables.LoadAssetAsync 是什么

Addressables 是 Unity 的资源加载系统。

原项目里：

```csharp
Addressables.LoadAssetAsync<GameObject>(prefabName)
```

表示：

> 用 `prefabName` 这个 key 异步加载一个 `GameObject` 资源。

它返回的不是直接的 `GameObject`，而是：

```csharp
AsyncOperationHandle<GameObject>
```

这个 handle 里以后才会有结果。

加载完成后，可以从：

```csharp
handle.Result
```

拿到真正的资源对象。

---

## 6. 先读懂原项目的 LoadPrefab

当前只需要重点读：

- `Assets/Scripts/Manager/GameAssetLoader.cs`
- `LoadPrefab`
- `LoadAsset<T>`

先不要深入 `LoadScene`。

读 `LoadPrefab` 时，按这个顺序看：

1. 字典里有没有这个 prefab。
2. 没有就调用 Addressables 异步加载。
3. `await` 等加载完成。
4. 把结果放进缓存字典。
5. 调用成功回调。
6. 返回加载结果。
7. 如果字典里已经有，就直接返回缓存。

这其实就是资源管理器第一版的核心流程。

---

## 7. 回调和返回值

原项目里同时用了两种方式通知外部：

```csharp
Action<GameObject> LoadSuccessCallBack
return handle.Result
```

也就是说，加载成功后：

- 可以通过回调拿到资源。
- 也可以通过 `await LoadPrefab(...)` 的返回值拿到资源。

第一版自己写学习代码时，可以先只保留返回值。

回调可以以后再加。

---

## 8. 和 AssetHandle<T> 的关系

上一节写的 `AssetHandle<T>` 可以用来包装加载结果。

比如以后资源管理器可以从：

```csharp
UniTask<GameObject>
```

升级成：

```csharp
UniTask<AssetHandle<GameObject>>
```

这样调用者拿到的不只是资源对象，还能拿到：

- `Key`
- `Asset`
- `IsValid`

这就是为什么前面要先学资源句柄。

---

## 9. 本节暂时不要深入

先不要研究这些：

- 多线程
- PlayerLoop
- CancellationToken
- async enumerable
- 并发加载队列
- 加载失败重试
- Addressables Release 细节

现在的目标只是：看懂原项目资源加载代码，并为自己写 `LearningAssetManager` 做准备。

---

## 10. 完成标准

当你能自己解释下面这句话时，本节就算完成：

> `LoadAssetAsync` 发起加载，`await` 等它完成，`UniTask<T>` 表示异步结果，`handle.Result` 才是真正加载到的资源。

完成后可以回到 `GameAssetLoader.LoadPrefab`，用自己的话写出每一行在做什么。

