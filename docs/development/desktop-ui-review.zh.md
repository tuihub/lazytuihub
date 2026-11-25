# 桌面 UI 自查

中文 | [English](desktop-ui-review.en.md)

新增页面或修改共享控件时，在真实插件页面和实际主题下，操作这次改动影响的控件。[阅读器工具栏](../../tests/LazyTuiHub.Tests/Desktop/DesktopTests.Layout.cs) 给出最小接入实例，[游戏书架](../../tests/LazyTuiHub.Tests/Desktop/DesktopTests.Visual.cs) 还测量字形、封面、文字对比度和动画。

## 声明场景

1. 使用已有的无窗口桌面驱动和独立工作区。准备与改动相关的代表性数据，例如长标题和缺失封面。等到资源实际就绪再开始测量。
2. 给 xUnit 测试添加 `[Trait(DesktopVisualScene.Trait, "true")]`。页面使用唯一短名，页面和控件的短名须能用作文件名。跨设计系统的改动应覆盖所支持的设计和明暗模式。按改动范围在交互场景中操作相关窗口宽度、选中态、禁用态、按压、弹层或返回导航。
3. 声明悬停和聚焦时应保持位置的控件及区域。给驱动指定一个无关的可聚焦控件，以及目标范围外的指针静止位置。对于有意移动的元素，在交互场景中检查其位移。涉及动效时采集实际过渡，并验证“减少动态效果”下的行为。

```csharp
var scene = new DesktopVisualScene(driver, "reader", design, mode);
scene.Interaction("subscribe", "Add subscription", addButton);
scene.Track("feed-picker", feedPicker);
await scene.CheckAsync(readFilter);
```

`Interaction` 驱动指针移入、移出、键盘聚焦、聚焦时悬停和移出后保留焦点。使用 `hover` 和 `alternate` 可从两个位置触发同一控件，使用 `checkHover` 可在指针仍悬停时验证控件自身的约定。`IconLabel` 测量组合按钮中文字和图标的可见像素。可选的 `fluentSpecimen` 指向[官方参照样板](../../.agents/prototypes/design-baselines/web/fluent.jsx)中同文案、同图标的微软 Fluent UI React 控件。`Contrast` 测量文字与实际背景的对比度。共享驱动与报告会自动发现这些测试和区域。[场景接口](../../tests/LazyTuiHub.Tests/Desktop/DesktopVisualScene.cs)

## 运行与判读

```sh
mise run setup:design-baselines
mise run review:desktop
```

首次使用或样板依赖变化时运行第一条。打开 `.local/desktop-review/index.html` 查看最近一次成功报告，每次运行都有独立的 JSON 测量和 PNG 局部图。修复问题时保留前一次报告，使用相同的数据、主题、缩放、字体和输入状态复测。产品与官方 Web 样板均在后台无窗口渲染。

| 检查项 | 判读依据 |
| --- | --- |
| 布局与交互 | 在产品测试中验证约定的边界、焦点、命中位置、排版基线或状态转换。调整窗口并执行曾触发问题的实际操作。 |
| 绘制范围 | 检查两次静止采样的噪声数、悬停和聚焦时的差分，以及移出后的残留像素。整卡可点击时分别悬停封面与标题。画面未稳定时回查资源加载和动画。 |
| 图文对齐 | 对照四档抗锯齿阈值下的可见中心偏移、局部原图与对应的官方样板。报告用逻辑单位表示字形偏移，用设备像素表示着色范围。 |
| 设计判定 | 依据相应平台的设计规范与权威样板解释差异，记录实际验证的平台、主题和渲染器。涉及系统合成、字体回退或平台特有行为时，补查原生渲染。 |

可稳定复现的行为和布局回归进入常规产品测试。像素报告按需运行，生成局部证据，无需前台窗口。平台相关的整页截图不作为 CI 门禁。[设计系统 Note](../../.agents/notes/2026-11-18-mvp-design-systems.zh.md)
