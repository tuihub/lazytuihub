---
status: implemented
category: repository
---
# 通过互联网访问第三方数据和服务

[English](2026-11-26-third-party-access.en.md) | 中文

本仓库中的代码通过互联网获取第三方数据或调用第三方服务时，只能使用该第三方官方提供的途径。产品、插件、工具和原型都遵守这条约定，通过依赖发出的请求和备用路径也在范围内。第三方没有为某项操作提供可用的官方途径时，该联网功能保持不可用。

第三方可以自行选择发布形式。官方数据库转储、RSS 订阅源、账户登录 API，以及网站提供给自家前端使用的 API，都可以作为接入途径。使用前端 API 时，依据网站 robots.txt 中适用于 LazyTuiHub 的规则判断能否访问目标路径，并遵守限流和重试时间。User-Agent 应兼容常见网站并如实标识 LazyTuiHub，例如 `Mozilla/5.0 (compatible; LazyTuiHub; +https://github.com/tuihub/lazytuihub)`。判断一个途径是否由官方提供，可以查看第三方发布的资料或其自家前端的实际用法。[Robots Exclusion Protocol](https://www.rfc-editor.org/rfc/rfc9309.html)

这条约定对读取、导入或转换本地文件没有限制。插件可以读取本机的 Steam 安装清单，也可以查询由数据库转储转换得到的 SQLite 文件。如果仓库中的代码还负责下载文件，或访问文件中的链接，对应的联网请求仍遵守上述约定。
