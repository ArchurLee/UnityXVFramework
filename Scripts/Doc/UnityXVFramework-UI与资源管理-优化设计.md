# UnityXVFramework —— UI 框架与资源管理 优化设计文档

> 适用对象:`ArchurLee/UnityXVFramework`(C# + Addressables + UniTask + ModuleBase 骨架)
> 范围:**仅 UI 框架 + 资源管理**两块
> 目的:既作为"这两个模块该怎么写"的设计文档,也作为后续完善的施工清单

---

## 0. 怎么用这份文档

这份文档分三层,你可以分别取用:

1. **现状诊断** —— 当前代码缺什么、为什么是问题(对着代码说)
2. **目标设计** —— 该长成什么样,附代码骨架与 API 表
3. **施工清单** —— 按优先级排好的 TODO,可逐条勾

每个模块文档建议都按这个结构写:**现状 → 目标 → 设计 → API → 边界与陷阱 → 检查清单**。下面就用这个结构,顺便示范"框架文档该怎么写"。

一条贯穿全文的核心判断写在最前面:

> **不要照搬重型自研 AB 框架(如 ResPool 那套 `pathRefCnt` + `freeResPath`)。**
> 那套引用计数是因为自研 AB **没有**引用计数才不得不自己造。
> 你用的是 **Addressables,它内部已经维护了引用计数**:`LoadAssetAsync` 计数 +1,`Release` 计数 -1,归零自动卸载。
> 所以你要做的不是"重造一个计数表",而是**"保证每次 Load 都有配对的 Release,并把'谁来 Release'这件事交给一个明确的 owner"**。这是你和大项目最该不一样的地方。

---

## 1. 资源管理模块（AssetLoader）

### 1.1 现状诊断

当前 `AssetLoader` 的关键代码:

```csharp
private readonly Dictionary<string, object> loadedAssets = new(); // 只增不减
var handle = Addressables.LoadAssetAsync<T>(assetName);
await handle.ToUniTask();
// handle 被丢弃,assetHandle 只存了 .Result,没存 handle 本身
```

存在四个问题,按严重程度:

| # | 问题 | 根因 | 后果 |
|---|------|------|------|
| P0 | **永不释放** | 缓存只 Add 不 Remove,`AsyncOperationHandle` 加载后丢弃 | Addressables 引用计数永不归零 → 资源永不卸载 → **必然泄漏** |
| P0 | **句柄丢失** | `AssetHandle<T>` 只存了 `Asset`,没存 `AsyncOperationHandle` | 即使想释放也**无句柄可 Release** |
| P1 | **并发重复加载** | 缓存命中判断只看"已完成的结果";两个请求同时进来、第一个还没 await 完时,缓存为空 → 各发一次 `LoadAssetAsync` | 同一资源加载多份,计数错乱 |
| P2 | **无加载失败/取消处理** | 失败只记日志,调用方拿到 `default`;加载途中对象已销毁的竞态未处理 | 偶发空引用 |

### 1.2 目标设计

不做全局引用计数表,而是**让 Addressables 的句柄成为一等公民,谁加载谁(或它的 owner)负责释放**。

#### 1.2.1 AssetHandle 必须存住句柄

```csharp
namespace Core
{
    public class AssetHandle<T>
    {
        public string Key { get; }
        public T Asset { get; }
        public bool IsValid => Asset != null;

        // ★ 关键:存住底层句柄,这是能释放的前提
        internal AsyncOperationHandle Handle { get; }

        public AssetHandle(string key, T asset, AsyncOperationHandle handle)
        {
            Key = key;
            Asset = asset;
            Handle = handle;
        }
    }
}
```

#### 1.2.2 AssetLoader 提供配对的 Load / Release，并合并加载中的请求

核心思路:缓存里存的是 **`UniTask<AssetHandle<T>>`(加载任务)**,不是结果。这样并发请求会 await 同一个 task,天然去重;同时维护一个轻量引用计数(只是为了知道"还有没有人在用",决定何时调 Addressables 的 Release)。

```csharp
public class AssetLoader : ModuleBase
{
    private class Entry
    {
        public AsyncOperationHandle Handle;
        public int RefCount;
    }

    // key -> 加载任务（合并并发请求）
    private readonly Dictionary<string, UniTask<AsyncOperationHandle>> _loading = new();
    // key -> 已加载条目（引用计数 + 句柄）
    private readonly Dictionary<string, Entry> _loaded = new();

    public override string ModuleName => "AssetLoader";

    public async UniTask<AssetHandle<T>> LoadAsset<T>(string key)
    {
        // 1) 已加载：计数 +1，直接返回
        if (_loaded.TryGetValue(key, out var entry))
        {
            entry.RefCount++;
            return Wrap<T>(key, entry.Handle);
        }

        // 2) 正在加载：等同一个任务（关键的并发合并）
        if (_loading.TryGetValue(key, out var loadingTask))
        {
            var h = await loadingTask;
            if (_loaded.TryGetValue(key, out entry)) entry.RefCount++;
            return Wrap<T>(key, h);
        }

        // 3) 首次加载
        var op = Addressables.LoadAssetAsync<T>(key);
        var task = op.ToUniTask().ContinueWith(_ => (AsyncOperationHandle)op);
        _loading[key] = task;

        var handle = await task;
        _loading.Remove(key);

        if (op.Status != AsyncOperationStatus.Succeeded)
        {
            Logger.Error(ModuleName, $"Load failed: {key}");
            Addressables.Release(op);
            return new AssetHandle<T>(key, default, default);
        }

        _loaded[key] = new Entry { Handle = handle, RefCount = 1 };
        return Wrap<T>(key, handle);
    }

    /// <summary>释放一次引用；归零时真正交还给 Addressables</summary>
    public void Release(string key)
    {
        if (!_loaded.TryGetValue(key, out var entry)) return;

        entry.RefCount--;
        if (entry.RefCount <= 0)
        {
            Addressables.Release(entry.Handle); // ← Addressables 自己管卸载
            _loaded.Remove(key);
        }
    }

    private AssetHandle<T> Wrap<T>(string key, AsyncOperationHandle handle)
        => new(key, (T)handle.Result, handle);

    protected override void OnShutdown()
    {
        foreach (var e in _loaded.Values) Addressables.Release(e.Handle);
        _loaded.Clear();
        _loading.Clear();
    }
}
```

> 说明:这里的 `RefCount` 不是在重造大项目的 `pathRefCnt`。大项目那套要管"path → AB → 真实卸载时机";这里 `RefCount` 只回答一个问题——"还有没有人用,要不要现在调 `Addressables.Release`"。真正的卸载交给 Addressables。代码量差一个数量级。

### 1.3 API 一览（文档里建议都配这种表）

| 方法 | 用途 | 谁调用 | 配对 |
|------|------|--------|------|
| `LoadAsset<T>(key)` | 异步加载，引用 +1 | 面板 / 业务 owner | 必须配 `Release(key)` |
| `Release(key)` | 引用 -1，归零交还 Addressables | owner 销毁时 | —— |
| `OnShutdown()` | 全量释放兜底 | 框架关闭 | —— |

### 1.4 边界与陷阱（一定要在文档里写）

- **Load 必须配 Release**:这是新的铁律。漏一个就泄漏一个,多一个就提前卸载导致空引用。所以最好别让业务裸调 `LoadAsset`,而是经过下面 UI 那层的 owner(见 §3)。
- **加载途中 owner 已销毁**:`await` 期间面板可能已经关了。加载完要判断 owner 还在不在,不在就立即 `Release`。用 `CancellationToken`(见 §3.2)处理最干净。
- **Addressables 的 Release 传句柄,不传资源对象**:所以 `AssetHandle` 一定要存 `AsyncOperationHandle`。
- **`Instantiate` 出来的实例**有另一套:`Addressables.InstantiateAsync` / `ReleaseInstance`。如果面板 prefab 走 Instantiate,要用配对的那组,别混用。

### 1.5 施工清单

- [ ] `AssetHandle<T>` 增加 `AsyncOperationHandle Handle` 字段
- [ ] `AssetLoader` 改为 `_loading`(任务合并)+ `_loaded`(计数)双表
- [ ] 实现 `Release(key)`,归零调 `Addressables.Release`
- [ ] `OnShutdown` 全量释放兜底
- [ ] 加载失败路径 `Addressables.Release(op)` 防泄漏

---

## 2. UI 框架模块（UIManager + BasePanel）

### 2.1 现状诊断

`UIManager.CloseTopPanel` 里:

```csharp
panel.gameObject.SetActive(false); // 仅隐藏，永不 Destroy，panelDict 永不移除
```

| # | 问题 | 后果 |
|---|------|------|
| P0 | **面板永不销毁** | 打开过的面板全留在 `panelDict` 和内存里 |
| P0 | **面板与资源释放脱钩** | 面板加载的图标/图集没人 Release（叠加 §1 的泄漏） |
| P1 | **只能关栈顶** | `Close()` 写死 `CloseTopPanel`，非栈顶面板关错对象 |
| P1 | **无分层** | 弹窗 / tips / loading 和普通界面挤在一个栈，层级混乱 |
| P2 | **打开不能带参数** | `ShowPanel<T>()` 无法传 initData |
| P2 | **事件不随显隐配对** | 接了 EventManager 但没和生命周期挂钩，易出"隐藏后仍响应"的 bug |

### 2.2 目标设计：让 BasePanel 成为"自己资源的 owner"

这是整份文档最重要的一节,直接回答了"UI 实例和 UI 资源谁释放、是否统一 owner"——**答案:让面板自己当 owner,销毁时一起释放**。这等价于大项目里 `UIBase : ResAutoHandle` 的设计,但用 C# 更轻地实现。

```csharp
public abstract class BasePanel : MonoBehaviour
{
    // ★ 面板持有它加载的所有资源 key —— 这就是 owner 的"账本"
    private readonly List<string> _ownedAssets = new();
    // ★ 处理"加载途中面板被关"的竞态
    private CancellationTokenSource _cts;

    public bool IsShow { get; private set; }

    // —— 面板内统一走这个加载，自动登记到账本 ——
    protected async UniTask<T> LoadAssetAsync<T>(string key)
    {
        var token = _cts.Token;
        var handle = await GameManager.AssetLoader.LoadAsset<T>(key);
        if (token.IsCancellationRequested) // 加载途中面板已关
        {
            GameManager.AssetLoader.Release(key);
            return default;
        }
        if (handle.IsValid) _ownedAssets.Add(key);
        return handle.Asset;
    }

    // —— 框架调用的生命周期（见 §2.3）——
    internal void InternalCreate() { _cts = new CancellationTokenSource(); OnCreate(); }
    internal void InternalShow(object args) { IsShow = true; OnShow(args); RegisterEvents(); }
    internal void InternalHide() { IsShow = false; UnregisterEvents(); OnHide(); }
    internal void InternalDestroy()
    {
        _cts?.Cancel(); _cts?.Dispose();
        OnDestroy_();
        ReleaseAll();          // ★ 实例与资源一起释放
    }

    private void ReleaseAll()
    {
        foreach (var key in _ownedAssets) GameManager.AssetLoader.Release(key);
        _ownedAssets.Clear();
    }

    // —— 子类重写 ——
    protected virtual void OnCreate() {}            // 只一次：找组件、建子物体
    protected virtual void OnShow(object args) {}   // 每次显示：刷新数据
    protected virtual void OnHide() {}              // 每次隐藏
    protected virtual void OnDestroy_() {}          // 真销毁：清 OnCreate 建的东西
    protected virtual void RegisterEvents() {}      // 显示时注册
    protected virtual void UnregisterEvents() {}    // 隐藏时反注册

    public void Close() => GameManager.UI.ClosePanel(this); // 关自己，不是关栈顶
}
```

`UIManager` 销毁面板时:

```csharp
private void DestroyPanel(BasePanel panel)
{
    panel.InternalHide();
    panel.InternalDestroy();          // → ReleaseAll：面板加载的资源在此归还
    panelDict.Remove(panel.GetType().Name);
    GameObject.Destroy(panel.gameObject);
}
```

至此形成一条清晰的 owner 链:

```
UIManager.ClosePanel(panel)
  └─ DestroyPanel
       ├─ Destroy(gameObject)        【UI 实例释放】
       └─ panel.ReleaseAll()         【UI 资源释放：遍历 _ownedAssets 逐个 Release】
            └─ AssetLoader.Release(key)
                 └─ RefCount==0 → Addressables.Release → 真正卸载
```

> 一句话:**面板是 owner,`_ownedAssets` 是账本,`InternalDestroy` 是结账点**。实例和资源在这里一起释放——这正是你最初问我的那个"统一 owner"。

### 2.3 生命周期：把"一次性"和"每次"分开

当前 `BasePanel` 只有 `Init/Show/Hide`,缺了关键区分。建议定义这套(对标大项目 UIBase,但精简):

| 回调 | 时机 | 触发次数 | 放什么 |
|------|------|---------|--------|
| `OnCreate` | 面板实例化后 | 1 次 | 找组件、建子物体、绑按钮 |
| `OnShow(args)` | 每次显示 | N 次 | 收外部参数、刷新数据 |
| `OnHide` | 每次隐藏 | N 次 | 停动画、清临时态 |
| `OnDestroy_` | 真销毁 | 1 次 | 清 `OnCreate` 建的东西 |
| `RegisterEvents`/`UnregisterEvents` | 随显/隐 | N 次 | 事件配对,防隐藏后仍响应 |

> 对应大项目的 `OnLoad / OnShow / OnHide / OnDestroy / OnClean`。你早期不必做到那么细(`OnPrepare` 那种"等所有子 UI 就绪"的可以以后再加),但 **"一次性 OnCreate"和"每次 OnShow"必须分开**,否则刷新逻辑要么漏执行要么重复执行。

### 2.4 关闭任意面板 + 分层（P1）

- **`ClosePanel<T>()` / `ClosePanel(panel)`**:不止关栈顶。栈顶就走 Pop + 显示下一个;非栈顶就从栈里移除并直接销毁。
- **分层 UILayer**:至少分三层,各自一个父节点 + 各自的栈/列表:

  | 层 | 用途 | 是否入栈 |
  |----|------|---------|
  | Normal | 普通全屏界面 | 入栈,关栈顶回上一个 |
  | Popup | 弹窗 | 可叠多个 |
  | Top | tips / loading / 红点飘字 | 不入栈,独立管理 |

  `ShowPanel<T>(layer, args)` 按层挂到对应父节点。这样 loading 永远盖在最上、弹窗不会被普通界面逻辑误关。

### 2.5 边界与陷阱

- **`Close()` 必须关自己**:现在写死 `CloseTopPanel` 是个定时炸弹。改成 `ClosePanel(this)`。
- **异步打开期间被关**:`ShowPanel` 是 async,await 期间用户可能已点了关闭。用 §2.2 的 `CancellationToken`,或在 await 后检查面板是否还该显示。
- **缓存策略二选一,先选简单的**:
  - **方案 A(关闭即销毁)**——`ClosePanel` 直接 Destroy + ReleaseAll。实现简单,无泄漏,代价是重开要重新加载。先用它把整条 owner 链跑通。
  - **方案 B(LRU 池)**——关闭进池(隐藏),容量满才真销毁。对标大项目 `UICache`,复用率高。详见 §2.5.1。
  - **建议:先 A 跑通,再演进到 B。** 不要一上来就写 B——A 是 B 的基础,B 只是在 A 的"销毁"前面插了一层"先进池"。
- **别在 `BasePanel` 里裸调 `AssetLoader.LoadAsset`**:一定走 `LoadAssetAsync`(登记账本),否则 ReleaseAll 漏掉它就泄漏。

### 2.5.1 方案 B：LRU 面板缓存池（确定采用）

> 本节的关键结论均**经大项目源码验证**(`UIBase.lua` 的 `Recycle` / `Clean`、`UICache.lua`、`LRUCache.lua`),不是凭空设计。

#### 决策已定（三条）

| 决策 | 选择 | 说明 |
|------|------|------|
| 缓存面板的资源怎么处理 | **连资源一起冻结** | 进池**不**释放资源,被淘汰时才 ReleaseAll。下方有大项目代码佐证 |
| 同类面板是否多开 | **同类只开一个** | 缓存键用 `typeName` 即可,简单覆盖 90% 场景 |
| 缓存范围怎么控制 | **UIManager 维护黑名单** | 对标大项目 `UICache.ExcludeUI`,集中一份不缓存列表 |

#### 核心转折点：把"关闭"和"销毁"拆成两件事

方案 A 里关闭=销毁,`ReleaseAll` 跟着关闭走。**一旦加缓存,这两件事必须拆开**:

```
关闭(Recycle 回池):   OnHide → 进缓存池(隐藏)          ← 资源【保留】,不调 ReleaseAll
销毁(Destroy 淘汰):   OnDestroy_ → ReleaseAll           ← 只在被 LRU 淘汰时发生
```

> **这是整个方案 B 最重要的一句话**:引入缓存后,`ReleaseAll` 不能再绑在"关闭"上,只能绑在"真销毁"上。

#### 大项目佐证：回池时资源确实不释放

对照 `UIBase.lua` 两个函数,`releaseAll`(释放资源)只在 `Clean` 出现,`Recycle` 里没有:

| 动作 | `Recycle`(回池) | `Clean`(真销毁/被淘汰) |
|------|----------------|----------------------|
| `OnClean` | ✅ (UIBase.lua:1276) | ✅ (UIBase.lua:1368) |
| `OnDestroy` | ❌ 不调 | ✅ (UIBase.lua:1375) |
| **`releaseAll`(资源)** | ❌ **不调** | ✅ **(UIBase.lua:1390)** |
| 清子类变量 | 部分(isAll=false) | 全部(isAll=true) |
| GameObject | 留着 | 处理(1393) |

所以"连资源一起冻结"不是偷懒,而是经过验证的成熟做法——省去"重开时重新异步加载 + 处理加载竞态"的复杂度,代价仅是缓存期间多占一点内存,由 LRU 容量上限兜底。

#### BasePanel 需要新增的入口

```csharp
public abstract class BasePanel : MonoBehaviour
{
    // 是否可被缓存。默认走 UIManager 黑名单判断;特殊面板也可在此直接重写
    // (黑名单为主,这里留作个别面板的逃生口)

    // 新增"回池"入口:只清临时态,不释放资源
    internal void InternalRecycle()
    {
        IsShow = false;
        UnregisterEvents();
        OnHide();
        OnRecycleClean();      // 清临时数据,但 _ownedAssets 账本保持不动
        // 注意:这里【不】调 ReleaseAll
    }

    // 真销毁(被 LRU 淘汰时):走完整释放
    internal void InternalDestroy()
    {
        _cts?.Cancel(); _cts?.Dispose();
        OnDestroy_();
        ReleaseAll();          // ★ 资源在此一次性释放
    }

    protected virtual void OnRecycleClean() {}  // 回池时清临时态(对标大项目 OnClean)
}
```

#### UIManager 需要的改动（3 处）

```csharp
public class UIManager : ModuleBase
{
    private readonly Dictionary<string, BasePanel> _active = new();  // 活跃面板
    private LRUCache<string, BasePanel> _pool;                       // 缓存池(容量 5~8)
    private static readonly HashSet<string> _noCache = new()         // 黑名单
    {
        "LoadingPanel", "ToastPanel", /* 一次性弹窗等 */
    };

    protected override void OnInitialize()
    {
        // 淘汰回调:这里才真销毁 + 释放资源
        _pool = new LRUCache<string, BasePanel>(6, (key, panel) =>
        {
            panel.InternalDestroy();
            GameObject.Destroy(panel.gameObject);
        });
    }

    // ① 关闭:能缓存就回池,否则直接销毁
    public void ClosePanel(BasePanel panel)
    {
        string name = panel.GetType().Name;
        _active.Remove(name);
        // ... 处理 panelStack 出栈、显示下一个 ...

        if (_noCache.Contains(name))
        {
            panel.InternalDestroy();
            GameObject.Destroy(panel.gameObject);
        }
        else
        {
            panel.InternalRecycle();
            panel.transform.SetParent(_poolRoot, false);  // 挂到隐藏常驻节点
            _pool.PushByKV(name, panel);                   // 进池;超容量自动触发淘汰回调
        }
    }

    // ② 打开:先查活跃,再查池,最后才新建
    public async UniTask<T> GetPanel<T>() where T : BasePanel
    {
        string name = typeof(T).Name;
        if (_active.TryGetValue(name, out var p)) return p as T;

        var cached = _pool.Remove(name);   // 注意:Remove 不触发淘汰回调(复用,非销毁)
        if (cached != null)
        {
            cached.transform.SetParent(canvasTrans, false);
            _active[name] = cached;
            return cached as T;
        }
        // ... 未命中:Instantiate + 加载(方案 A 那套)...
    }
}
```

> **`_poolRoot`**:一个 `SetActive(false)` 的常驻节点(对标大项目 `UICache.poolRoot`)。回池面板挂到它下面,一个开关藏住全部,且不占激活 Canvas 的计算。

#### 时序总览

```
关 → 黑名单? ─是→ InternalDestroy(ReleaseAll) → Destroy
            └否→ InternalRecycle(不释放) → 挂 poolRoot → 进 LRU 池
开 → 活跃dict有? ─是→ 直接用
     └否→ LRU池有? ─是→ Remove 取出 → 挂回 Canvas → InternalShow
              └否→ Instantiate + 加载(方案 A 那套)
池满 → LRU 淘汰最旧 → InternalDestroy(ReleaseAll + Destroy)  ← 资源在此真正释放
```

#### 一个必须注意的陷阱：Remove 不能触发淘汰回调

复用(`GetPanel` 从池里取)走 `Remove`——它只是把面板拿出来,**不能**调淘汰回调(否则把要复用的面板销毁了)。只有"超容量"和"清池"才触发回调。大项目 `LRUCache.lua` 正是这么设计的:`Remove` 不调回调,`popTail`/`Clear`/`Destroy` 才调。直接复用大项目的 `LRUCache` 实现即可,无需重写。

#### 方案 B 施工清单

- [ ] 复用大项目 `LRUCache` 实现(双向链表 + map)
- [ ] `BasePanel` 增加 `InternalRecycle`(不释放资源)+ `OnRecycleClean`
- [ ] `UIManager` 增加 `_pool` + `_poolRoot` + 黑名单 `_noCache`
- [ ] `ClosePanel`:黑名单直销毁,其余回池
- [ ] `GetPanel`:活跃 → 池(Remove 复用)→ 新建 三级查找
- [ ] LRU 淘汰回调里调 `InternalDestroy` + `Destroy`(确认资源在此释放)
- [ ] 验证:Remove 复用路径**不**触发淘汰回调

### 2.6 施工清单

- [ ] `BasePanel` 增加 `_ownedAssets` 账本 + `LoadAssetAsync` 登记入口
- [ ] `BasePanel` 生命周期拆成 `OnCreate / OnShow(args) / OnHide / OnDestroy_`
- [ ] `RegisterEvents / UnregisterEvents` 随显隐配对
- [ ] `Close()` 改为关自己
- [ ] `UIManager.DestroyPanel`:Destroy + ReleaseAll + 从 dict 移除
- [ ] `ClosePanel<T>()` 支持关任意面板
- [ ] `ShowPanel<T>(args)` 支持打开带参数
- [ ] (P1) UILayer 分层
- [ ] (P2) LRU 面板缓存池(已确定采用,详见 §2.5.1)

---

## 3. 两个模块的协作关系（务必单独成节）

文档里建议画一张"谁持有谁、谁释放谁"的关系图,这是新人最容易问的:

```
GameManager
  ├─ AssetLoader   ← 全局资源仓库（Addressables 句柄 + 引用计数）
  └─ UIManager     ← 面板栈 / 字典 / 分层
        └─ BasePanel（owner）
              ├─ 持有: gameObject（UI 实例）
              └─ 持有: _ownedAssets（向 AssetLoader 借的资源 key）

释放时（ClosePanel）:
  UIManager.DestroyPanel
    ├─ Destroy(gameObject)              实例没了
    └─ panel.ReleaseAll()
         └─ foreach key: AssetLoader.Release(key)
              └─ RefCount 0 → Addressables.Release → 卸载
```

**一句话总纲**:
> UI 实例由 `UIManager` 创建/销毁;UI 资源由 `AssetLoader` 借出/收回;**`BasePanel` 是把两者绑在一起的 owner**——销毁面板这一个动作,同时了结实例和它借的所有资源。不存在"实例和资源各释放各的"。

---

## 4. 与大项目（自研 AB + ResPool/UICache）的取舍对照

| 能力 | 大项目做法 | 你应该怎么做 | 为什么不同 |
|------|-----------|-------------|-----------|
| 资源引用计数 | 自研 `ResPool.pathRefCnt` | **直接用 Addressables 内置计数** | Addressables 已有,无需重造 |
| 资源延迟卸载 | `freeResPath` + 5min/350MB 策略 | **早期不做**,归零即 Release | 过早优化,Addressables 卸载够用 |
| 资源 owner | `ResAutoHandle.__resHandles` | `BasePanel._ownedAssets` | 思路一致,C# 实现更轻 |
| 面板复用 | `UICache` LRU(容量 10) | **先"关闭即销毁",后期再 LRU** | 先正确,后性能 |
| GameObject 池 | `ResPool` 藏 poolRoot 复用 | **暂不需要** | Addressables Instantiate 已够,有需要再说 |
| 异步竞态 | `IsNull` 检查 | `CancellationToken` | C# 有更优雅的工具 |

**结论**:大项目那套"重"是被自研 AB 和海量界面逼出来的。你站在 Addressables + UniTask 上,**该学的是它的"分层职责 + owner 思想",而不是它的"引用计数/池化实现"**。后者在你的技术栈里大部分是多余的。

---

## 5. 推荐落地顺序

> 一次只解决一件事,每步都能独立验证。

**第一步(止血,解决泄漏根源)**
1. `AssetHandle` 存句柄 + `AssetLoader` 加 `Release`/任务合并(§1.2)
2. `BasePanel` 做成资源 owner(`_ownedAssets` + `ReleaseAll`)(§2.2)
3. `UIManager` 加销毁路径,关闭即 Destroy + ReleaseAll(§2.4 方案 A)

> 做完这三步,框架就**不再泄漏**,且 UI 实例与资源由统一 owner 释放——核心问题解决。

**第二步(完善易用性)**
4. 生命周期拆分 `OnCreate/OnShow/OnHide/OnDestroy_`(§2.3)
5. `Close()` 关自己、`ClosePanel<T>` 关任意、`ShowPanel<T>(args)` 带参(§2.4)
6. 事件随显隐配对(§2.3)

**第三步(规模上来再做)**
7. UILayer 分层
8. 面板 LRU 池化复用
9. 资源延迟卸载(若 Addressables 默认卸载造成卡顿才做)

---

## 附:框架文档"怎么写"的通用模板

每个模块文档都建议套这个骨架(就是本文用的结构):

```
# 模块名
## 1. 它解决什么问题（一句话）
## 2. 现状/背景（如果是改造）
## 3. 设计
    - 数据结构
    - 核心流程（配图/伪代码）
    - 代码骨架
## 4. API 一览（表格：方法 | 用途 | 谁调用 | 配对/注意）
## 5. 生命周期 / 时序（表格或时序图）
## 6. 边界与陷阱（最重要，写踩过的坑）
## 7. 与其他模块的关系
## 8. 施工清单 / TODO（可勾选）
```

写文档的几条原则:
- **对着代码说**,别空谈概念("`CloseTopPanel` 里只 `SetActive`"比"缺少销毁机制"有用)
- **每个设计都说明"为什么"**,尤其是和直觉不同的取舍(如"为什么不做引用计数表")
- **陷阱单独成节**,这是文档最值钱的部分
- **TODO 可勾选**,让文档同时是施工单
```
