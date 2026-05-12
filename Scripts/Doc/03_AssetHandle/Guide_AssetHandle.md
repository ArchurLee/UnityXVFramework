# 资源句柄学习指引

这个文档不是答案代码，而是引导你自己写 `LearningAssetHandle<T>`。

本节目标：理解为什么资源管理器不应该只返回一个资源对象，而是返回一个“带信息的加载结果”。

---

## 1. 资源句柄是什么

资源句柄可以先理解成：

> 一张资源小票。

它不仅告诉你“资源对象是什么”，还记录：

- 这个资源是用哪个 key 加载的
- 资源当前是否有效
- 以后释放资源时应该根据什么信息找回去

比如你加载一个 Prefab：

```text
key: PlayerPrefab
asset: 真实的 GameObject 资源
isValid: true
```

这三个信息放在一起，就比单独返回一个 `GameObject` 更容易管理。

---

## 2. 为什么不直接返回 GameObject

如果资源管理器只返回：

```csharp
GameObject prefab;
```

短期看很简单，但后面会遇到问题：

- 不知道这个资源是用哪个 key 加载的
- 不方便判断加载是否成功
- 不方便统一释放
- 不方便做缓存
- 不方便做引用计数
- 不方便调试资源来自哪里

所以第一版资源管理器可以把结果包装成：

```text
LearningAssetHandle<T>
```

它是资源管理器和外部调用者之间的一个结果对象。

---

## 3. 为什么要写成泛型 T

Unity 资源不只有一种类型。

你以后可能加载：

- `GameObject`
- `Sprite`
- `AudioClip`
- `TextAsset`
- `ScriptableObject`
- `Material`

如果每种资源都写一个句柄，会重复很多。

泛型的意思是：这个句柄可以包装不同类型的资源。

例如：

```csharp
LearningAssetHandle<GameObject>
LearningAssetHandle<Sprite>
LearningAssetHandle<AudioClip>
```

它们结构一样，只是里面的资源类型不同。

---

## 4. 第一版建议包含哪些数据

建议创建文件：

- `Assets/_LearningWorkspace/Scripts/Core/Assets/LearningAssetHandle.cs`

第一版先考虑这几个成员：

- `Key`：资源加载用的 key
- `Asset`：加载出来的资源对象
- `IsValid`：这个句柄是否有效

你可以思考：

- `Key` 应不应该允许外部修改？
- `Asset` 应不应该允许外部修改？
- `IsValid` 是字段，还是根据 `Asset != null` 算出来的属性？

第一版推荐思路：

- `Key` 创建后不轻易变
- `Asset` 创建后不轻易变
- `IsValid` 可以先根据资源是否为空判断

---

## 5. IsValid 怎么理解

`IsValid` 不是“资源一定永远可用”。

第一版里它可以简单表示：

```text
这个句柄里有没有拿到资源对象
```

例如：

```text
Asset 不为空 -> IsValid 为 true
Asset 为空 -> IsValid 为 false
```

后面接入 Addressables 后，`IsValid` 可能会变复杂：

- 加载句柄是否还有效
- 资源是否已经释放
- 引用计数是否大于 0

但现在先不要复杂化。

---

## 6. 构造函数要想清楚

资源句柄一般在资源管理器加载完成后创建。

你可以考虑构造函数需要什么参数：

```text
key
asset
```

这样创建句柄时就能把必要信息放进去。

思考：

- 如果 key 是空字符串，应该允许吗？
- 如果 asset 是 null，句柄是否仍然可以创建？
- 加载失败时，是返回 null，还是返回一个无效句柄？

第一版可以简单一点：

- 构造函数接收 key 和 asset
- `IsValid` 根据 asset 是否为空判断
- 加载失败时也可以返回一个 `Asset` 为空的句柄，方便外部检查

---

## 7. 它和资源管理器的关系

资源句柄本身不负责加载资源。

它只负责保存加载结果。

职责划分可以这样理解：

- `LearningAssetManager`：负责加载、缓存、释放
- `LearningAssetHandle<T>`：负责保存某一次加载的结果
- 调用者：拿到句柄后检查 `IsValid`，再使用 `Asset`

不要让句柄第一版承担太多职责。

---

## 8. 本节暂时不要加的功能

第一版先保持轻。

暂时不要做：

- 引用计数
- 自动释放
- Addressables 原始句柄保存
- 加载进度
- 异步状态
- 资源分组
- 生命周期回调

这些会在资源管理器里慢慢出现。

---

## 9. 完成标准

当你能自己解释下面这句话时，本节就算完成：

> 资源句柄不是资源本身，而是资源加载结果的包装；它让资源管理器以后更容易缓存、释放和调试资源。

完成后你可以写一个小测试：

```csharp
// 伪代码思路，不是要求照抄
var handle = new LearningAssetHandle<GameObject>("PlayerPrefab", prefab);
Debug.Log(handle.Key);
Debug.Log(handle.IsValid);
```

观察你是否能通过句柄知道：

- 资源从哪个 key 来
- 资源对象是什么
- 当前结果是否有效

