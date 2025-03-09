import json
from pathlib import Path

class HistoryManager:
    def __init__(self):
        self.history_file = Path(__file__).parent.parent.parent / "data" / "input_history.json"
        self.character_history = set()
        self.work_history = set()
        self.load_history()
    
    def load_history(self):
        """加载历史记录"""
        if self.history_file.exists():
            try:
                with open(self.history_file, 'r', encoding='utf-8') as f:
                    data = json.load(f)
                    self.character_history = set(data.get('characters', []))
                    self.work_history = set(data.get('works', []))
            except Exception as e:
                print(f"加载历史记录失败: {e}")
                self.character_history = set()
                self.work_history = set()
        
        # 确保至少包含 "unknown" 选项
        self.character_history.add("unknown")
        self.work_history.add("unknown")
    
    def save_history(self):
        """保存历史记录"""
        try:
            # 确保目录存在
            self.history_file.parent.mkdir(parents=True, exist_ok=True)
            
            # 保存数据
            data = {
                'characters': list(sorted(self.character_history)),
                'works': list(sorted(self.work_history))
            }
            with open(self.history_file, 'w', encoding='utf-8') as f:
                json.dump(data, f, ensure_ascii=False, indent=2)
        except Exception as e:
            print(f"保存历史记录失败: {e}")
    
    def add_character(self, character: str):
        """添加角色记录"""
        if character and character.strip():
            self.character_history.add(character.strip())
            self.save_history()
    
    def add_work(self, work: str):
        """添加作品记录"""
        if work and work.strip():
            self.work_history.add(work.strip())
            self.save_history()
    
    def get_character_suggestions(self) -> list:
        """获取角色建议列表"""
        return sorted(self.character_history)
    
    def get_work_suggestions(self) -> list:
        """获取作品建议列表"""
        return sorted(self.work_history) 