import os

def get_all_file_names(folder_path):
    file_names = []
    # 遍历文件夹及其子文件夹
    for root, dirs, files in os.walk(folder_path):
        for file in files:
            # 只添加文件名
            file_names.append(file)
    return file_names

def save_to_txt(file_names, output_file):
    with open(output_file, 'w', encoding='utf-8') as f:
        for name in file_names:
            f.write(name + '\n')

# 要遍历的文件夹路径，请替换为实际路径
folder_path = 'E:\\xsj666\Documents\Folder11-Ico-main\ico'
# 输出的 txt 文件路径
output_file = 'file_list.txt'

# 获取所有文件名称
all_files = get_all_file_names(folder_path)
# 将文件名称保存到 txt 文件
save_to_txt(all_files, output_file)