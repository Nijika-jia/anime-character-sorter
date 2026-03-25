# 动漫图片分类助手：一个基于 AI 的动漫角色识别与整理工具
> 基于 AnimeTrace API 的动漫角色图片分类工具，可自动识别角色与作品并按规则整理。

![.NET](https://img.shields.io/badge/.NET-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/WPF-512BD4?style=flat-square&logo=windows&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?style=flat-square&logo=c-sharp&logoColor=white)
![SQLite](https://img.shields.io/badge/SQLite-003B57?style=flat-square&logo=sqlite&logoColor=white)

---

## 下载
### 直接下载
- [最新版本](https://github.com/Nijika-jia/anime-character-sorter/releases/latest)
- [所有版本](https://github.com/Nijika-jia/anime-character-sorter/releases)

### 源码构建
```bash
git clone https://github.com/Nijika-jia/anime-character-sorter.git
cd AnimeSorter
dotnet build AnimeSorterWin\AnimeSorterWin.csproj -c Release
dotnet publish AnimeSorterWin\AnimeSorterWin.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## 简介
解决动漫图片堆积、角色/作品遗忘、手动整理低效等问题，支持批量识别与分类整理，适合画师、收藏爱好者等使用。

核心能力：
- 自动识别动漫角色与作品
- 按角色/作品/作品+角色多维度分类
- 交互式确认窗口：查看识别候选、框选人脸、手动修正
- 智能缓存识别结果，避免重复调用
- 导出/导入确认数据，支持离线整理

---

## 功能特点
- **智能缓存系统**：SQLite + MD5 缓存，相同图片无需重复识别
- **API 限流控制**：可调节并发数，内置 429 重试与退避机制
- **交互式确认窗口**
  - 列表展示所有图片识别状态
  - 图片预览 + 人脸框标注
  - 下拉选择候选作品/角色
  - 支持手动输入作品/角色名称
  - 批量确认、跳过、一键全部确认
- **三种分类模式**：按作品、按角色、按作品+角色
- **文件操作模式**：复制模式（保留原文件）/ 移动模式（剪切原文件）
- **导出/导入确认数据**：支持导出 `.animesortercache.json` 离线整理
- **实时统计面板**：已扫描、缓存命中、识别成功/失败、限流次数
- **MaterialDesign 界面**：现代化 WPF 界面，采用 MVVM 架构

---

## 技术栈
- **语言**：C# (.NET 8.0)
- **GUI**：WPF + MaterialDesignThemes
- **架构**：MVVM (CommunityToolkit.Mvvm)
- **数据库**：SQLite (Entity Framework Core)
- **API**：[AnimeTrace](https://www.animetrace.com/)
- **依赖**：Microsoft.Extensions.DependencyInjection、System.Threading.Tasks.Dataflow

---

## 使用说明
### 基本流程
1. **启动程序** → 选择输入目录（动漫图片）和输出目录（整理后存放位置）
2. **调整设置**：API 并发数（1-10）、文件操作模式（复制/移动）
3. **点击“开始”** → 程序自动扫描并识别所有图片，保存候选结果
4. **进入确认窗口**
   - 查看每张图片的识别候选
   - 选择/修改作品名与角色名
   - 批量确认、跳过或一键全部确认
5. **点击“开始整理文件”** → 按分类模式自动整理

### 导出/导入确认数据
- **导出**：在确认窗口点击「导出确认数据」，保存为 `.animesortercache.json`
- **导入**：主界面点击「导入确认数据」，选择 JSON 文件后继续整理

### 支持的图片格式
- JPG、JPEG、PNG、WEBP

### 输出结构
```
输出目录/
├── 作品名/              # 按作品分类
│   └── 角色名/          # 按作品+角色分类
├── 角色名/              # 按角色分类
└── Unknown/             # 标记为跳过的图片
```

---

## 注意事项
- 支持图片格式：JPG/JPEG/PNG/WEBP
- 需要联网调用 API 进行识别
- 建议显示器分辨率 1920×1080 及以上
- 缓存数据库路径：`%LocalAppData%\AnimeSorterWin\`

---

## 性能优化
- **异步处理管道**：基于 TPL Dataflow 构建高性能流水线
- **流式文件扫描**：避免一次性加载全部文件到内存
- **背压控制**：防止内存膨胀溢出
- **并发哈希计算**：充分利用多核 CPU 加速处理

---

## 常见问题
1. **无法识别**：检查网络连接、图片清晰度与大小
2. **结果不准**：使用确认窗口的候选选择或手动输入修正
3. **程序无响应**：检查网络与服务器状态，重启程序
4. **API 限流**：调低并发数滑块，降低请求频率

---

## 更新历史
### v2.0.0 - 全新 .NET 版本重构
-  技术栈升级：Python → .NET 8.0 + WPF
-  新增智能缓存系统（SQLite + MD5）
-  新增 API 限流控制（并发数 + RPS + 自动重试）
-  **新增交互式确认窗口**（预览、人脸框、候选选择、手动输入）
-  新增三种分类模式（作品/角色/作品+角色）
-  新增复制/移动文件操作模式
-  新增导出/导入确认数据功能
-  新增实时统计面板
-  MaterialDesign 现代化界面
-  异步处理管道，处理性能大幅提升

### v1.0.0
- 基础识别与分类功能
- 多模型与双分类模式
- 历史记录与输入补全
- 界面优化与跳过功能

---

## 致谢
本项目基于 [AnimeTrace](https://www.animetrace.com/) API 实现，感谢提供精准识别服务！
