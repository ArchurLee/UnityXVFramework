# 日志工具学习指引

这个文档不是答案代码，而是引导你自己写 `LearningLogger`。

本节目标：写一个简单统一的日志工具，让后面的资源管理器、事件管理器、UI 管理器都用同一种格式输出信息。

---

## 1. 为什么框架需要日志工具

写普通玩法代码时，随手用 `Debug.Log()` 问题不大。

但写框架时，你经常要观察这些事情：

- 某个模块有没有初始化
- 资源有没有加载成功
- UI 面板有没有打开
- 事件有没有注册
- 缓存有没有命中
- 释放逻辑有没有执行

如果到处直接写：

```csharp
Debug.Log("加载成功");
Debug.Log("初始化完成");
Debug.Log("打开面板");
```

后面控制台会很乱，你很难知道这些日志来自哪个模块。

所以要先封装一个统一日志工具。

---

## 2. 先确定日志格式

建议第一版日志格式固定成这样：

```text
[LearningFramework][模块名] 日志内容
```

例如：

```text
[LearningFramework][Asset] 加载资源成功: PlayerPrefab
[LearningFramework][UI] 打开面板: InventoryPanel
[LearningFramework][Event] 注册事件: OnPlayerDead
```

这样你在 Unity Console 里一眼就能看出：

- 这是学习框架输出的日志
- 日志来自哪个模块
- 具体发生了什么

---

## 3. 为什么第一版可以写成 static

日志工具通常不需要每次都 new 一个对象。

你希望在任何地方都能直接调用：

```csharp
LearningLogger.Log("Asset", "加载资源成功");
```

所以第一版可以做成 `static class`。

思考：

- 日志工具有没有自己的复杂状态？
- 是否需要每个模块各自创建一个 Logger？
- 如果只是统一调用 `Debug.Log`，是否适合做成静态工具类？

第一版答案通常是：适合。

---

## 4. 建议你先设计哪些方法

建议在这个文件里写：

- `Assets/_LearningWorkspace/Scripts/Utils/LearningLogger.cs`

第一版可以先有三个方法：

```csharp
Log(string moduleName, string message)
Warning(string moduleName, string message)
Error(string moduleName, string message)
```

分别对应 Unity 的：

- `Debug.Log`
- `Debug.LogWarning`
- `Debug.LogError`

注意：这里先传入 `moduleName`，不要急着和 `IModule` 绑定。

原因是日志工具应该足够简单，资源管理器、事件管理器，甚至普通测试脚本都能直接用。

---

## 5. 可以抽一个格式化方法

如果每个方法都自己拼字符串，会重复。

你可以思考是否需要一个私有方法：

```csharp
FormatMessage(string moduleName, string message)
```

它只负责把模块名和内容拼成统一格式。

学习重点：

- 公开方法负责给外部调用
- 私有方法负责内部复用
- 格式变化时，只改一个地方

---

## 6. 写代码前先回答这些问题

动手前先想清楚：

- `LearningLogger` 应该放在哪个目录？
- 它需要继承 `MonoBehaviour` 吗？
- 它需要实现 `IModule` 吗？
- 日志格式里的模块名由谁传进来？
- `Warning` 和 `Error` 只是颜色不同，还是语义也不同？

建议答案：

- 放在 `Scripts/Utils`
- 不需要继承 `MonoBehaviour`
- 不需要实现 `IModule`
- 模块名由调用者传入
- `Warning` 表示可恢复异常，`Error` 表示明显错误或失败

---

## 7. 本节暂时不要加的功能

第一版保持简单。

暂时不要做：

- 日志开关
- 文件日志
- 日志等级过滤
- 彩色富文本
- 时间戳
- 堆栈信息包装
- 上传远程日志

这些功能以后可以加，但现在会干扰你理解“统一封装”的目的。

---

## 8. 完成标准

当你能自己解释下面这句话时，本节就算完成：

> `LearningLogger` 不负责业务逻辑，只负责把所有框架日志变成统一格式，方便观察和排查。

完成后可以写一个临时测试脚本，调用：

```csharp
LearningLogger.Log("Test", "普通日志");
LearningLogger.Warning("Test", "警告日志");
LearningLogger.Error("Test", "错误日志");
```

然后观察 Unity Console 里的输出是否格式统一。

