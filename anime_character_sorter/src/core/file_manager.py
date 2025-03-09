import shutil
import zipfile
import tempfile
from pathlib import Path
from typing import Dict, List

class FileManager:
    def __init__(self):
        self.temp_base = Path(tempfile.gettempdir()) / "anime_character_sorter"
    
    def get_temp_dir(self) -> Path:
        """获取临时目录"""
        temp_dir = self.temp_base / str(id(self))
        temp_dir.mkdir(parents=True, exist_ok=True)
        return temp_dir
    
    def cleanup_temp_dir(self, temp_dir: Path):
        """清理临时目录"""
        if temp_dir.exists() and temp_dir.is_relative_to(self.temp_base):
            shutil.rmtree(temp_dir)
    
    def prepare_directory(self, directory: Path):
        """准备目录，如果存在则清空，不存在则创建"""
        if directory.exists():
            shutil.rmtree(directory)
        directory.mkdir(parents=True)
    
    def sort_by_character(self, images: Dict[Path, tuple], output_dir: Path):
        """按角色分类图片"""
        self.prepare_directory(output_dir)
        
        for image_path, (character, _) in images.items():
            char_dir = output_dir / self._sanitize_filename(character)
            char_dir.mkdir(exist_ok=True)
            
            dest_path = char_dir / image_path.name
            shutil.copy2(image_path, dest_path)
    
    def sort_by_work(self, images: Dict[Path, tuple], output_dir: Path):
        """按作品分类图片"""
        self.prepare_directory(output_dir)
        
        for image_path, (_, work) in images.items():
            work_dir = output_dir / self._sanitize_filename(work)
            work_dir.mkdir(exist_ok=True)
            
            dest_path = work_dir / image_path.name
            shutil.copy2(image_path, dest_path)
    
    def create_zip(self, source_dir: Path, output_path: Path):
        """创建zip压缩包"""
        with zipfile.ZipFile(output_path, 'w', zipfile.ZIP_DEFLATED) as zipf:
            for file in source_dir.rglob('*'):
                if file.is_file():
                    zipf.write(file, file.relative_to(source_dir))
    
    @staticmethod
    def _sanitize_filename(filename: str) -> str:
        """清理文件名，移除不合法字符"""
        # 替换Windows文件系统不允许的字符
        invalid_chars = '<>:"/\\|?*'
        for char in invalid_chars:
            filename = filename.replace(char, '_')
        return filename.strip() 