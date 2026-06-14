# UIManager 第一版学习指引

这一阶段开始复刻第三个框架模块：UI 管理器。

UI 管理器的目标不是“画 UI”，而是统一管理 UI 面板的生命周期：

```text
加载面板
创建面板
缓存面板
显示面板
隐藏面板
查询面板状态
```

原项目参考：

- `Assets/Scripts/Manager/GameUIManager.cs`
- `Assets/Scripts/GameUI/Panel/BasePanel.cs`
- `Assets/Scripts/GameUI/MainCanvas.cs`

---

## 1. 这一阶段先做什么

第一版建议分两步：

1. 写 `BasePanel`
2. 写 `UIManager`

建议创建目录：

```text
Assets/_LearningWorkspace/Scripts/Core/UI
```

建议创建文件：

```text
BasePanel.cs
UIManager.cs
```

暂时不要做：

- UI 层级系统
- 弹窗栈
- 多 Canvas
- UI 动画队列
- UI 与输入阻塞
- 红点系统
- 复杂窗口参数传递

---

## 2. BasePanel 负责什么

`BasePanel` 是所有 UI 面板的基类。

它不负责加载资源，也不负责缓存。

它只负责一个面板自己的显示状态。

第一版可以先有：

```csharp
bool IsShowing { get; }
void Show()
void Hide()
```

如果想模仿原项目，可以让它继承 `MonoBehaviour`：

```csharp
public abstract class BasePanel : MonoBehaviour
```

这样每个具体面板都可以挂在 Prefab 上。

---

## 3. 为什么要有 BasePanel

如果没有 `BasePanel`，UIManager 只能拿到一个普通 `GameObject`。

普通 `GameObject` 不知道：

- 这个面板是否正在显示
- 显示时要不要刷新数据
- 隐藏时要不要播放动画
- 面板初始化逻辑放在哪里

有了 `BasePanel`，UIManager 就可以统一调用：

```csharp
panel.Show();
panel.Hide();
```

而不用关心具体面板内部怎么实现。

---

## 4. UIManager 第一版负责什么

`UIManager` 第一版建议继承：

```csharp
ModuleBase
```

它负责：

- 找到 UI 根节点
- 加载面板 Prefab
- 实例化面板
- 把面板挂到 UI 根节点下
- 缓存已经创建过的面板
- 显示面板
- 隐藏面板

---

## 5. 第一版需要的数据结构

面板缓存可以先用：

```csharp
Dictionary<string, BasePanel>
```

key 可以先用面板类型名：

```csharp
string panelName = typeof(T).Name;
```

例如：

```text
MainMenuPanel -> MainMenuPanel 实例
SettingPanel  -> SettingPanel 实例
```

---

## 6. UIManager 第一版方法

建议先写这些方法：

```csharp
GetPanel<T>() where T : BasePanel
ShowPanel<T>() where T : BasePanel
HidePanel<T>() where T : BasePanel
IsShow<T>() where T : BasePanel
```

其中：

- `GetPanel<T>()`：如果缓存有，就返回；如果没有，就加载并创建。
- `ShowPanel<T>()`：先获取面板，再调用 `Show()`。
- `HidePanel<T>()`：找到缓存里的面板，再调用 `Hide()`。
- `IsShow<T>()`：查询面板是否正在显示。

---

## 7. UI 根节点怎么处理

原项目里有：

```csharp
MainCanvas
```

它负责保存：

```csharp
Transform panelsParent;
```

也就是所有面板挂载的父节点。

学习版第一版可以先简单一点：

```csharp
private Transform uiRoot;
```

初始化时可以通过外部传入，或者先用：

```csharp
GameObject.Find("Canvas")
```

学习阶段可以先用 `Find`，但要知道：

```text
正式框架里不要到处 Find，最好由入口统一传入引用。
```

---

## 8. 面板加载依赖谁

UIManager 不应该自己直接写一套资源加载逻辑。

它应该复用前面写过的：

```csharp
AssetLoader
```

例如：

```text
UIManager
    -> AssetLoader.LoadPrefab(panelName)
    -> Instantiate
    -> GetComponent<T>()
    -> Cache
    -> Show
```

这样资源加载规则仍然集中在 `AssetLoader`。

---

## 9. ShowPanel 的流程

`ShowPanel<T>()` 可以这样理解：

```text
传入面板类型 T
    ↓
用 typeof(T).Name 得到面板名
    ↓
检查缓存
    ↓
没有缓存就加载 Prefab 并实例化
    ↓
拿到 T 组件
    ↓
调用 Show()
    ↓
返回面板
```

---

## 10. HidePanel 的流程

`HidePanel<T>()` 可以这样理解：

```text
传入面板类型 T
    ↓
用 typeof(T).Name 得到面板名
    ↓
检查缓存
    ↓
找到就调用 Hide()
    ↓
第一版可以只 SetActive(false)，不销毁
```

第一版建议先做缓存隐藏，不急着销毁。

销毁和释放资源以后再说。

---

## 11. 第一版不要急着做动画

原项目 `BasePanel` 使用了：

```csharp
CanvasGroup
```

来做淡入淡出。

学习版第一版可以先不用动画，只做：

```csharp
gameObject.SetActive(true);
gameObject.SetActive(false);
```

等 Show/Hide 流程跑通后，再加 `CanvasGroup`。

---

## 12. 测试思路

写完第一版后，可以创建一个测试面板：

```text
TestPanel
```

测试内容：

- 第一次 `ShowPanel<TestPanel>()` 会加载并创建。
- 第二次 `ShowPanel<TestPanel>()` 会命中缓存。
- `HidePanel<TestPanel>()` 会隐藏面板。
- `IsShow<TestPanel>()` 能返回正确状态。

---

## 13. 下一阶段

UIManager 第一版跑通后，再考虑：

- 面板打开参数
- 面板关闭回调
- 面板销毁
- UI 层级
- 弹窗栈

不要一开始就把 UI 系统做复杂。
