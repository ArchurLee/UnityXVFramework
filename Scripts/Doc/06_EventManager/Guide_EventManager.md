# EventManager 第一版学习指引

这一阶段开始复刻第二个框架模块：事件管理器。

事件管理器的目标不是“让代码变高级”，而是让不同模块之间不要互相直接调用。

例如：

```text
战斗模块发现玩家死亡
    ↓
广播 PlayerDead 事件
    ↓
UI 模块、音频模块、存档模块各自响应
```

这样战斗模块不需要知道 UI、音频、存档模块具体怎么写。

---

## 1. 当前阶段先做什么

第一版只做无参数事件。

建议创建目录：

```text
Assets/_LearningWorkspace/Scripts/Core/Events
```

建议创建文件：

```text
EventManager.cs
```

第一版方法：

```csharp
Register(string eventName, Action callback)
Unregister(string eventName, Action callback)
Broadcast(string eventName)
```

暂时不要做：

- 泛型事件
- 带参数事件
- 一次性事件
- 事件优先级
- 事件队列
- 异步事件

---

## 2. 需要先理解的 C# 基础

### Action 是什么

`Action` 可以理解成一个变量，但它保存的不是普通数据，而是一个“没有参数、没有返回值的方法”。

例如：

```csharp
Action callback = OnPlayerDead;
```

意思是：把 `OnPlayerDead` 这个方法保存到 `callback` 里。

---

### += 是什么

`+=` 表示追加一个回调。

```csharp
events[eventName] += callback;
```

同一个事件名可以注册多个方法。

广播事件时，这些方法会按注册顺序被调用。

---

### -= 是什么

`-=` 表示移除一个回调。

```csharp
events[eventName] -= callback;
```

这一步很重要。

如果对象已经销毁，但它的回调还留在事件管理器里，后面广播事件时就可能调用到已经不该存在的逻辑。

---

### ?.Invoke() 是什么

```csharp
callback?.Invoke();
```

意思是：

- 如果 `callback` 不是 `null`，就调用它。
- 如果 `callback` 是 `null`，就什么都不做。

这样可以避免空引用报错。

---

## 3. 第一版数据结构

事件管理器需要用字典保存事件。

推荐第一版使用：

```csharp
Dictionary<string, Action>
```

含义是：

- `string`：事件名
- `Action`：这个事件对应的所有回调

例如：

```text
"PlayerDead" -> OnShowGameOver + OnPlayDeadSound
"GoldChanged" -> OnRefreshGoldText
```

---

## 4. 第一版代码骨架

先写骨架，不急着一次写完所有细节：

```csharp
using System;
using System.Collections.Generic;

namespace Core
{
    public class EventManager : ModuleBase
    {
        private readonly Dictionary<string, Action> events = new Dictionary<string, Action>();

        public override string ModuleName => "EventManager";

        public void Register(string eventName, Action callback)
        {
        }

        public void Unregister(string eventName, Action callback)
        {
        }

        public void Broadcast(string eventName)
        {
        }
    }
}
```

---

## 5. Register 要做什么

注册事件时，需要考虑两种情况。

第一种：这个事件名还不存在。

```csharp
events.Add(eventName, callback);
```

第二种：这个事件名已经存在。

```csharp
events[eventName] += callback;
```

可以先用 `ContainsKey` 判断。

---

## 6. Unregister 要做什么

取消注册时，也要先判断事件名是否存在。

如果存在，就移除 callback：

```csharp
events[eventName] -= callback;
```

移除之后，如果这个事件已经没有任何回调了，可以把这个 key 从字典里删掉。

```csharp
if (events[eventName] == null)
{
    events.Remove(eventName);
}
```

---

## 7. Broadcast 要做什么

广播事件时：

1. 根据 `eventName` 找到对应的 `Action`。
2. 如果找到了，就调用它。
3. 如果没找到，可以先什么都不做，也可以打一条日志。

第一版建议用日志观察流程：

```csharp
Logger.Log(ModuleName, $"Broadcast: {eventName}");
```

---

## 8. 测试思路

写完 `EventManager` 后，可以先用一个临时 `MonoBehaviour` 测试。

测试内容：

- 注册一个事件。
- 广播这个事件，确认方法被调用。
- 取消注册这个事件。
- 再广播一次，确认方法不会被调用。

测试时可以用这样的事件名：

```text
TestEvent
```

---

## 9. 下一阶段

无参数事件跑通后，再考虑第二版：

```csharp
Register<T>(string eventName, Action<T> callback)
Broadcast<T>(string eventName, T arg)
```

带参数事件会引入一个新问题：

```text
同一个事件名下，参数类型必须匹配
```

所以先不要急着写泛型版。
