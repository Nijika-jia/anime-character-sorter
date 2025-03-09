import requests
import base64
from pathlib import Path
from typing import Dict, List, Tuple

class ImageProcessor:
    def __init__(self):
        self.api_url = "https://api.animetrace.com/v1/search"
    
    def process_image(self, image_path: Path, model: str = "anime_model_lovelive") -> Dict:
        """处理单张图片，返回API识别结果"""
        try:
            # 读取并编码图片
            with open(image_path, 'rb') as image_file:
                base64_image = base64.b64encode(image_file.read()).decode('utf-8')
            
            # 准备请求数据
            data = {
                'model': model,
                'base64': base64_image
            }
            
            # 发送API请求
            response = requests.post(self.api_url, json=data)
            
            if response.status_code == 200:
                result = response.json()
                return result
            else:
                return {"error": f"API请求失败: {response.status_code}"}
                
        except Exception as e:
            return {"error": f"处理图片时出错: {str(e)}"}
    
    def get_character_info(self, api_result: Dict) -> List[Tuple[str, str]]:
        """从API结果中提取角色信息"""
        character_info = []
        
        if (api_result.get('code') == 0 and 
            api_result.get('data') and 
            len(api_result['data']) > 0 and 
            api_result['data'][0].get('character')):
            
            for char_data in api_result['data'][0]['character']:
                character = char_data.get('character', 'Unknown')
                work = char_data.get('work', 'Unknown Work')
                character_info.append((character, work))
        
        return character_info
    
    def get_works(self, api_result: Dict) -> List[str]:
        """从API结果中提取作品名称"""
        works = set()
        
        if (api_result.get('code') == 0 and 
            api_result.get('data') and 
            len(api_result['data']) > 0 and 
            api_result['data'][0].get('character')):
            
            for char_data in api_result['data'][0]['character']:
                work = char_data.get('work')
                if work:
                    works.add(work)
        
        return list(works) 