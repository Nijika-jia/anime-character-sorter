import os
import json
import requests

# JSON 文件名
json_file = "data.json"  # 确保 data.json 和这个脚本在同一个文件夹

# 目标文件夹
save_dir = "images"
os.makedirs(save_dir, exist_ok=True)  # 创建 images 文件夹（如果不存在）

# 读取 JSON 数据
with open(json_file, "r", encoding="utf-8") as f:
    data = json.load(f)

# 遍历 JSON 数据并下载图片
for index, item in enumerate(data):
    image_url = item.get("avatar", "")  # 获取图片 URL
    name = item.get("name", f"image_{index}")  # 获取名字（用于命名文件）

    if image_url:
        try:
            # 发送 HTTP 请求下载图片
            response = requests.get(image_url, stream=True, timeout=10)
            if response.status_code == 200:
                # 确保文件名安全
                safe_name = "".join(c if c.isalnum() else "_" for c in name)
                image_path = os.path.join(save_dir, f"{safe_name}.jpg")

                # 写入文件
                with open(image_path, "wb") as img_file:
                    for chunk in response.iter_content(1024):
                        img_file.write(chunk)

                print(f"✅ 成功下载: {image_path}")
            else:
                print(f"❌ 下载失败: {image_url} (状态码: {response.status_code})")
        except Exception as e:
            print(f"⚠️ 发生错误: {image_url} ({e})")

print("🎉 所有图片下载完成！")
