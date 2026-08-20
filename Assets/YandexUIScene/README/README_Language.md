中英文切换操作步骤：

Assets/Local文件夹下一个名为 LanguageDatabase 的资源文件。

选中它，在 Inspector（属性） 窗口中，您会看到 Translations 列表：

点击右下角的 + 按钮可以添加一个翻译项。
Key：填写该文本的唯一标识符（例如：main_title，btn_start，rule_text 等）。
Cn：填写该 Key 对应的中文文本。
En：填写该 Key 对应的英文文本。
TIP

您可以预先为规则说明页面的文本配置如下翻译：

Key: rule_title | Cn: 规则说明 | En: RULES
Key: rule_body | Cn: 躲避红色的方块，击碎它们获得积分！ | En: Avoid the red cubes and destroy them to score!
Key: rule_confirm | Cn: 按 空格键 开始游戏 | En: Press SPACE to start


第二步：为 UI 文本组件挂载本地化脚本
对于场景中所有需要翻译的 TextMesh Pro 文本组件（例如规则说明标题、提示文案等）：

在 Hierarchy（层级） 视图中选中对应的 Text (TMP) 游戏物体。
点击 Add Component 搜索并添加 LocalizedText 脚本。
将您在第一步创建的 LanguageDatabase 资源文件拖入该组件的 Database 插槽中。
在 Translation Key 中填写您在数据库里配好的 Key（例如 rule_title）。
重要：确保该 TextMesh Pro 组件使用的 Font Asset（字体资产） 能够显示中文。若使用的是纯英文字体，切换到中文时会显示为方块（口口）。


选中对应的 Image 游戏物体。
点击 Add Component 搜索并添加 LocalizedImage 脚本。
在 Inspector 的 Cn Sprite 中拖入中文版本的图片，在 En Sprite 中拖入英文版本的图片即可。

选中对应的 textmeshpro组件
点击 Add Component 搜索并添加 LocalizedFont 脚本。
在 Inspector 的 Cn Font 中拖入中文版本的字体，在 En Font 中拖入英文版本的字体即可。


