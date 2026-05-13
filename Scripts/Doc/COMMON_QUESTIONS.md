# 常见问题记录

这个文档只记录学习框架时反复容易混淆的关键点。

---

## 接口和基类

- `IModule` 是规则：规定模块必须提供哪些能力。
- `ModuleBase` 是默认流程：把初始化、关闭、状态维护这些重复逻辑收起来。
- 实现接口不用 `override`，继承基类改写虚方法才用 `override`。

---

## public 和 private

- 框架基础类型一般用 `public`，例如 `IModule`、`ModuleBase`，方便其他模块访问。
- 类内部状态要收紧，例如 `public bool IsInitialized { get; private set; }`。
- 接口成员默认是公开规则；普通 class 成员不写修饰符默认是 `private`。

---

## 属性实现

接口里的：

```csharp
string ModuleName { get; }
```

只表示外部能读取 `ModuleName`。

实现时可以写成：

```csharp
public string ModuleName => GetType().Name;
```

也可以写成：

```csharp
public string ModuleName { get; private set; }
```

---

## 命名空间

C# 区分大小写。

`Core` 和 `core` 是两个不同的命名空间。

学习框架里统一使用 `Core`。

---

## AssetHandle<T>

- `AssetHandle<T>` 是资源加载结果的包装，不是资源管理器。
- 第一版只需要 `Key`、`Asset`、`IsValid`。
- 如果加 `where T : UnityEngine.Object`，就不能用 `AssetHandle<string>` 测试。

---

## AssetLoader 缓存

- 缓存不同类型的 `AssetHandle<T>` 时，可以先用 `Dictionary<string, object>`。
- 缓存 key 不要只用资源名，最好组合资源类型和资源名。
- `default` 在资源加载失败时通常表示 `null`，会让 `AssetHandle<T>.IsValid` 变成 `false`。
