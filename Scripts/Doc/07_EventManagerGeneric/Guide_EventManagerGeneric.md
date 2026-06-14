# EventManager 第二版学习指引：带参数事件

这一阶段在第一版无参数事件的基础上，给 `EventManager` 增加“带一个参数”的事件。

第一版解决的是：

```csharp
BroadcastEvent("PlayerDead");
```

第二版要解决的是：

```csharp
BroadcastEvent("GoldChanged", 100);
BroadcastEvent("CardSelected", cardId);
```

也就是：广播事件时，把一份数据一起传给监听者。

---

## 1. 这一阶段先做什么

给 `EventManager` 增加三组泛型方法：

```csharp
RegisterEvent<T>(string eventName, Action<T> callback)
UnregisterEvent<T>(string eventName, Action<T> callback)
BroadcastEvent<T>(string eventName, T arg)
```

暂时只支持一个参数。

暂时不要做：

- 两个或更多参数
- 返回值
- 异步事件
- 事件优先级
- 事件队列

---

## 2. Action<T> 是什么

第一版用的是：

```csharp
Action
```

它表示：

```text
没有参数、没有返回值的方法
```

第二版要用：

```csharp
Action<T>
```

它表示：

```text
有一个 T 类型参数、没有返回值的方法
```

例如：

```csharp
Action<int> onGoldChanged;
```

它可以保存这种方法：

```csharp
void RefreshGoldText(int gold)
{
}
```

---

## 3. 为什么不能直接放进 Dictionary<string, Action>

无参数事件可以用：

```csharp
Dictionary<string, Action>
```

但是带参数事件有很多类型：

```csharp
Action<int>
Action<string>
Action<GameObject>
Action<CardData>
```

这些在 C# 里都是不同类型，不能直接放进 `Dictionary<string, Action>`。

所以第二版可以先用：

```csharp
Dictionary<string, object>
```

含义是：

- key：事件名
- value：某种 `Action<T>`

代价是：取出来时需要判断类型。

---

## 4. 建议的数据结构

保留第一版的无参数事件字典：

```csharp
private readonly Dictionary<string, Action> eventDic = new Dictionary<string, Action>();
```

再新增一个带参数事件字典：

```csharp
private readonly Dictionary<string, object> eventArgDic = new Dictionary<string, object>();
```

这样两类事件分开管理：

```text
eventDic     -> 无参数事件
eventArgDic  -> 带一个参数的事件
```

---

## 5. RegisterEvent<T> 怎么想

注册带参数事件时，也分两种情况。

事件名不存在：

```csharp
eventArgDic[eventName] = callback;
```

事件名已经存在：

```csharp
if (eventArgDic[eventName] is Action<T> action)
{
    action += callback;
    eventArgDic[eventName] = action;
}
```

注意：这里 `action += callback` 之后，要重新放回字典。

因为 `Action<T>` 是委托，`+=` 会生成一个新的组合委托。

---

## 6. 类型不匹配是什么意思

这是泛型事件最容易踩坑的地方。

例如你先注册：

```csharp
RegisterEvent<int>("GoldChanged", OnGoldChanged);
```

后面又写：

```csharp
RegisterEvent<string>("GoldChanged", OnGoldChangedText);
```

事件名一样，但参数类型不一样。

这时 `eventArgDic["GoldChanged"]` 里面原本是：

```csharp
Action<int>
```

你却想把它当成：

```csharp
Action<string>
```

这是不安全的。

第一版处理方式可以很简单：

```csharp
Logger.Error(ModuleName, $"Event type mismatch: {eventName}");
return;
```

---

## 7. BroadcastEvent<T> 怎么想

广播带参数事件时，要先取出对象，再判断它是不是当前类型的 `Action<T>`：

```csharp
if (eventArgDic.TryGetValue(eventName, out object callbackObject))
{
    if (callbackObject is Action<T> callback)
    {
        callback.Invoke(arg);
    }
}
```

如果类型不匹配，也先打日志即可。

---

## 8. UnregisterEvent<T> 怎么想

取消注册时：

1. 先找事件名。
2. 再确认类型是 `Action<T>`。
3. 执行 `-= callback`。
4. 如果结果是 `null`，从字典删除。
5. 如果不是 `null`，重新写回字典。

大概结构：

```csharp
if (eventArgDic.TryGetValue(eventName, out object callbackObject))
{
    if (callbackObject is Action<T> action)
    {
        action -= callback;

        if (action == null)
        {
            eventArgDic.Remove(eventName);
        }
        else
        {
            eventArgDic[eventName] = action;
        }
    }
}
```

---

## 9. Shutdown 要注意什么

关闭模块时，两个字典都要清空：

```csharp
eventDic.Clear();
eventArgDic.Clear();
```

这表示事件管理器不再持有任何事件回调。

---

## 10. 测试思路

写完第二版后，可以测试一个 `int` 参数事件：

```csharp
RegisterEvent<int>("GoldChanged", OnGoldChanged);
BroadcastEvent("GoldChanged", 100);
UnregisterEvent<int>("GoldChanged", OnGoldChanged);
BroadcastEvent("GoldChanged", 200);
```

预期：

- 第一次广播会调用 `OnGoldChanged(100)`。
- 取消注册后，第二次广播不会再调用。

也可以故意测试类型不匹配：

```csharp
RegisterEvent<int>("GoldChanged", OnGoldChanged);
BroadcastEvent("GoldChanged", "100");
```

这时应该看到类型不匹配日志。

---

## 11. 下一阶段

带参数事件跑通后，事件管理器就可以先暂停。

下一阶段再进入第三个模块：

```text
UI 管理器
```

UI 管理器会依赖前面写过的：

- `ModuleBase`
- `Logger`
- `AssetLoader`
- `EventManager`
