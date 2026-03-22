# 动漫图片分类助手：一个基于 AI 的动漫角色识别与整理工具
> 基于 AnimeTrace API 的动漫角色图片分类工具，可自动识别角色与作品并按规则整理。

[![Python](https://img.shields.io/badge/Python-3776AB?style=flat-square&logo=python&logoColor=white)](https://www.python.org/)
[![PyQt6](https://img.shields.io/badge/PyQt6-41CD52?style=flat-square&logo=qt6&logoColor=white)](https://www.riverbankcomputing.com/software/pyqt/)
[![PyInstaller](https://img.shields.io/badge/PyInstaller-000000?style=flat-square&logo=python&logoColor=white)](https://pyinstaller.org/)
[![Requests](https://img.shields.io/badge/Requests-FF69B4?style=flat-square&logo=requests&logoColor=white)](https://requests.readthedocs.io/)

---

## 下载
### 直接下载
- [最新版本 v1.0.0](https://github.com/Nijika-jia/anime-character-sorter/releases/latest)
- [历史版本](https://github.com/Nijika-jia/anime-character-sorter/releases)

### 源码构建
```bash
git clone https://github.com/Nijika-jia/anime-character-sorter.git
pip install -r requirements.txt
pyinstaller --noconsole --icon=assets/icon.ico src/main.py
```

---

## 简介
解决动漫图片堆积、角色/作品遗忘、手动整理低效等问题，支持批量识别与分类整理，适合画师、收藏爱好者等使用。

核心能力：
- 自动识别动漫角色与作品
- 按角色/作品双维度分类
- 智能记忆用户修正结果
- 批量处理与结果打包

---

## 功能特点
- **多模型支持**：动画/ Galgame 多精度识别模型可选
- **分类模式**：按角色/作品分类，支持同时启用
- **识别模式**：自动批量处理 / 手动确认修正
- **智能记忆**：保存历史记录，支持输入补全
- **便捷操作**：文件夹拖放、图片跳过、重新识别、结果打包

---

## 技术栈
- **语言**：Python 3.10+
- **GUI**：PyQt6
- **打包**：PyInstaller
- **API**：[AnimeTrace](https://www.animetrace.com/)
- **依赖**：requests、pathlib、json、shutil

---

## 使用说明
### 基本流程
1. 启动程序 → 选择图片文件夹
2. 选择识别模型与分类模式
3. 选择自动/手动模式 → 开始分类

### 手动确认模式
- 查看识别结果，可确认、从历史选择、手动输入或跳过
- 支持切换模型重新识别

### 自动分类模式
- 批量处理所有图片，使用 API 首选结果
- 完成后自动保存并打包为 ZIP

### 输出结构
```
输出目录/
├── by_character/  # 按角色分类
└── by_work/       # 按作品分类
```

---

## 注意事项
- 图片格式：JPG/PNG，单张≤4MB，分辨率≥256×256
- API 限制：服务器繁忙时需重试，可能存在次数限制
- 数据存储：历史记录保存在 `data` 目录，程序自动清理临时文件

---

## 常见问题
1. **无法识别**：切换模型、检查图片清晰度与大小
2. **结果不准**：使用高精度模型或手动修正
3. **程序无响应**：检查网络与服务器状态，重启程序

---

## 更新历史
### v1.0.0
- 基础识别与分类功能
- 多模型与双分类模式
- 历史记录与输入补全
- 界面优化与跳过功能

---

## 致谢
本项目基于 [AnimeTrace](https://www.animetrace.com/) API 实现，感谢其提供的精准识别服务。
