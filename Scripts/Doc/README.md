# _LearningWorkspace

这个目录是你的个人学习和编程工作区，用来练习 Unity 框架写法，避免和已经导入的第三方或参考项目代码混在一起。

## AI 协作记忆

- `Assets/_LearningWorkspace/Scripts/Doc/FRAMEWORK_LEARNING_ROADMAP.md` 是总览路线，只记录整体学习顺序和每节入口。
- `Assets/_LearningWorkspace/Scripts/Doc` 下可以放学习文档和 AI 协作记忆。
- 每个学习部分可以单独建文件夹，例如 `01_FrameworkModuleBase`。
- 章节文件夹里的文档应当是“学习指引”，用于引导用户自己写代码，不要直接替用户完成代码。
- `COMMON_QUESTIONS.md` 用于记录用户学习过程中问过的常见问题，不要把踩坑记录分散写进各章节 Guide。
- 除非用户明确要求，否则不要修改 `_LearningWorkspace/Scripts/Core` 等代码文件。
- 用户当前已完成框架基础件前 4 节学习指引，并开始复刻第一个资源加载模块 `AssetLoader`。
- `01_FrameworkModuleBase` 里的文档不要命名为 `README.md`，应使用更具体的文档名。
- 项目根目录下的 `LearningFramework.code-workspace` 是学习专用 VS Code 工作区，只展示 `_LearningWorkspace` 和原项目框架/系统相关代码。
- 当前学习代码已经推进到 `IModule`、`ModuleBase`、`Logger`、`AssetHandle<T>` 的第一版；今天记录过的关键坑是命名空间大小写和泛型类型约束。
- `AssetLoader` 第一版保留 callback，使用 `AssetHandle<T>` 包装结果，缓存 key 使用资源类型和资源名组合。

## 建议目录

- `Scripts/Core`：框架核心，例如启动器、模块管理、生命周期管理。
- `Scripts/Gameplay`：玩法逻辑，例如角色、卡牌、战斗、关卡。
- `Scripts/UI`：界面逻辑。
- `Scripts/Data`：数据结构、配置类、ScriptableObject 脚本。
- `Scripts/Utils`：通用工具类。
- `Scenes`：你自己的测试场景。
- `Prefabs`：你自己的测试预制体。
- `ScriptableObjects`：你自己的配置资源。

## 使用建议

- 学习和实验代码优先放在这里。
- 暂时不要直接改 `Assets/Scripts` 里的导入代码，先通过阅读和复刻理解框架思想。
- 如果需要引用原项目已有管理器，可以先在这里写测试脚本调用，确认理解后再决定是否重构。
