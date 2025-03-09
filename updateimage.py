import json

# JSON 文件路径
json_file = "data.json"

# 你的新 GitHub 图床地址
base_url = "https://cdn.jsdelivr.net/gh/Nijika-jia/Nijika-jia.github.io@main/public/girls/avatar/"

# 读取 JSON 文件
with open(json_file, "r", encoding="utf-8") as f:
    data = json.load(f)

# 遍历数据并修改 avatar 字段
for item in data:
    name = item.get("name", "").strip()  # 获取 name 字段
    if name:
        # 确保文件名安全（防止特殊字符导致 URL 失效）
        safe_name = "".join(c if c.isalnum() else "_" for c in name)
        item["avatar"] = f"{base_url}{safe_name}.jpg"  # 更新 avatar 字段

# 保存修改后的 JSON 文件
with open(json_file, "w", encoding="utf-8") as f:
    json.dump(data, f, ensure_ascii=False, indent=4)

print("🎉 JSON 文件更新完成！")
