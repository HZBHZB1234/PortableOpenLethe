# 用户脚本（Jint）— 当前实现

脚本是**每个用户自己安装**的 JavaScript，始终绑定安装者账号执行。管理员通过 `/admin/scripts/grants` 逐用户授予高权限能力。

## 安装与管理

- `POST /api/Script/install { name, source }`
- `POST /api/Script/update { name, source }`
- `POST /api/Script/set-enabled?enabled=true|false { name }`
- `POST /api/Script/remove { name }`
- `POST /api/Script/list {}`
- `POST /api/Script/Invoke { scriptName, action, args }`

所有端点要求游戏 JWT；账号从 token 的 `sub` 解析。

## 脚本头（权限声明）

```js
/* { "id":"my-script", "version":"1.0.0",
     "permissions":["log","account.read:self","account.write:self","endpoint"] } */
```

权限白名单：`log`、`account.read:self`、`account.write:self`、`endpoint`、`config.read`、`packet.read`、`packet.intercept`、`packet.modify`、`timer`、`http`。

基础能力（`log` / `account.read:self` / `account.write:self` / `endpoint` / `config.read`）声明即可用；其余必须在 `script_grants` 中由管理员为「该用户 + 该脚本」显式授予才生效。`crossAccount` **不存在于用户脚本权限集**。

## 宿主 API

- `Log.info/warn/error(msg)`
- `Account.get(doc)` → 文档 JSON 对象或 null；`Account.setField(doc, value)` 写回
- `Hub.on("accountLoaded", fn)`；`Hub.action(name, fn)`；`Hub.intercept.request(path, fn)`；`Hub.intercept.response(path, fn)`

拦截路径白名单：`/api/*`、`/login/*`。请求接管返回的对象会被包成响应信封（`packetId = 67`）；响应改写接收完整信封对象，返回修改后的对象。脚本抛异常时回退原行为。

## 已实现 / 未实现

已实现：脚本 CRUD、权限门面与逐用户授权、账号自读写、`accountLoaded` 事件、具名 action、`/api/Script/Invoke`、请求接管、响应改写、16MB 内存上限与异常隔离。

未实现（规划中的 v2）：`Hub.timer.*`、`Http.*`、`packet.read` 旁观事件、`packetId` 逐包配置。不要在脚本中依赖这些 API。

## 示例

见 `examples/scripts/daily-checkin.js`（签到）与 `first-page-tweak.js`（首包改写，需管理员授权 `packet.modify`）。

## 安全

Jint 引擎禁用 CLR 类型解析，脚本没有文件、反射、`eval`、`require` 能力；不要安装来源不明的脚本。插件（DLL，完全信任）默认关闭，需 `Plugins:Enabled=true` 才加载。
