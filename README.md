# 动漫图片分类助手：一个基于 AI 的动漫角色识别与整理工具
<div align="center">
基于 AnimeTrace API 的动漫角色识别与分类工具，可自动识别角色与作品并按规则整理。
</div>

<p align="center">
<img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white" alt=".NET 8.0">
<img src="https://img.shields.io/badge/WPF-Windows-512BD4?style=flat-square&logo=windows&logoColor=white" alt="WPF">
<img src="https://img.shields.io/badge/C%23-12.0-239120?style=flat-square&logo=c-sharp&logoColor=white" alt="C# 12.0">
<img src="https://img.shields.io/badge/SQLite-3.x-003B57?style=flat-square&logo=sqlite&logoColor=white" alt="SQLite 3.x">
</p>

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

<div style="background-color:#f6f8fa; padding:14px; border-radius:8px; margin-bottom:20px;">
解决动漫图片堆积、角色与作品信息遗忘、手动整理效率低下等问题。支持批量识别、自动分类、交互式确认，适用于画师、图片收藏爱好者及日常整理使用。
</div>
<img width="1641" height="1017" alt="image" src="https://github.com/user-attachments/assets/cb60d728-0065-4451-8699-49bc49ea05b1" />

**核心能力：**
- 自动识别动漫角色与对应作品
- 支持按角色、按作品、按作品+角色多维度分类
- 交互式确认窗口，支持查看识别候选、人脸标注与手动修正
- 智能缓存识别结果，避免重复请求
- 支持导出与导入识别数据，实现离线整理

---

## 功能特点

<div style="background-color:#f6f8fa; padding:14px; border-radius:8px;">
<ul style="margin:0; padding-left:20px;">
<li><strong>智能缓存系统</strong>：SQLite + MD5 缓存机制，相同图片无需重复识别</li>
<li><strong>API 限流控制</strong>：可调节并发数量，内置 429 重试与退避策略</li>
<li><strong>交互式确认窗口</strong>：图片预览、人脸框标注、候选选择、手动输入、批量操作</li>
<li><strong>三种分类模式</strong>：按作品、按角色、按作品+角色</li>
<li><strong>文件操作模式</strong>：支持复制（保留原文件）与移动（剪切原文件）</li>
<li><strong>数据导出与导入</strong>：支持导出 .animesortercache.json 离线整理</li>
<li><strong>实时统计面板</strong>：显示扫描数量、缓存命中、识别结果、限流次数</li>
<li><strong>现代化界面</strong>：基于 WPF 与 MaterialDesign 风格，采用 MVVM 架构</li>
</ul>
</div>

---

## 技术栈

<table>
<tr>
<td width="150"><strong>开发语言</strong></td>
<td>C# (.NET 8.0)</td>
</tr>
<tr>
<td><strong>界面框架</strong></td>
<td>WPF + MaterialDesignThemes</td>
</tr>
<tr>
<td><strong>架构模式</strong></td>
<td>MVVM (CommunityToolkit.Mvvm)</td>
</tr>
<tr>
<td><strong>数据库</strong></td>
<td>SQLite (Entity Framework Core)</td>
</tr>
<tr>
<td><strong>识别服务</strong></td>
<td><a href="https://www.animetrace.com/">AnimeTrace API</a></td>
</tr>
<tr>
<td><strong>核心依赖</strong></td>
<td>Microsoft.Extensions.DependencyInjection、System.Threading.Tasks.Dataflow</td>
</tr>
</table>

---

## 使用说明

### 基本流程
1. 启动程序，选择图片输入目录与输出目录
2. 调整 API 并发数与文件操作模式
3. 开始扫描与识别，程序自动保存识别候选
4. 进入确认窗口，查看、选择或修正识别结果
5. 批量确认后，开始自动整理文件

### 导出与导入数据
- 导出：在确认窗口中导出当前识别结果为 `.animesortercache.json`
- 导入：在主界面导入已有的数据文件，直接进入确认流程

### 支持格式
- JPG、JPEG、PNG、WEBP

### 输出目录结构
```
输出目录/
├── 作品名/
│   └── 角色名/
├── 角色名/
└── Unknown/
```

---

## 注意事项

<div style="background-color:#fff8e6; padding:14px; border-radius:8px; border-left:4px solid #ffc107;">
<ul style="margin:0; padding-left:20px;">
<li>支持图片格式：JPG、JPEG、PNG、WEBP</li>
<li>使用识别功能需要保持网络连接</li>
<li>建议分辨率 1920×1080 及以上</li>
<li>缓存数据库路径：%LocalAppData%\AnimeSorterWin\</li>
</ul>
</div>

---

## 性能优化

- 异步处理管道：基于 TPL Dataflow 构建稳定高效处理流程
- 流式文件扫描：避免一次性加载大量文件至内存
- 背压控制：防止内存占用过高
- 并发哈希计算：充分利用多核处理器提升效率

---

## 常见问题

1. **无法识别**：检查网络状态、图片清晰度与文件大小
2. **识别结果不准确**：使用确认窗口手动选择或输入修正
3. **程序无响应**：检查网络与 API 状态，尝试重启程序
4. **API 限流提示**：降低并发数设置，减少请求频率

---

## 更新历史

### v2.0.0 - 全新 .NET 版本重构
- 技术栈升级：从 Python 迁移至 .NET 8.0 + WPF
- 新增智能缓存系统
- 新增 API 限流与自动重试机制
- 新增交互式确认窗口
- 新增三种分类模式
- 新增复制/移动文件模式
- 新增数据导出与导入功能
- 新增实时统计面板
- 界面全面升级为 MaterialDesign 风格
- 整体性能与稳定性大幅提升

### v1.0.0
- 基础识别与分类功能
- 多模型支持与双分类模式
- 历史记录与输入补全
- 基础界面与跳过功能

---

## 致谢

本项目基于 [AnimeTrace](https://www.animetrace.com/) API 提供的识别能力实现，感谢其提供的精准服务。
