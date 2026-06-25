# UI Owner 与缓存池学习指引（优化设计剩余功能点）

这一阶段不再是“新写一个模块”，而是把已有的 `AssetLoader`、`UIManager`、`BasePanel` 真正打通。

目标对应 `UnityXVFramework-UI与资源管理-优化设计.md` 里还没做完的部分。

资源层（`AssetLoader`）已经做完了：句柄保留、引用计数、`Release`、加载合并、`OnShutdown` 兜底都有了。

所以这一阶段的重点全在 UI 层，核心只有一句话：

```text
让面板成为它自己资源的 owner，关闭面板这一个动作，同时了结实例和它借的资源。
```

原项目参考：

- `Scripts/Core/UI/UIManager.cs`
- `Scripts/Core/UI/BasePanel.cs`
- `Scripts/Core/Assets/AssetLoader.cs`

---

## 0. 这一阶段一共要做几件事

按落地顺序分三步，每一步都能单独验证：

```text
第一步（止血）
    面板做成 owner
    关闭走真销毁，实例和资源一起释放

第二步（易用性）
    生命周期拆成 OnCreate / OnShow / OnHide / OnDestroy
    关任意面板、带参打开
    事件随显隐配对

第三步（规模上来再做）
    UILayer 分层
    LRU 缓存池
    加载中的取消处理
```

不要跳着做。第一步没跑通之前，不要碰缓存池。

---

## 第一步：面板做成 owner（止血）

### 1.1 为什么先做这个

现在的 `UIManager.LoadPanelAsync` 拿到 `handle.Asset` 之后，句柄就丢了。

面板自己运行时动态加载的图标、特效，也没有任何地方记录。

结果就是：关掉面板，资源还占着，永远不释放。

所以第一步要解决的就是这条泄漏。

### 1.2 面板要持有什么

面板作为 owner，持有的不是资源本身，而是“我借过哪些资源的 key”。

可以理解成一张借书记录：

```text
BackpackPanel 的账本
    Icon_Sword
    Icon_Shield
    Effect_Click
```

建议在 `BasePanel` 里加一个清单：

```csharp
private readonly List<string> _ownedAssets = new();
```

### 1.3 面板内统一的加载入口

关键约束：面板不要裸调 `AssetLoader.LoadAsset`，要走面板自己的一个方法，加载成功后顺手登记。

骨架思路（具体实现你来写）：

```text
protected LoadAssetAsync<T>(key)
    调 AssetLoader.LoadAsset<T>(key)
    成功 → _ownedAssets.Add(key)
    返回 asset
```

这样只要面板用过的资源，账本里一定有记录，后面才能统一还。

### 1.4 面板销毁时统一归还

面板真销毁时，遍历账本逐个 `Release`，再清空账本。

顺序很重要：

```text
先 Release 资源
再 Destroy 实例
最后从 panelDict 移除
```

因为面板实例销毁后，你就拿不到账本了。

骨架思路：

```text
InternalDestroy()
    foreach key in _ownedAssets
        AssetLoader.Release(key)
    _ownedAssets.Clear()
```

### 1.5 谁来执行销毁

第一步用最简单的“关闭即销毁”，由 `UIManager` 执行：

```text
UIManager.ClosePanel(panel)
    panel.InternalDestroy()            面板还资源
    GameObject.Destroy(panel.gameObject)   销毁实例
    panelDict.Remove(panelName)        移出缓存
    AssetLoader.Release(panelPrefabKey)    还 Prefab 本身
```

注意最后一行：Prefab 本身也是借来的，也要还。

### 1.6 两种资源要分清楚

这里有个容易混的点：

```text
Prefab 里直接挂的图（Image 上拖好的 Sprite）
    跟随 Prefab 资源
    释放 Prefab 时一起卸载
    不需要登记到账本

面板运行时动态加载的图
    和 Prefab 无关
    必须登记到账本
    必须面板自己 Release
```

owner 账本只管第二种。

### 1.7 第一步验证

- 打开面板 → 动态加载几张图 → 关闭面板
- 看日志里 `Release` 是否成对出现
- 引用计数归零后是否真的卸载
- 反复开关，内存是否稳定不涨

---

## 第二步：生命周期与易用性

### 2.1 生命周期为什么要拆

现在 `BasePanel` 的 `Init / Show / Hide` 是混在一起的。

问题是分不清“只做一次的事”和“每次显示都要做的事”。

比如“找组件、绑按钮”应该只做一次，“刷新数据”应该每次显示都做。

混在一起，刷新逻辑要么漏执行，要么重复执行。

### 2.2 建议的生命周期

```text
OnCreate      实例化后一次    找组件、建子物体、绑按钮
OnShow(args)  每次显示        收参数、刷新数据
OnHide        每次隐藏        停动画、清临时态
OnDestroy_    真销毁一次      清 OnCreate 建的东西
```

框架内部用 `InternalCreate / InternalShow / InternalHide / InternalDestroy` 包一层，子类只重写 `OnXxx`。

注意 `OnDestroy_` 加下划线，避免和 Unity 自带的 `OnDestroy` 撞名。

### 2.3 关任意面板

现在 `BasePanel.Close()` 写死了 `CloseTopPanel`，这是个定时炸弹：非栈顶面板调 `Close` 会关错对象。

要改成关自己：

```text
Close() → UIManager.ClosePanel(this)
```

`UIManager` 里要支持两种关闭：

```text
关的是栈顶    → 正常出栈，显示下一个
关的是非栈顶  → 从栈里移除这一个，不影响栈顶显示
```

### 2.4 带参数打开

现在 `ShowPanel<T>()` 打开后，面板拿不到外部数据。

要加一个带参版本：

```text
ShowPanel<T>(object args)
    拿到面板
    InternalShow(args)   把 args 传进 OnShow
```

这样背包面板打开时就能知道“打开的是哪个分页”。

### 2.5 事件随显隐配对

`EventManager` 已经有了注册和注销。

问题是面板没把它和显隐绑起来，容易出现“隐藏后还在响应事件”。

约定：

```text
InternalShow → RegisterEvents()
InternalHide → UnregisterEvents()
```

子类在 `RegisterEvents` 里注册，在 `UnregisterEvents` 里一一对应注销。显示时接，隐藏时断。

### 2.6 异步打开期间被关

`ShowPanel` 是异步的，await 期间用户可能已经点了关闭。

第一步可以先用一个简单判断：await 拿到面板后，检查它是不是还该显示。

更干净的做法用 `CancellationToken`，但这个可以放到第三步再补。

---

## 第三步：分层与缓存池（规模上来再做）

### 3.1 UILayer 分层

现在所有面板挤在一个栈里，弹窗、loading、普通界面混着管，层级会乱。

至少分三层，各自一个父节点、各自的管理方式：

```text
Normal   普通全屏界面    入栈，关栈顶回上一个
Popup    弹窗            可叠多个
Top      tips / loading  不入栈，独立管理
```

`ShowPanel<T>(layer, args)` 按层挂到对应父节点。

这样 loading 永远在最上面，弹窗也不会被普通界面的逻辑误关。

### 3.2 LRU 缓存池要解决什么

回到你最初的疑问：面板缓存在 `panelDict`，没有上限。

加上 LRU 池后，关闭的面板先进池（隐藏，不销毁），池满了才淘汰最旧的真销毁。

这样常用面板反复开关不用重新加载，又不会无限占内存。

### 3.3 关闭和打开的流程会变成什么样

关闭时多了一个分叉：

```text
关闭面板
    黑名单里？（loading、一次性弹窗）
        是 → InternalDestroy（还资源）→ Destroy
        否 → InternalRecycle（不还资源）→ 挂到 poolRoot → 进池
```

打开时变成三级查找：

```text
打开面板
    活跃 dict 里有？     → 直接用
    LRU 池里有？         → Remove 取出 → 挂回 Canvas → InternalShow
    都没有？             → Instantiate + 加载（第一步那套）
```

### 3.4 进池不释放资源，淘汰才释放

这是缓存池最关键的一点：

```text
进池      只隐藏，不释放（图标特效都留着，重开秒显示）
被淘汰    这时才 InternalDestroy + Destroy，资源在这里才真正还
```

所以“谁销毁面板”的答案，在缓存池模式下是：

```text
LRU 淘汰回调
```

不是关闭的瞬间，而是池满淘汰的时候。

### 3.5 一个必须避开的坑：Remove 不能触发淘汰回调

复用（从池里取出来重新用）走的是 `Remove`。

`Remove` 绝对不能调淘汰回调，否则你正要复用的面板被销毁了。

只有“超容量淘汰”和“清空池”才触发回调。

```text
Remove（复用）        不调回调
超容量 / 清池         调回调（InternalDestroy + Destroy）
```

### 3.6 poolRoot

准备一个常驻的、`SetActive(false)` 的节点当池子根节点。

回池的面板都挂到它下面，一个开关藏住全部，也不占激活 Canvas 的计算。

---

## 4. 三者职责边界（记住这张图）

```text
UIManager     管面板实例（创建 / 销毁 / 栈 / 池）
BasePanel     管自己借的资源（owner，持有资源 key 的账本）
AssetLoader   管引用计数和真正卸载（不关心是谁借的）
```

释放链路：

```text
UIManager 销毁面板
    Destroy(gameObject)         实例没了
    panel 遍历账本 Release
        AssetLoader 计数 -1
            归零 → Addressables 卸载
```

一句话：UI 实例归 UIManager，UI 资源归 BasePanel，计数和卸载归 AssetLoader。

---

## 5. 施工清单（可逐条勾）

### 第一步：止血

- [ ] `BasePanel` 加 `_ownedAssets` 账本
- [ ] `BasePanel` 加统一加载入口 `LoadAssetAsync<T>`，加载成功登记
- [ ] `BasePanel.InternalDestroy` 遍历账本 `Release` 并清空
- [ ] `UIManager.ClosePanel` 走真销毁：还资源 → Destroy → 移出 dict → 还 Prefab
- [ ] 验证反复开关内存稳定

### 第二步：易用性

- [ ] 生命周期拆成 `OnCreate / OnShow(args) / OnHide / OnDestroy_`
- [ ] 框架内部用 `InternalCreate / InternalShow / InternalHide / InternalDestroy` 包一层
- [ ] `Close()` 改成关自己（`ClosePanel(this)`）
- [ ] `ClosePanel` 支持关任意面板（栈顶 / 非栈顶分别处理）
- [ ] `ShowPanel<T>(args)` 带参打开
- [ ] `RegisterEvents / UnregisterEvents` 随显隐配对

### 第三步：规模

- [ ] UILayer 分层（Normal / Popup / Top）
- [ ] `BasePanel.InternalRecycle`（进池不释放资源）
- [ ] `UIManager` 加 `_pool` + `poolRoot` + 黑名单
- [ ] `ClosePanel` 分叉：黑名单直销毁，其余进池
- [ ] `GetPanel` 三级查找：活跃 → 池 → 新建
- [ ] LRU 淘汰回调里 `InternalDestroy + Destroy`
- [ ] 验证：`Remove` 复用路径不触发淘汰回调
- [ ] 加载中取消（`CancellationToken`）

---

## 6. 推荐节奏

不要一次写完。建议：

```text
先把第一步三条做完，跑通“关闭即销毁 + 资源归还”
确认不漏不炸之后，再做第二步的生命周期和易用性
最后规模真的需要了，再上分层和缓存池
```

第一步做完，框架就已经不泄漏了，这是最重要的里程碑。