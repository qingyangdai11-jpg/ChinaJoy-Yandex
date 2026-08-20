关于替换角色

创建任意物体（球体/胶囊体/自定义模型）
给它添加 Collider（如 SphereCollider）
添加 PlayerIdentity 组件
如需移动控制，再添加 SimplePlayerController（会自动添加 Rigidbody 和 PlayerIdentity）
将 Main Camera 上 CameraFollow 的 target 拖到该物体上