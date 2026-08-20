# 🎮 Controls Tutorial 弹窗功能说明

暂停菜单中新增 **Controls Tutorial** 按钮，点击或按A键可切换操作说明面板的显示/隐藏。

---

## 1. 交互流程

```
游戏中按 ESC/Start → 打开三按钮暂停菜单
                        ↓
                选中 ControlsTutorial 按钮
                        ↓
          ┌───────────按 A / 点击────────────┐
          ↓                                  ↓
   显示教程面板                        隐藏教程面板
   （再按A则隐藏）                   （再按A则显示）
```

---

## 2. 按键规则

| 操作 | 键盘 | 手柄 | 说明 |
| :--- | :--- | :--- | :--- |
| 打开/关闭暂停菜单 | `Escape` (ESC) | `Start` 键 | 显示/隐藏三按钮菜单 |
| 切换教程面板 | 选中按钮后按 `Space` / `Enter` | 选中按钮后按 `A` 键 | 显示 ↔ 隐藏 |
| 切换教程面板（面板显示时） | `Space` / `Enter` | `A` 键 | 直接关闭面板 |
| 继续游戏 | 点击 `CONTINUE` | 点击 `CONTINUE` | 恢复游戏 |
| 退出游戏 | 点击 `QUIT` | 点击 `QUIT` | 进入结束动画 |

---

## 3. 核心方法

### 📁 SimpleUIManager.cs

| 方法 | 作用 |
| :--- | :--- |
| `ToggleControlsTutorial()` | 切换教程面板的显示/隐藏 |
| `ShowControlsTutorial()` | 显示面板，记录当前选中按钮 |
| `HideControlsTutorial()` | 隐藏面板，恢复上次选中的按钮焦点 |
| `OnConfirmPressed()` | 确认键回调，面板显示时关闭面板；未显示时若选中 Tutorial 按钮则打开面板 |

**核心字段**：
- `controlsTutorialButton` — Controls Tutorial 按钮引用
- `controlsTutorialPanel` — 教程面板引用
- `isShowingTutorial` — 面板是否显示
- `confirmAction` — 确认键（Space/Enter/手柄A）输入动作

---

## 4. 场景配置

### 4.1 Inspector 字段

在 `SimpleUIManager` 上配置：

| 字段 | 对象 |
| :--- | :--- |
| `Menu Canvas Object` | `PauseMenuCanvas` |
| `Resume Button` | `ResumeButton` |
| `Quit Button` | `QuitButton` |
| `Controls Tutorial Button` | `controlsTutorial` |
| `Controls Tutorial Panel` | `ControlsTutorialPanel` |
| `Default Selected Button` | `ResumeButton` |

> 如果 `Controls Tutorial Panel` 未赋值，代码会自动按名称 `ControlsTutorialPanel` 查找。

### 4.2 层级结构

```
PauseMenuCanvas
├── PausePanel
│   ├── ResumeButton
│   ├── QuitButton
│   └── controlsTutorial
└── ControlsTutorialPanel
```

### 4.3 初始状态

`ControlsTutorialPanel` 默认应为**非激活状态**（Inspector 左侧复选框不勾选）。

---

## 5. UI 焦点保护

1. 打开菜单时：自动选中 `defaultSelectedButton`
2. 显示教程前：记录当前选中按钮
3. 隐藏教程后：恢复到记录的按钮（Controls Tutorial）
4. Update 中：每帧检查焦点，丢失则自动恢复
