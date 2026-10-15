# 首关手绘视觉素材

这两张项目素材使用内置 `imagegen` 工具生成，2026-10-15。风格依据《游戏设计文档.md》“游戏页面设计”及 Word 设计稿中森林、纸签按钮、Q 版史莱姆的参考方向。没有使用 CLI/API 备用路径。

- `ForestClearing-v1.png`：森林空地插画；在独立 UI 测试场景中作为背景 Sprite，另交付可复用的 `PF_FirstLevelScenery.prefab`。
- `StorybookIcons-v1.png`：带真实 Alpha 的四图标图集；Unity Sprite Editor Data Provider API 切出 Food / Furnace / Obstacle / Slime，保留原始 PNG，不改动像素数据。

背景中的小路、树木、水塘、洞穴仅是美术，不提供 MapBounds、路线、Anchor、终点统计或碰撞。它没有加入正式关卡，也不随 Player Prefab 自动创建。UI 试玩中的机关仍由 TEST_ONLY 后端创建和清理，生成插画只是显示层。

完整生成提示见同目录 `generation-prompts.json`。文字、按钮、面板边缘、选中标记、阴影和交互反馈仍由 Unity 原生组件绘制，便于继续编辑和适配分辨率。
