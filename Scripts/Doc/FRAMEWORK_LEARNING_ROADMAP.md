# Unity 框架学习路线：模块驱动复刻版

这份路线不是 Unity 入门教程，而是用“我要写一个框架模块，需要哪些基础件？”的方式学习。

核心思路：

- 不从大而全的 `GameManager` 开始。
- 先选一个模块，例如资源管理器。
- 反推这个模块需要哪些基础结构。
- 先写基础结构，再写管理器本体。
- 每个模块都做一个能单独运行的小版本。

你的练习代码统一放在：

`Assets/_LearningWorkspace`

原项目代码只作为参考，主要在：

`Assets/Scripts`

---

## 总学习顺序

推荐顺序：

1. 框架基础件
2. 资源管理器
3. 事件管理器
4. UI 管理器
5. 音频管理器
6. 场景管理器
7. 存档管理器
8. 输入管理器
9. 最小 `LearningGameManager` 入口
10. 再回头分析原项目完整 `GameManager`

---

# 第一部分：框架基础件

这些不是“Unity 入门基础”，而是写框架时经常要用到的结构。

---

## 基础件 1：框架模块基类

学习指引：

- `Assets/_LearningWorkspace/Scripts/Doc/01_FrameworkModuleBase/Guide_FrameworkModuleBase.md`

### 为什么需要

如果每个管理器都有自己的初始化、销毁、清理逻辑，后面会很乱。

所以可以先设计一个统一的模块基类或接口。

### 要解决的问题

每个 Manager 至少可能需要：

- 初始化
- 更新
- 释放
- 是否已经初始化
- 模块名称



## 基础件 2：日志工具

学习指引：

- `Assets/_LearningWorkspace/Scripts/Doc/02_LearningLogger/Guide_LearningLogger.md`

### 为什么需要

写框架时需要大量观察初始化、加载、释放过程。

统一日志比到处 `Debug.Log` 更清晰。

### 可以先写什么

在 `_LearningWorkspace/Scripts/Utils` 下创建：

- `LearningLogger`

### 功能

- `Log(string message)`
- `Warning(string message)`
- `Error(string message)`

### 输出格式

例如：

`[LearningFramework][Asset] 加载资源成功: xxx`

### 学习目标

你要理解：

- 工具类可以先做成 `static`。
- 统一日志格式方便排查框架问题。

---

## 基础件 3：资源句柄

学习指引：

- `Assets/_LearningWorkspace/Scripts/Doc/03_AssetHandle/Guide_AssetHandle.md`

### 为什么需要

资源管理器不能只返回一个 `GameObject`，否则以后不好释放、缓存、统计引用。

可以先设计一个简化的资源句柄。

### 可以先写什么

在 `_LearningWorkspace/Scripts/Core/Assets` 下创建：

- `LearningAssetHandle<T>`

### 第一版字段

- `string Key`
- `T Asset`
- `bool IsValid`

### 学习目标

你要理解：

- 资源加载结果最好被包装起来。
- 句柄可以保存资源名、资源对象、有效状态。
- 后面可以扩展引用计数、释放逻辑。

---

## 基础件 4：异步加载概念

学习指引：

- `Assets/_LearningWorkspace/Scripts/Doc/04_AsyncLoadingConcepts/Guide_AsyncLoadingConcepts.md`

### 为什么需要

资源、场景、UI 面板都可能是异步加载。

如果资源管理器一开始只考虑同步加载，后面扩展会很难。

### 当前项目参考

- `Assets/Scripts/Manager/GameAssetLoader.cs`

### 要理解

- `async` 表示方法里可以等待异步任务。
- `await` 表示等任务完成再继续。
- `UniTask<T>` 是 Unity 中常用的异步返回类型。
- Addressables 的加载结果不是立刻出来的。

### 不需要一开始掌握

暂时不用深入：

- PlayerLoop
- CancellationToken
- async enumerable
- 复杂并发控制

---

# 第二部分：第一个模块：资源管理器

学习指引：

- `Assets/_LearningWorkspace/Scripts/Doc/05_AssetLoader/Guide_AssetLoader.md`

这是推荐你第一个复刻的管理器。

原项目参考文件：

- `Assets/Scripts/Manager/GameAssetLoader.cs`
- `Assets/Scripts/Manager/GameManager.cs` 中资源加载部分

---

## 资源管理器目标

第一版不要追求完整。

先实现一个 `LearningAssetManager`，负责：

- 加载资源
- 加载 Prefab
- 实例化 Prefab
- 缓存已经加载的资源
- 提供释放接口的占位

---

## 写资源管理器需要哪些东西

### 1. 模块基类

资源管理器本身应该是一个框架模块。

所以它可以继承：

- `LearningModuleBase`

这样它就拥有：

- `Initialize()`
- `Shutdown()`
- `IsInitialized`
- `ModuleName`

---

### 2. 日志工具

资源加载失败、成功、缓存命中，都需要日志。

所以需要：

- `LearningLogger`

---

### 3. 缓存容器

资源管理器需要记录已经加载过的资源。

第一版可以用：

- `Dictionary<string, UnityEngine.Object>`

后面可以升级成：

- `Dictionary<string, LearningAssetHandle<T>>`
- 引用计数
- 分组释放

---

### 4. 资源加载 API

第一版有两种选择。

#### 选择 A：使用 Addressables

优点：

- 和原项目一致。
- 更接近实际项目框架。

需要依赖：

- `UnityEngine.AddressableAssets`
- `Cysharp.Threading.Tasks`

#### 选择 B：使用 Resources

优点：

- 更简单。
- 不需要配置 Addressables。

缺点：

- 不适合大型项目。
- 和原项目不完全一致。

### 推荐

既然原项目已经用了 Addressables，建议第一版就使用 Addressables，但只写最小功能。

---

## 资源管理器第一版设计

建议创建目录：

`_LearningWorkspace/Scripts/Core/Assets`

建议创建文件：

- `LearningAssetManager.cs`
- `LearningAssetHandle.cs`

### 第一版方法

`LearningAssetManager` 建议先有这些方法：

- `Initialize()`
- `Shutdown()`
- `LoadAssetAsync<T>(string key)`
- `LoadPrefabAsync(string key)`
- `InstantiatePrefabAsync(string key)`
- `Release(string key)`
- `ClearCache()`

### 第一版不做

先不要做：

- 引用计数
- 资源分组
- 自动释放依赖
- 资源预加载队列
- 加载进度 UI
- 失败重试

这些以后再加。

---

## 资源管理器学习步骤

### Step 1：读原项目最小代码

只读：

- `GameAssetLoader.LoadAsset<T>()`
- `GameAssetLoader.LoadPrefab()`

暂时不要看加载场景。

你要回答：

- 它用什么 API 加载资源？
- 它怎么缓存 Prefab？
- 它返回的是什么？
- 它失败时有没有处理？

---

### Step 2：先写模块基类

创建：

- `ILearningModule`
- `LearningModuleBase`

目标：

- 后续所有 Manager 都可以继承。

---

### Step 3：写日志工具

创建：

- `LearningLogger`

目标：

- 后续所有 Manager 都用统一日志。

---

### Step 4：写资源句柄

创建：

- `LearningAssetHandle<T>`

目标：

- 包装资源加载结果。

---

### Step 5：写 `LearningAssetManager` 第一版

只实现：

- 加载任意资源
- 加载 Prefab
- 缓存 Prefab
- 实例化 Prefab

---

### Step 6：写测试脚本

创建：

- `LearningAssetManagerTester`

它负责：

- 初始化资源管理器
- 调用加载方法
- 打印结果

---

### Step 7：接入 `LearningGameManager`

等单独测试通过后，再把它挂到自己的框架入口里。

不要一开始就接入总入口。

---

# 第三部分：第二个模块：事件管理器

等资源管理器写完，再学事件管理器。

原项目参考：

- `Assets/Scripts/Manager/GameEventManager.cs`
- `Assets/Scripts/GameEvent`

---

## 写事件管理器需要哪些东西

### 1. 模块基类

事件管理器也是模块，继承：

- `LearningModuleBase`

### 2. 委托

需要理解：

- `Action`
- `Action<T>`
- 多播委托

### 3. 字典

需要用字典保存：

- 事件名
- 对应回调列表

---

## 事件管理器第一版方法

- `Register(string eventName, Action callback)`
- `Unregister(string eventName, Action callback)`
- `Broadcast(string eventName)`

第二版再加：

- `Register<T>(string eventName, Action<T> callback)`
- `Broadcast<T>(string eventName, T arg)`

---

# 第四部分：第三个模块：UI 管理器

等事件管理器写完，再学 UI 管理器。

原项目参考：

- `Assets/Scripts/Manager/GameUIManager.cs`
- `Assets/Scripts/GameUI/BasePanel.cs`

---

## 写 UI 管理器需要哪些东西

### 1. 面板基类

先写：

- `LearningBasePanel`

提供：

- `Show()`
- `Hide()`
- `IsShowing`

### 2. UI 管理器

再写：

- `LearningUIManager`

负责：

- 创建面板
- 缓存面板
- 显示面板
- 隐藏面板

### 3. 资源管理器

UI 管理器后面可以依赖资源管理器加载 Prefab。

所以 UI 管理器建议放在资源管理器之后学。

---

# 第五部分：什么时候看完整 GameManager

不要一开始看完整 `GameManager`。

建议你至少写完这些东西后再看：

- `LearningModuleBase`
- `LearningLogger`
- `LearningAssetManager`
- `LearningEventManager`
- `LearningBasePanel`
- `LearningUIManager`

到那时再看 `GameManager`，你会发现它其实就是：

- 创建模块
- 保存模块
- 暴露模块访问入口
- 调用模块生命周期
- 协调模块之间的顺序

---

# 当前推荐下一步

不要继续读完整路线。

现在只做第一组：

1. 写 `ILearningModule`
2. 写 `LearningModuleBase`
3. 写 `LearningLogger`
4. 写 `LearningAssetHandle<T>`
5. 再开始写 `LearningAssetManager`

完成这组之后，你就能真正开始复刻第一个框架模块：资源管理器。
